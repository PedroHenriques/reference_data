using System.Net;
using System.Text;
using System.Text.Json.Serialization;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using DbFixtures.Kafka;
using DbFixtures.Redis;
using Newtonsoft.Json;
using StackExchange.Redis;
using Toolkit;
using Toolkit.Types;
using TkKafkaUtils = Toolkit.Utils.Kafka<Notification.Tests.Integration.NotifDataKafkaKey, Notification.Tests.Integration.NotifDataKafkaValue>;
using TkRedisUtils = Toolkit.Utils.Redis;

namespace Notification.Tests.Integration;

[Trait("Type", "Integration")]
[Collection("IntegrationTests")]
public class NotificationTests : IDisposable, IAsyncLifetime
{
  private const string TOPIC_NAME_1 = "TestTopic";
  private const string TOPIC_NAME_2 = "TestTopic2";
  private const string HTTP_SERVER_URL = "http://myapp_test_runner:11000/";
  private readonly IAdminClient _adminClient;
  private readonly DbFixtures.DbFixtures _redisDblistenerDbFixtures;
  private readonly DbFixtures.DbFixtures _redisNotificationDbFixtures;
  private readonly DbFixtures.DbFixtures _kafkaDbFixtures;
  private readonly IQueue _redisNotification;
  private readonly IKafka<NotifDataKafkaKey, NotifDataKafkaValue> _kafka1;
  private readonly IKafka<NotifDataKafkaKey, NotifDataKafkaValue> _kafka2;
  private readonly HttpListener _listener;

  public NotificationTests()
  {
    var redisDblistenerInputs = TkRedisUtils.PrepareInputs(
      new ConfigurationOptions
      {
        EndPoints = { "dblistener_db:6379" },
        Password = "password",
        AbortOnConnectFail = false,
      },
      "test-notification-dblistener-cg-integration"
    );
    var redisDblistenerDriver = new RedisDriver(
      redisDblistenerInputs.Client, redisDblistenerInputs.Client.GetDatabase(0),
      new Dictionary<string, DbFixtures.Redis.Types.KeyTypes>
      {
        { "mongo_changes", DbFixtures.Redis.Types.KeyTypes.Stream },
      }
    );
    this._redisDblistenerDbFixtures = new DbFixtures.DbFixtures([redisDblistenerDriver]);

    var redisNotificationInputs = TkRedisUtils.PrepareInputs(
      new ConfigurationOptions
      {
        EndPoints = { "notification_db:6379" },
        Password = "other password",
        AbortOnConnectFail = false,
      },
      "test-notification-cg-integration"
    );
    this._redisNotification = new Redis(redisNotificationInputs);

    var redisNotificationDriver = new RedisDriver(
      redisNotificationInputs.Client, redisNotificationInputs.Client.GetDatabase(0),
      new Dictionary<string, DbFixtures.Redis.Types.KeyTypes>
      {
        { "entity:myname1|notif configs", DbFixtures.Redis.Types.KeyTypes.String },
        { "dispatcher_retry_queue", DbFixtures.Redis.Types.KeyTypes.Stream },
      }
    );
    this._redisNotificationDbFixtures = new DbFixtures.DbFixtures([redisNotificationDriver]);

    SchemaRegistryConfig schemaRegistryConfig = new SchemaRegistryConfig { Url = "http://schema-registry:8081" };
    var kafkaInputs1 = TkKafkaUtils.PrepareInputs(
      schemaRegistryConfig,
      null,
      new ConsumerConfig
      {
        BootstrapServers = "broker:29092",
        GroupId = "real-group-1-notification-integration",
        AutoOffsetReset = AutoOffsetReset.Earliest,
        EnableAutoCommit = false,
      }
    );
    this._kafka1 = new Kafka<NotifDataKafkaKey, NotifDataKafkaValue>(kafkaInputs1);
    var kafkaInputs2 = TkKafkaUtils.PrepareInputs(
      schemaRegistryConfig,
      null,
      new ConsumerConfig
      {
        BootstrapServers = "broker:29092",
        GroupId = "real-group-2-notification-integration",
        AutoOffsetReset = AutoOffsetReset.Earliest,
        EnableAutoCommit = false,
      }
    );
    this._kafka2 = new Kafka<NotifDataKafkaKey, NotifDataKafkaValue>(kafkaInputs2);

    this._adminClient = new AdminClientBuilder(
      new AdminClientConfig { BootstrapServers = "broker:29092" }
    ).Build();
    var fixtureKafkaConsumer = new ConsumerBuilder<Ignore, Ignore>(
      new ConsumerConfig
      {
        BootstrapServers = "broker:29092",
        GroupId = "cleanup-group-notification-integration",
        AutoOffsetReset = AutoOffsetReset.Latest
      }
    ).Build();
    var jsonSerializerConfig = new JsonSerializerConfig
    {
      AutoRegisterSchemas = true,
    };
    var fixtureKafkaProducer = new ProducerBuilder<NotifDataKafkaKey, NotifDataKafkaValue>(
      new ProducerConfig
      {
        BootstrapServers = "broker:29092",
      }
    )
    .SetKeySerializer(new JsonSerializer<NotifDataKafkaKey>(kafkaInputs1.SchemaRegistry, jsonSerializerConfig))
    .SetValueSerializer(new JsonSerializer<NotifDataKafkaValue>(kafkaInputs1.SchemaRegistry, jsonSerializerConfig))
    .Build();
    var kafkaDriver = new KafkaDriver<NotifDataKafkaKey, NotifDataKafkaValue>(this._adminClient, fixtureKafkaConsumer, fixtureKafkaProducer);
    this._kafkaDbFixtures = new DbFixtures.DbFixtures([kafkaDriver]);

    this._listener = new HttpListener();
    this._listener.Prefixes.Add(HTTP_SERVER_URL);
  }

  public void Dispose()
  {
    this._redisDblistenerDbFixtures.CloseDrivers();
    this._redisNotificationDbFixtures.CloseDrivers();
    this._kafkaDbFixtures.CloseDrivers();
    this._listener.Close();
  }

  public Task DisposeAsync()
  {
    return Task.CompletedTask;
  }

  public async Task InitializeAsync()
  {
    try
    {
      await _adminClient.CreateTopicsAsync(new[]
      {
        new TopicSpecification { Name = TOPIC_NAME_1, NumPartitions = 1, ReplicationFactor = 1 },
        new TopicSpecification { Name = TOPIC_NAME_2, NumPartitions = 1, ReplicationFactor = 1 },
      });
    }
    catch (CreateTopicsException ex) when (
      ex.Results.All(r => r.Error.IsError == false || r.Error.Code == ErrorCode.TopicAlreadyExists)
    )
    { }
  }

  [Fact]
  public async Task Notification_IfMessageIsForEntity_ItShouldCacheTheEntityNotifConfigurationAndNotDispatchNotifications()
  {
    this._listener.Start();

    var listenerCallCount = 0;
    var _ = Task.Run(async () =>
    {
      var ctx = await this._listener.GetContextAsync();
      var req = ctx.Request;
      var res = ctx.Response;

      var reader = new StreamReader(
        req.InputStream,
        Encoding.UTF8,
        detectEncodingFromByteOrderMarks: true,
        bufferSize: 1024,
        leaveOpen: true
      );

      listenerCallCount++;

      res.StatusCode = (int)HttpStatusCode.OK;
      await res.OutputStream.FlushAsync();
      res.Close();
    });

    await this._kafkaDbFixtures.InsertFixtures<Message<NotifDataKafkaKey, NotifDataKafkaValue>>(
      [TOPIC_NAME_1, TOPIC_NAME_2],
      new Dictionary<string, Message<NotifDataKafkaKey, NotifDataKafkaValue>[]>
      {
        { TOPIC_NAME_1, [] },
        { TOPIC_NAME_2, [] },
      }
    );

    var cts = new CancellationTokenSource();
    List<dynamic> actualKafkaEvents = new List<dynamic> { };
    this._kafka1.Subscribe(
      [TOPIC_NAME_1],
      (message, ex) =>
      {
        if (ex != null) { throw ex; }
        if (message == null) { return; }
        actualKafkaEvents.Add(new
        {
          Key = message.Message.Key,
          Value = message.Message.Value,
        });
        this._kafka1.Commit(message);
      },
      cts
    );

    await this._redisNotificationDbFixtures.InsertFixtures<string>(
      ["entity:myname1|notif configs", "dispatcher_retry_queue"],
      new Dictionary<string, string[]>
      {
        { "dispatcher_retry_queue", [] },
        { "entity:myname1|notif configs", [] },
      }
    );

    var notifConfig = new object[] {
      new { protocol = "webhook", targetUrl = HTTP_SERVER_URL },
      new { protocol = "event", targetUrl = TOPIC_NAME_1 },
    };
    var notifConfigStr = JsonConvert.SerializeObject(notifConfig);

    await this._redisDblistenerDbFixtures.InsertFixtures<Dictionary<string, string>>(
      ["mongo_changes"],
      new Dictionary<string, Dictionary<string, string>[]>
      {
        {
          "mongo_changes",
          [
            new Dictionary<string, string> {
              {
                "data",
                JsonConvert.SerializeObject(new {
                  ChangeTime = "2025-09-18T13:32:40Z",
                  ChangeRecord = JsonConvert.SerializeObject(new {
                    id = "68cc09f86533312d1c9d4863",
                    changeType = new {
                      Id = 1,
                      Name = "insert",
                    },
                    document = new
                    {
                      _id = "68cc09f86533312d1c9d4863",
                      name = "myname1",
                      description = "some content",
                      notifConfigs = notifConfig,
                    },
                  }),
                  Source = JsonConvert.SerializeObject(new {
                    dbName = "test db name",
                    collName = "entities",
                  }),
                  NotifConfigs = (object?)null,
                })
              }
            },
          ]
        },
      }
    );

    await Task.Delay(5000);

    var cachedNotifConfig = await ((ICache)this._redisNotification).GetString("entity:myname1|notif configs");
    Assert.Equal(notifConfigStr, cachedNotifConfig);

    var (retryId, retryMsg) = await this._redisNotification.Dequeue("dispatcher_retry_queue", "verify_cg");
    Assert.Null(retryId);
    Assert.Null(retryMsg);

    Assert.Equal(0, listenerCallCount);

    cts.Cancel();
    Assert.Empty(actualKafkaEvents);
  }

  [Fact]
  public async Task Notification_IfMessageIsForEntityData_ItShouldDispatchNotificationToHttpAndKafka()
  {
    this._listener.Start();

    var receivedBodyTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
    var _ = Task.Run(async () =>
    {
      var ctx = await this._listener.GetContextAsync();
      var req = ctx.Request;
      var res = ctx.Response;

      var reader = new StreamReader(
        req.InputStream,
        Encoding.UTF8,
        detectEncodingFromByteOrderMarks: true,
        bufferSize: 1024,
        leaveOpen: true
      );

      receivedBodyTcs.TrySetResult(await reader.ReadToEndAsync());

      res.StatusCode = (int)HttpStatusCode.OK;
      await res.OutputStream.FlushAsync();
      res.Close();
    });

    List<Message<NotifDataKafkaKey, NotifDataKafkaValue>> expectedKafkaEvents = new List<Message<NotifDataKafkaKey, NotifDataKafkaValue>> {
      new Message<NotifDataKafkaKey, NotifDataKafkaValue>
      {
        Key = new NotifDataKafkaKey { Id = "seed id 1" },
        Value = new NotifDataKafkaValue {
          Metadata = new NotifDataKafkaValueMetadata {
            Action = "seed insert",
            ActionDatetime = DateTime.Now,
            CorrelationId = "seed insert correlation id",
            EventDatetime = DateTime.Now,
            Source = "integration test",
          },
          Data = new NotifDataKafkaValueData {
            Id = "68c0072634336093835452c0",
            Entity = "myname1",
            Document = new Dictionary<string, dynamic?> {
              { "name", "seed data 1" },
            },
          },
        },
      },
    };

    await this._kafkaDbFixtures.InsertFixtures<Message<NotifDataKafkaKey, NotifDataKafkaValue>>(
      [TOPIC_NAME_1, TOPIC_NAME_2],
      new Dictionary<string, Message<NotifDataKafkaKey, NotifDataKafkaValue>[]>
      {
        { TOPIC_NAME_1, [] },
        { TOPIC_NAME_2, [ expectedKafkaEvents[0] ] },
      }
    );

    var cts = new CancellationTokenSource();
    List<dynamic> actualKafkaEvents = new List<dynamic> { };
    this._kafka2.Subscribe(
      [TOPIC_NAME_2],
      (message, ex) =>
      {
        if (ex != null) { throw ex; }
        if (message == null) { return; }
        actualKafkaEvents.Add(new
        {
          Key = message.Message.Key,
          Value = message.Message.Value,
        });
        this._kafka2.Commit(message);
      },
      cts
    );

    await this._redisNotificationDbFixtures.InsertFixtures<string>(
      ["entity:myname1|notif configs", "dispatcher_retry_queue"],
      new Dictionary<string, string[]>
      {
        { "dispatcher_retry_queue", [] },
        {
          "entity:myname1|notif configs",
          [
            JsonConvert.SerializeObject(new object[] {
              new { protocol = "webhook", targetUrl = HTTP_SERVER_URL },
              new { protocol = "event", targetUrl = TOPIC_NAME_2 },
            }),
          ]
        },
      }
    );

    await this._redisDblistenerDbFixtures.InsertFixtures<Dictionary<string, string>>(
      ["mongo_changes"],
      new Dictionary<string, Dictionary<string, string>[]>
      {
        {
          "mongo_changes",
          [
            new Dictionary<string, string> {
              {
                "data",
                JsonConvert.SerializeObject(new {
                  ChangeTime = "2025-09-18T13:32:40Z",
                  ChangeRecord = JsonConvert.SerializeObject(new {
                    id = "68cc09f86533312d1c9d4863",
                    changeType = new {
                      Id = 1,
                      Name = "insert",
                    },
                    document = new
                    {
                      _id = "68cc09f86533312d1c9d4863",
                      key1 = true,
                      key2 = "some content",
                      key3 = 349857,
                    },
                  }),
                  Source = JsonConvert.SerializeObject(new {
                    dbName = "test db name",
                    collName = "myname1",
                  }),
                  NotifConfigs = (object?)null,
                })
              }
            },
          ]
        },
      }
    );

    await Task.Delay(5000);

    var (retryId, retryMsg) = await this._redisNotification.Dequeue("dispatcher_retry_queue", "verify_cg");
    Assert.Null(retryId);
    Assert.Null(retryMsg);

    var webhookMessageStr = await receivedBodyTcs.Task;
    var webhookMessage = JsonConvert.DeserializeObject<dynamic>(webhookMessageStr);
    Assert.Equal(
      JsonConvert.SerializeObject(new
      {
        eventTime = webhookMessage.eventTime, // Not relevant for this test
        changeTime = "2025-09-18T13:32:40Z",
        id = "68cc09f86533312d1c9d4863",
        changeType = "insert",
        entity = "myname1",
        document = new
        {
          key1 = true,
          key2 = "some content",
          key3 = 349857,
          id = "68cc09f86533312d1c9d4863",
        },
      }),
      webhookMessageStr
    );

    cts.Cancel();
    Assert.Equal(2, actualKafkaEvents.Count);

    expectedKafkaEvents.Add(
      new Message<NotifDataKafkaKey, NotifDataKafkaValue>
      {
        Key = new NotifDataKafkaKey { Id = "68cc09f86533312d1c9d4863" },
        Value = new NotifDataKafkaValue
        {
          Metadata = new NotifDataKafkaValueMetadata
          {
            Action = "CREATE",
            ActionDatetime = DateTimeOffset.Parse("2025-09-18T13:32:40Z").UtcDateTime,
            CorrelationId = actualKafkaEvents[1].Value.Metadata.CorrelationId, // Not relevant for this test
            EventDatetime = actualKafkaEvents[1].Value.Metadata.EventDatetime, // Not relevant for this test
            Source = "myapp",
          },
          Data = new NotifDataKafkaValueData
          {
            Id = "68cc09f86533312d1c9d4863",
            Entity = "myname1",
            Document = new Dictionary<string, dynamic?> {
              { "key1", true },
              { "key2", "some content" },
              { "key3", 349857 },
              { "id", "68cc09f86533312d1c9d4863" },
            },
          },
        },
      }
    );

    Assert.Equal(JsonConvert.SerializeObject(expectedKafkaEvents[0].Key), JsonConvert.SerializeObject(actualKafkaEvents[0].Key));
    Assert.Equal(JsonConvert.SerializeObject(expectedKafkaEvents[0].Value), JsonConvert.SerializeObject(actualKafkaEvents[0].Value));
    Assert.Equal(JsonConvert.SerializeObject(expectedKafkaEvents[1].Key), JsonConvert.SerializeObject(actualKafkaEvents[1].Key));
    Assert.Equal(JsonConvert.SerializeObject(expectedKafkaEvents[1].Value), JsonConvert.SerializeObject(actualKafkaEvents[1].Value));
  }
}

public class NotifDataKafkaKey
{
  [JsonPropertyName("id")]
  [JsonProperty("id")]
  public required string Id { get; set; }
}

public class NotifDataKafkaValue
{
  [JsonPropertyName("metadata")]
  [JsonProperty("metadata")]
  public required NotifDataKafkaValueMetadata Metadata { get; set; }

  [JsonPropertyName("data")]
  [JsonProperty("data")]
  public required NotifDataKafkaValueData Data { get; set; }
}

public class NotifDataKafkaValueMetadata
{
  [JsonPropertyName("action")]
  [JsonProperty("action")]
  public required string Action { get; set; }

  [JsonPropertyName("actionDatetime")]
  [JsonProperty("actionDatetime")]
  public required DateTime ActionDatetime { get; set; }

  [JsonPropertyName("correlationId")]
  [JsonProperty("correlationId")]
  public required string CorrelationId { get; set; }

  [JsonPropertyName("eventDatetime")]
  [JsonProperty("eventDatetime")]
  public required DateTime EventDatetime { get; set; }

  [JsonPropertyName("source")]
  [JsonProperty("source")]
  public required string Source { get; set; }
}

public class NotifDataKafkaValueData
{
  [JsonPropertyName("document")]
  [JsonProperty("document")]
  public required Dictionary<string, dynamic?>? Document { get; set; }

  [JsonPropertyName("entity")]
  [JsonProperty("entity")]
  public required string Entity { get; set; }

  [JsonPropertyName("id")]
  [JsonProperty("id")]
  public required string Id { get; set; }
}