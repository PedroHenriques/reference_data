using System.Net;
using System.Text;
using System.Text.Json.Serialization;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using DbFixtures.Kafka;
using DbFixtures.Mongodb;
using DbFixtures.Redis;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using StackExchange.Redis;
using Toolkit;
using Toolkit.Types;
using MongodbUtils = Toolkit.Utils.Mongodb;
using RedisUtils = Toolkit.Utils.Redis;
using TkKafkaUtils = Toolkit.Utils.Kafka<Tests.E2E.NotifDataKafkaKey, Tests.E2E.NotifDataKafkaValue>;

namespace Tests.E2E;

[Trait("Type", "E2E")]
public class E2ETests : IDisposable, IAsyncLifetime
{
  private const string DB_NAME = "referenceData";
  private const string ENTITIES_COLL_NAME = "entities";
  private const string TOPIC_NAME = "TestTopic";
  private const string HTTP_SERVER_URL = "http://myapp_test_runner:11000/";
  private readonly HttpClient _httpClient;
  private readonly IAdminClient _adminClient;
  private readonly IMongodb _mongodb;
  private readonly IQueue _dblistenerRedis;
  private readonly IQueue _notificationRedis;
  private readonly IKafka<NotifDataKafkaKey, NotifDataKafkaValue> _kafka;
  private readonly DbFixtures.DbFixtures _mongodbFixtures;
  private readonly DbFixtures.DbFixtures _dblistenerRedisFixtures;
  private readonly DbFixtures.DbFixtures _notificationRedisFixtures;
  private readonly DbFixtures.DbFixtures _kafkaFixtures;
  private readonly HttpListener _listener;

  public E2ETests()
  {
    this._httpClient = new HttpClient();

    var mongoInputs = MongodbUtils.PrepareInputs("mongodb://admin:pw@api_db:27017/admin?authMechanism=SCRAM-SHA-256&replicaSet=rs0", "deletedAt");
    this._mongodb = new Mongodb(mongoInputs);

    var driver = new MongodbDriver(mongoInputs.Client, DB_NAME);
    this._mongodbFixtures = new DbFixtures.DbFixtures([driver]);

    var dblistenerRedisInputs = RedisUtils.PrepareInputs(
      new ConfigurationOptions
      {
        EndPoints = { "dblistener_db:6379" },
        Password = "password",
        AbortOnConnectFail = false,
      },
      "test-dblistener-cg-e2e"
    );
    this._dblistenerRedis = new Redis(dblistenerRedisInputs);

    var redisDriver = new RedisDriver(
      dblistenerRedisInputs.Client, dblistenerRedisInputs.Client.GetDatabase(0),
      new Dictionary<string, DbFixtures.Redis.Types.KeyTypes>
      {
        { "change_resume_data", DbFixtures.Redis.Types.KeyTypes.String },
        { "mongo_changes", DbFixtures.Redis.Types.KeyTypes.Stream },
      }
    );
    this._dblistenerRedisFixtures = new DbFixtures.DbFixtures([redisDriver]);

    var notificationRedisInputs = RedisUtils.PrepareInputs(
      new ConfigurationOptions
      {
        EndPoints = { "notification_db:6379" },
        Password = "other password",
        AbortOnConnectFail = false,
      },
      "test-notification-cg-e2e"
    );
    this._notificationRedis = new Redis(notificationRedisInputs);

    var redisNotificationDriver = new RedisDriver(
      notificationRedisInputs.Client, notificationRedisInputs.Client.GetDatabase(0),
      new Dictionary<string, DbFixtures.Redis.Types.KeyTypes>
      {
        { "entity:my entity|notif configs", DbFixtures.Redis.Types.KeyTypes.String },
        { "dispatcher_retry_queue", DbFixtures.Redis.Types.KeyTypes.Stream },
      }
    );
    this._notificationRedisFixtures = new DbFixtures.DbFixtures([redisNotificationDriver]);

    SchemaRegistryConfig schemaRegistryConfig = new SchemaRegistryConfig { Url = "http://schema-registry:8081" };
    var kafkaInputs = TkKafkaUtils.PrepareInputs(
      schemaRegistryConfig,
      null,
      new ConsumerConfig
      {
        BootstrapServers = "broker:29092",
        GroupId = "real-group-e2e",
        AutoOffsetReset = AutoOffsetReset.Earliest,
        EnableAutoCommit = false,
      }
    );
    this._kafka = new Kafka<NotifDataKafkaKey, NotifDataKafkaValue>(kafkaInputs);

    this._adminClient = new AdminClientBuilder(
      new AdminClientConfig { BootstrapServers = "broker:29092" }
    ).Build();
    var fixtureKafkaConsumer = new ConsumerBuilder<Ignore, Ignore>(
      new ConsumerConfig
      {
        BootstrapServers = "broker:29092",
        GroupId = "cleanup-group-e2e",
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
    .SetKeySerializer(new JsonSerializer<NotifDataKafkaKey>(kafkaInputs.SchemaRegistry, jsonSerializerConfig))
    .SetValueSerializer(new JsonSerializer<NotifDataKafkaValue>(kafkaInputs.SchemaRegistry, jsonSerializerConfig))
    .Build();
    var kafkaDriver = new KafkaDriver<NotifDataKafkaKey, NotifDataKafkaValue>(this._adminClient, fixtureKafkaConsumer, fixtureKafkaProducer);
    this._kafkaFixtures = new DbFixtures.DbFixtures([kafkaDriver]);

    this._listener = new HttpListener();
    this._listener.Prefixes.Add(HTTP_SERVER_URL);
  }

  public void Dispose()
  {
    this._mongodbFixtures.CloseDrivers();
    this._dblistenerRedisFixtures.CloseDrivers();
    this._notificationRedisFixtures.CloseDrivers();
    this._kafkaFixtures.CloseDrivers();
    this._httpClient.Dispose();
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
  public async Task ReferenceData_ItShouldHandleEntityAndEntityDataCorrectly()
  {
    await this._mongodbFixtures.InsertFixtures(
      [ENTITIES_COLL_NAME],
      new Dictionary<string, Entity[]>
      {
        { ENTITIES_COLL_NAME, [] }
      }
    );
    await this._mongodbFixtures.InsertFixtures(
      ["my entity"],
      new Dictionary<string, TestData[]>
      {
        { "my entity", [] },
      }
    );
    await this._dblistenerRedisFixtures.InsertFixtures<string>(
      ["change_resume_data"],
      new Dictionary<string, string[]>
      {
        { "change_resume_data", [] },
      }
    );
    await this._dblistenerRedisFixtures.InsertFixtures<Dictionary<string, string>>(
      ["mongo_changes"],
      new Dictionary<string, Dictionary<string, string>[]>
      {
        { "mongo_changes", [] },
      }
    );

    await this._notificationRedisFixtures.InsertFixtures<string>(
      ["entity:my entity|notif configs", "dispatcher_retry_queue"],
      new Dictionary<string, string[]>
      {
        { "dispatcher_retry_queue", [] },
        { "entity:my entity|notif configs", [] },
      }
    );

    var seedKafkaEvent = new Message<NotifDataKafkaKey, NotifDataKafkaValue>
    {
      Key = new NotifDataKafkaKey { Id = "seed id 1" },
      Value = new NotifDataKafkaValue
      {
        Metadata = new NotifDataKafkaValueMetadata
        {
          Action = "seed insert",
          ActionDatetime = DateTime.Now,
          CorrelationId = "seed insert correlation id",
          EventDatetime = DateTime.Now,
          Source = "integration test",
        },
        Data = new NotifDataKafkaValueData
        {
          Id = "68c0072634336093835452c0",
          Entity = "myname1",
          Document = new Dictionary<string, dynamic?> {
              { "name", "seed data 1" },
            },
        },
      },
    };
    await this._kafkaFixtures.InsertFixtures<Message<NotifDataKafkaKey, NotifDataKafkaValue>>(
      [TOPIC_NAME],
      new Dictionary<string, Message<NotifDataKafkaKey, NotifDataKafkaValue>[]>
      {
        { TOPIC_NAME, [ seedKafkaEvent ] },
      }
    );

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

    var cts = new CancellationTokenSource();
    List<dynamic> actualKafkaEvents = new List<dynamic> { };
    this._kafka.Subscribe(
      [TOPIC_NAME],
      (message, ex) =>
      {
        if (ex != null) { throw ex; }
        if (message == null) { return; }
        actualKafkaEvents.Add(new
        {
          Key = message.Message.Key,
          Value = message.Message.Value,
        });
        this._kafka.Commit(message);
      },
      cts
    );

    Entity entity = new Entity
    {
      Id = ObjectId.GenerateNewId().ToString(),
      Name = "my entity",
      NotifConfigs = [
        new NotifConfig { Protocol = "webhook", TargetURL = HTTP_SERVER_URL },
        new NotifConfig { Protocol = "event", TargetURL = TOPIC_NAME },
      ]
    };

    HttpContent content = new StringContent(JsonConvert.SerializeObject(new Entity[] { entity }), Encoding.UTF8, "application/json");

    var result = await this._httpClient.PostAsync("http://api:10000/v1/entities/", content);
    string body = await result.Content.ReadAsStringAsync();

    Assert.True(result.IsSuccessStatusCode, body);
    Assert.Equal(JsonConvert.SerializeObject(new Entity[] { entity }), body);

    var entityFindRes = await this._mongodb.Find<Entity>(DB_NAME, ENTITIES_COLL_NAME, 1, 10, null, false, new BsonDocument { { "name", 1 } });
    var entityDocs = entityFindRes.Data;

    Assert.Single(entityDocs);
    Assert.Equal(JsonConvert.SerializeObject(entity), JsonConvert.SerializeObject(entityDocs[0]));

    await Task.Delay(10000);

    var (_, mongoChangeMsg1) = await this._dblistenerRedis.Dequeue("mongo_changes", "test-dblistener-cg-e2e-0");
    var mongoChangeMsgChangeTime = DateTimeOffset.Parse((string)JsonConvert.DeserializeObject<dynamic>(mongoChangeMsg1).ChangeTime).ToUniversalTime();
    Assert.Equal(
      JsonConvert.SerializeObject(new
      {
        ChangeTime = mongoChangeMsgChangeTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"), // Not relevant for this test
        ChangeRecord = JsonConvert.SerializeObject(new
        {
          id = entity.Id,
          changeType = new { Id = 1, Name = "insert" },
          document = new { _id = entity.Id, name = entity.Name, description = entity.Desc, deletedAt = entity.DeletedAt, notifConfigs = entity.NotifConfigs },
        }),
        Source = JsonConvert.SerializeObject(new { dbName = "referenceData", collName = "entities" }),
        NotifConfigs = (object?)null,
      }),
      mongoChangeMsg1
    );

    var cachedNotifConfig = await ((ICache)this._notificationRedis).GetString("entity:my entity|notif configs");
    Assert.Equal(JsonConvert.SerializeObject(entity.NotifConfigs), cachedNotifConfig);


    TestData entityData = new TestData
    {
      Name = "test doc 1",
      Desc = null,
      DeletedAt = null,
    };
    content = new StringContent(JsonConvert.SerializeObject(new TestData[] { entityData }), Encoding.UTF8, "application/json");

    result = await this._httpClient.PostAsync($"http://api:10000/v1/data/{entity.Id}", content);
    body = await result.Content.ReadAsStringAsync();
    TestData[] bodyEntity = JsonConvert.DeserializeObject<TestData[]>(body);

    Assert.True(result.IsSuccessStatusCode, body);
    entityData.Id = bodyEntity[0].Id;
    // I'm deserializing and then serializing the body to make sure the properties are in the same order as in data, since the API is returning
    // them in swapped order
    Assert.Equal(JsonConvert.SerializeObject(new TestData[] { entityData }), JsonConvert.SerializeObject(bodyEntity));

    var entityDataFindRes = await this._mongodb.Find<TestData>(DB_NAME, entity.Name, 1, 10, null, false, new BsonDocument { { "name", 1 } });
    var entityDataDocs = entityDataFindRes.Data;

    Assert.Equal(JsonConvert.SerializeObject(new TestData[] { entityData }), JsonConvert.SerializeObject(entityDataDocs));

    var (_, mongoChangeMsg2) = await this._dblistenerRedis.Dequeue("mongo_changes", "test-dblistener-cg-e2e-0");
    var mongoChangeMsg2ChangeTime = DateTimeOffset.Parse((string)JsonConvert.DeserializeObject<dynamic>(mongoChangeMsg2).ChangeTime).ToUniversalTime();
    Assert.Equal(
      JsonConvert.SerializeObject(new
      {
        ChangeTime = mongoChangeMsg2ChangeTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"), // Not relevant for this test
        ChangeRecord = JsonConvert.SerializeObject(new
        {
          id = entityData.Id,
          changeType = new { Id = 1, Name = "insert" },
          document = new { _id = entityData.Id, name = entityData.Name, description = entityData.Desc, deletedAt = entityData.DeletedAt },
        }),
        Source = JsonConvert.SerializeObject(new { dbName = "referenceData", collName = entity.Name }),
        NotifConfigs = (object?)null,
      }),
      mongoChangeMsg2
    );

    var webhookMessageStr = await receivedBodyTcs.Task;
    var webhookMessage = JsonConvert.DeserializeObject<dynamic>(webhookMessageStr);
    Assert.Equal(
      JsonConvert.SerializeObject(new
      {
        eventTime = webhookMessage.eventTime, // Not relevant for this test
        changeTime = mongoChangeMsg2ChangeTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
        id = entityData.Id,
        changeType = "insert",
        entity = entity.Name,
        document = new
        {
          name = entityData.Name,
          description = entityData.Desc,
          deletedAt = entityData.DeletedAt,
          id = entityData.Id
        },
      }),
      webhookMessageStr
    );

    cts.Cancel();
    await Task.Delay(20000);
    Assert.Equal(2, actualKafkaEvents.Count);
    Assert.Equal(JsonConvert.SerializeObject(seedKafkaEvent.Key), JsonConvert.SerializeObject(actualKafkaEvents[0].Key));
    Assert.Equal(JsonConvert.SerializeObject(seedKafkaEvent.Value), JsonConvert.SerializeObject(actualKafkaEvents[0].Value));
    Assert.Equal(
      JsonConvert.SerializeObject(new NotifDataKafkaKey { Id = entityData.Id }),
      JsonConvert.SerializeObject(actualKafkaEvents[1].Key)
    );
    Assert.Equal(
      JsonConvert.SerializeObject(new NotifDataKafkaValue
      {
        Metadata = new NotifDataKafkaValueMetadata
        {
          Action = "CREATE",
          ActionDatetime = DateTimeOffset.Parse(mongoChangeMsg2ChangeTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'")).UtcDateTime,
          CorrelationId = actualKafkaEvents[1].Value.Metadata.CorrelationId, // Not relevant for this test
          EventDatetime = actualKafkaEvents[1].Value.Metadata.EventDatetime, // Not relevant for this test
          Source = "myapp",
        },
        Data = new NotifDataKafkaValueData
        {
          Id = entityData.Id,
          Entity = entity.Name,
          Document = new Dictionary<string, dynamic?> {
            { "name", entityData.Name },
            { "description", entityData.Desc },
            { "deletedAt", entityData.DeletedAt },
            { "id", entityData.Id },
          },
        },
      }),
      JsonConvert.SerializeObject(actualKafkaEvents[1].Value)
    );
  }
}

public class TestData
{
  [JsonPropertyName("id")]
  [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
  [BsonId]
  [BsonRepresentation(BsonType.ObjectId)]
  [BsonIgnoreIfDefault]
  public string? Id { get; set; }

  [JsonPropertyName("name")]
  [JsonProperty("name")]
  [BsonElement("name")]
  public required string Name { get; set; }

  [JsonPropertyName("description")]
  [JsonProperty("description")]
  [BsonElement("description")]
  public string? Desc { get; set; }

  [JsonPropertyName("deletedAt")]
  [JsonProperty("deletedAt")]
  [BsonElement("deletedAt")]
  public DateTime? DeletedAt { get; set; }
}

public class Entity
{
  [JsonPropertyName("id")]
  [JsonProperty("id")]
  [BsonId]
  [BsonRepresentation(BsonType.ObjectId)]
  [BsonIgnoreIfDefault]
  public string? Id { get; set; }

  [JsonPropertyName("name")]
  [JsonProperty("name")]
  [BsonElement("name")]
  public required string Name { get; set; }

  [JsonPropertyName("description")]
  [JsonProperty("description")]
  [BsonElement("description")]
  public string? Desc { get; set; }

  [JsonPropertyName("deletedAt")]
  [JsonProperty("deletedAt")]
  [BsonElement("deletedAt")]
  public DateTime? DeletedAt { get; set; }

  [JsonPropertyName("notifConfigs")]
  [JsonProperty("notifConfigs")]
  [BsonElement("notifConfigs")]
  public NotifConfig[]? NotifConfigs { get; set; }
}

public struct NotifConfig
{
  [JsonPropertyName("protocol")]
  [JsonProperty("protocol")]
  [BsonElement("protocol")]
  public required string Protocol { get; set; }

  [JsonPropertyName("targetUrl")]
  [JsonProperty("targetUrl")]
  [BsonElement("targetUrl")]
  public required string TargetURL { get; set; }
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