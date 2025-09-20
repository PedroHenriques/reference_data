using System.Text.Json.Serialization;
using DbFixtures.Mongodb;
using DbFixtures.Redis;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace DbListener.Tests.Integration;

[Trait("Type", "Integration")]
[Collection("IntegrationTests")]
public class DbListenerTests : IDisposable
{
  private const string DB_NAME = "referenceData";
  private readonly IMongoClient _mongoClient;
  private readonly DbFixtures.DbFixtures _mongoDbFixtures;
  private readonly IConnectionMultiplexer _redisClient;
  private readonly DbFixtures.DbFixtures _redisDbFixtures;
  private readonly IDatabase _redisDb;

  public DbListenerTests()
  {
    string connStr = "mongodb://admin:pw@api_db:27017/admin?authMechanism=SCRAM-SHA-256&replicaSet=rs0";
    this._mongoClient = new MongoClient(connStr);
    var mongoDriver = new MongodbDriver(this._mongoClient, DB_NAME);
    this._mongoDbFixtures = new DbFixtures.DbFixtures([mongoDriver]);

    this._redisClient = ConnectionMultiplexer.Connect(
      new ConfigurationOptions
      {
        EndPoints = { "dblistener_db:6379" },
        Password = "password",
        AbortOnConnectFail = false,
      }
    );
    this._redisDb = this._redisClient.GetDatabase(0);
    var redisDriver = new RedisDriver(
      this._redisClient, this._redisDb,
      new Dictionary<string, DbFixtures.Redis.Types.KeyTypes>
      {
        { "change_resume_data", DbFixtures.Redis.Types.KeyTypes.String },
        { "mongo_changes", DbFixtures.Redis.Types.KeyTypes.Stream },
      }
    );
    this._redisDbFixtures = new DbFixtures.DbFixtures([redisDriver]);
  }

  public void Dispose()
  {
    this._mongoDbFixtures.CloseDrivers();
    this._redisDbFixtures.CloseDrivers();
  }

  [Fact]
  public async Task DbListener_ItShouldInsertTheCorrectMessageInTheRedisQueue()
  {
    await this._redisDbFixtures.InsertFixtures<string>(
      ["change_resume_data"],
      new Dictionary<string, string[]>
      {
        { "change_resume_data", [] },
      }
    );
    await this._redisDbFixtures.InsertFixtures<Dictionary<string, string>>(
      ["mongo_changes"],
      new Dictionary<string, Dictionary<string, string>[]>
      {
        { "mongo_changes", [] },
      }
    );

    TestData[] entityDataSeedData = [
      new TestData {
        Id = "68c0072634336093835452c4",
        Name = "test data 1",
      },
      new TestData {
        Id = "68c0072234336093835452c3",
        Name = "test data 2",
      },
    ];

    var startTs = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).UtcDateTime;
    await this._mongoDbFixtures.InsertFixtures(
      ["some coll"],
      new Dictionary<string, TestData[]>
      {
        { "some coll", entityDataSeedData },
      }
    );
    var endTs = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).UtcDateTime;

    await Task.Delay(5000);

    var streamMsgs = this._redisDb.StreamRange("mongo_changes");
    var actualMsgs = streamMsgs.Select(msg => msg.Values).ToArray();

    var msg1ChangeTime = DateTimeOffset.Parse((string)JsonConvert.DeserializeObject<dynamic>(actualMsgs[0][0].Value).ChangeTime).ToUniversalTime();
    var msg2ChangeTime = DateTimeOffset.Parse((string)JsonConvert.DeserializeObject<dynamic>(actualMsgs[1][0].Value).ChangeTime).ToUniversalTime();

    Assert.InRange(msg1ChangeTime, startTs, endTs);
    Assert.InRange(msg2ChangeTime, startTs, endTs);

    NameValueEntry[][] expectedMsgs = [
      [
        new NameValueEntry(
          "data",
          JsonConvert.SerializeObject(new {
            ChangeTime = msg2ChangeTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
            ChangeRecord = JsonConvert.SerializeObject(new {
              id = "68c0072634336093835452c4",
              changeType = new { Id = 1, Name = "insert" },
              document = new { _id = "68c0072634336093835452c4", name = "test data 1", description = (string?)null, deletedAt = (DateTime?)null },
            }),
            Source = JsonConvert.SerializeObject(new { dbName = "referenceData", collName = "some coll" }),
            NotifConfigs = (object?)null,
          })
        ),
        new NameValueEntry("retries", "0"),
      ],
      [
        new NameValueEntry(
          "data",
          JsonConvert.SerializeObject(new {
            ChangeTime = msg1ChangeTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
            ChangeRecord = JsonConvert.SerializeObject(new {
              id = "68c0072234336093835452c3",
              changeType = new { Id = 1, Name = "insert" },
              document = new { _id = "68c0072234336093835452c3", name = "test data 2", description = (string?)null, deletedAt = (DateTime?)null },
            }),
            Source = JsonConvert.SerializeObject(new { dbName = "referenceData", collName = "some coll" }),
            NotifConfigs = (object?)null,
          })
        ),
        new NameValueEntry("retries", "0"),
      ],
    ];

    Assert.Equal(
      JsonConvert.SerializeObject(expectedMsgs),
      JsonConvert.SerializeObject(actualMsgs)
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