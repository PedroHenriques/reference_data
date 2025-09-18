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
using TkKafkaUtils = Toolkit.Utils.Kafka<Notification.Tests.Integration.TestKey, Notification.Tests.Integration.TestValue>;
using TkRedisUtils = Toolkit.Utils.Redis;

namespace Notification.Tests.Integration;

[Trait("Type", "Integration")]
public class NotificationTests : IDisposable, IAsyncLifetime
{
  private const string TOPIC_NAME = "TestTopic";
  private const string HTTP_SERVER_URL = "http://localhost:11000/";
  private readonly IAdminClient _adminClient;
  private readonly DbFixtures.DbFixtures _redisDblistenerDbFixtures;
  private readonly DbFixtures.DbFixtures _redisNotificationDbFixtures;
  private readonly DbFixtures.DbFixtures _kafkaDbFixtures;
  private readonly IQueue _redisDblistener;
  private readonly ICache _redisNotification;
  private readonly IKafka<TestKey, TestValue> _kafka;
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
      "test-dblistener-cg"
    );
    this._redisDblistener = new Redis(redisDblistenerInputs);

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
      "test-notification-cg"
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
    var kafkaInputs = TkKafkaUtils.PrepareInputs(
      schemaRegistryConfig, null,
      new ConsumerConfig
      {
        BootstrapServers = "broker:29092",
        GroupId = "real-group",
        AutoOffsetReset = AutoOffsetReset.Earliest,
        EnableAutoCommit = false,
      }
    );
    this._kafka = new Kafka<TestKey, TestValue>(kafkaInputs);

    this._adminClient = new AdminClientBuilder(
      new AdminClientConfig { BootstrapServers = "broker:29092" }
    ).Build();
    var fixtureKafkaConsumer = new ConsumerBuilder<Ignore, Ignore>(
      new ConsumerConfig
      {
        BootstrapServers = "broker:29092",
        GroupId = "cleanup-group",
        AutoOffsetReset = AutoOffsetReset.Latest
      }
    ).Build();
    var jsonSerializerConfig = new JsonSerializerConfig
    {
      AutoRegisterSchemas = true,
    };
    var fixtureKafkaProducer = new ProducerBuilder<TestKey, TestValue>(
      new ProducerConfig
      {
        BootstrapServers = "broker:29092",
      }
    )
    .SetKeySerializer(new JsonSerializer<TestKey>(kafkaInputs.SchemaRegistry, jsonSerializerConfig))
    .SetValueSerializer(new JsonSerializer<TestValue>(kafkaInputs.SchemaRegistry, jsonSerializerConfig))
    .Build();
    var kafkaDriver = new KafkaDriver<TestKey, TestValue>(this._adminClient, fixtureKafkaConsumer, fixtureKafkaProducer);
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
        new TopicSpecification { Name = TOPIC_NAME, NumPartitions = 1, ReplicationFactor = 1 },
      });
    }
    catch (CreateTopicsException ex)
    {
      if (ex.Results.Any(r => r.Error.Code != ErrorCode.TopicAlreadyExists)) { throw; }
    }
  }

  [Fact]
  public async Task Notification_ItShould()
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

    Message<TestKey, TestValue>[] expectedKafkaEvents = [
      new Message<TestKey, TestValue>
      {
        Key = new TestKey { Id = "seed id 1" },
        Value = new TestValue {
          Metadata = new TestValueMetadata {
            Action = "seed insert",
            ActionDatetime = DateTime.Now,
            CorrelationId = "seed insert correlation id",
            EventDatetime = DateTime.Now,
            Source = "integration test",
          },
          Data = new TestValueData {
            Id = "68c0072634336093835452c0",
            Entity = "myname1",
            Document = new Dictionary<string, dynamic?> {
              { "name", "seed data 1" },
            },
          },
        },
      },
    ];

    await this._kafkaDbFixtures.InsertFixtures<Message<TestKey, TestValue>>(
      [TOPIC_NAME],
      new Dictionary<string, Message<TestKey, TestValue>[]>
      {
        { TOPIC_NAME, [ expectedKafkaEvents[0] ] },
      }
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
              new { protocol = "event", targetUrl = TOPIC_NAME },
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
                "ChangeTime",
                "2025-09-18T13:32:40Z"
              },
              {
                "ChangeRecord",
                JsonConvert.SerializeObject(new {
                  id = "68cc09f86533312d1c9d4863",
                  changeType = new {
                    Id = 1,
                    Name = "insert",
                  },
                  document = new {
                    _id = "68cc09f86533312d1c9d4863",
                    key1 = true,
                    key2 = "some content",
                    key3 = 349857,
                  },
                })
              },
              {
                "Source",
                JsonConvert.SerializeObject(new {
                  dbName = "test db name",
                  collName = "myname1",
                })
              },
              { "NotifConfigs", JsonConvert.SerializeObject(null) },
            },
          ]
        },
      }
    );

    await Task.Delay(1000);

    Assert.Equal("", await receivedBodyTcs.Task);
  }
}

public class TestKey
{
  [JsonPropertyName("id")]
  [JsonProperty("id")]
  public required string Id { get; set; }
}

public class TestValue
{
  [JsonPropertyName("metadata")]
  [JsonProperty("metadata")]
  public required TestValueMetadata Metadata { get; set; }

  [JsonPropertyName("data")]
  [JsonProperty("data")]
  public required TestValueData Data { get; set; }
}

public class TestValueMetadata
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

public class TestValueData
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