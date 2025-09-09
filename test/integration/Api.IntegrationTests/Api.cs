using System.Text;
using System.Text.Json.Serialization;
using DbFixtures.Mongodb;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Newtonsoft.Json;

namespace Api.Tests.Integration;

[Trait("Type", "Integration")]
public class ApiTests : IDisposable
{
  private const string DB_NAME = "referenceData";
  private const string ENTITIES_COLL_NAME = "entities";
  private readonly HttpClient _httpClient;
  private readonly IMongoClient _client;
  private readonly DbFixtures.DbFixtures _dbFixtures;

  public ApiTests()
  {
    this._httpClient = new HttpClient();

    string connStr = "mongodb://admin:pw@api_db:27017/admin?authMechanism=SCRAM-SHA-256&replicaSet=rs0";
    this._client = new MongoClient(connStr);
    var driver = new MongodbDriver(this._client, DB_NAME);
    this._dbFixtures = new DbFixtures.DbFixtures([driver]);
  }

  public void Dispose()
  {
    this._dbFixtures.CloseDrivers();
    this._httpClient.Dispose();
  }

  [Fact]
  public async Task Entities_Post_ItShouldInsertEntitiesInDbAndReturnInsertedDocuments()
  {
    Entity[] seedData = [
      new Entity {
        Id = ObjectId.GenerateNewId().ToString(),
        Name = "doc 1",
      },
      new Entity {
        Name = "doc 2",
      },
    ];

    await this._dbFixtures.InsertFixtures(
      [ENTITIES_COLL_NAME],
      new Dictionary<string, Entity[]>
      {
        { ENTITIES_COLL_NAME, seedData }
      }
    );

    Entity[] data = [
      new Entity {
        Id = ObjectId.GenerateNewId().ToString(),
        Name = "doc 3",
      },
      new Entity {
        Name = "doc 4",
        Desc = "doc 4 desc",
        NotifConfigs = [
          new NotifConfig {
            Protocol = "webhook",
            TargetURL = "some url",
          },
        ],
      },
    ];
    HttpContent content = new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json");

    var result = await this._httpClient.PostAsync("http://api:10000/v1/entities/", content);
    string body = await result.Content.ReadAsStringAsync();

    Assert.True(result.IsSuccessStatusCode, body);

    var docs = await this._client.GetDatabase(DB_NAME).GetCollection<Entity>(ENTITIES_COLL_NAME)
      .Find<Entity>(FilterDefinition<Entity>.Empty).ToListAsync();

    Assert.Equal(4, docs.Count);
    Assert.Equal(JsonConvert.SerializeObject(seedData[0]), JsonConvert.SerializeObject(docs[0]));
    seedData[1].Id = docs[1].Id;
    Assert.Equal(JsonConvert.SerializeObject(seedData[1]), JsonConvert.SerializeObject(docs[1]));
    Assert.Equal(JsonConvert.SerializeObject(data[0]), JsonConvert.SerializeObject(docs[2]));
    data[1].Id = docs[3].Id;
    Assert.Equal(JsonConvert.SerializeObject(data[1]), JsonConvert.SerializeObject(docs[3]));

    Assert.Equal(JsonConvert.SerializeObject(data), body);
  }

  [Fact]
  public async Task Entities_Put_ItShouldReplaceTheProvidedEntityInTheDbAndReturnTheUpdatedDocument()
  {
    Entity[] seedData = [
      new Entity {
        Id = ObjectId.GenerateNewId().ToString(),
        Name = "doc 1",
      },
      new Entity {
        Id = ObjectId.GenerateNewId().ToString(),
        Name = "doc 2",
      },
    ];

    await this._dbFixtures.InsertFixtures(
      [ENTITIES_COLL_NAME],
      new Dictionary<string, Entity[]>
      {
        { ENTITIES_COLL_NAME, seedData }
      }
    );

    seedData[0].Desc = "doc 1 dec";
    seedData[0].NotifConfigs = [
      new NotifConfig {
        Protocol = "event",
        TargetURL = "some topic name",
      },
    ];
    var docStr = JsonConvert.SerializeObject(seedData[0]);

    HttpContent content = new StringContent(docStr, Encoding.UTF8, "application/json");

    var result = await this._httpClient.PutAsync($"http://api:10000/v1/entities/{seedData[0].Id}", content);
    string body = await result.Content.ReadAsStringAsync();

    Assert.True(result.IsSuccessStatusCode, body);

    var docs = await this._client.GetDatabase(DB_NAME).GetCollection<Entity>(ENTITIES_COLL_NAME)
      .Find<Entity>(FilterDefinition<Entity>.Empty).ToListAsync();

    Assert.Equal(2, docs.Count);
    Assert.Equal(docStr, JsonConvert.SerializeObject(docs[0]));
    Assert.Equal(JsonConvert.SerializeObject(seedData[1]), JsonConvert.SerializeObject(docs[1]));

    Assert.Equal(docStr, body);
  }

  [Fact]
  public async Task Entities_Delete_ItShouldSoftDeleteTheProvidedEntityInTheDbAndReturnASuccessResponse()
  {
    Entity[] seedData = [
      new Entity {
        Id = ObjectId.GenerateNewId().ToString(),
        Name = "doc 1",
      },
      new Entity {
        Id = ObjectId.GenerateNewId().ToString(),
        Name = "doc 2",
      },
    ];

    await this._dbFixtures.InsertFixtures(
      [ENTITIES_COLL_NAME],
      new Dictionary<string, Entity[]>
      {
        { ENTITIES_COLL_NAME, seedData }
      }
    );

    var startTs = DateTime.Now;
    var result = await this._httpClient.DeleteAsync($"http://api:10000/v1/entities/{seedData[0].Id}");
    var endTs = DateTime.Now;

    Assert.True(result.IsSuccessStatusCode);

    var docs = await this._client.GetDatabase(DB_NAME).GetCollection<Entity>(ENTITIES_COLL_NAME)
      .Find<Entity>(FilterDefinition<Entity>.Empty).ToListAsync();

    seedData[0].DeletedAt = docs[0].DeletedAt;
    var docStr = JsonConvert.SerializeObject(seedData[0]);

    Assert.Equal(2, docs.Count);
    Assert.NotNull(docs[0].DeletedAt);
    Assert.InRange<DateTime>((DateTime)(docs[0].DeletedAt), startTs, endTs);
    Assert.Equal(docStr, JsonConvert.SerializeObject(docs[0]));
    Assert.Equal(JsonConvert.SerializeObject(seedData[1]), JsonConvert.SerializeObject(docs[1]));
  }

  [Fact]
  public async Task Entities_GetAll_ItShouldQueryTheEntitiesCollectionInTheDbAndReturnAllRelevantDocumentsPaginated()
  {
    Entity[] seedData = [
      new Entity {
        Id = "68bd8e86765fc5112cd9f2e2",
        Name = "doc 1",
      },
      new Entity {
        Id = "68bd8e86765fc5112cd9f2e3",
        Name = "doc 2",
      },
    ];

    await this._dbFixtures.InsertFixtures(
      [ENTITIES_COLL_NAME],
      new Dictionary<string, Entity[]>
      {
        { ENTITIES_COLL_NAME, seedData }
      }
    );

    var result = await this._httpClient.GetAsync("http://api:10000/v1/entities");
    string body = await result.Content.ReadAsStringAsync();

    Assert.True(result.IsSuccessStatusCode);
    Assert.Equal(
      """{"metadata":{"totalCount":2,"page":1,"pageSize":50,"totalPages":1},"data":[{"id":"68bd8e86765fc5112cd9f2e2","name":"doc 1","description":null,"deletedAt":null,"notifConfigs":null},{"id":"68bd8e86765fc5112cd9f2e3","name":"doc 2","description":null,"deletedAt":null,"notifConfigs":null}]}""",
      body
    );
  }

  [Fact]
  public async Task Entities_GetById_ItShouldQueryTheEntitiesCollectionInTheDbAndReturnTheRequestedDocument()
  {
    Entity[] seedData = [
      new Entity {
        Id = "68bd8e86765fc5112cd9f2e2",
        Name = "doc 1",
      },
      new Entity {
        Id = "68bd8e86765fc5112cd9f2e3",
        Name = "doc 2",
      },
    ];

    await this._dbFixtures.InsertFixtures(
      [ENTITIES_COLL_NAME],
      new Dictionary<string, Entity[]>
      {
        { ENTITIES_COLL_NAME, seedData }
      }
    );

    var result = await this._httpClient.GetAsync($"http://api:10000/v1/entities/{seedData[1].Id}");
    string body = await result.Content.ReadAsStringAsync();

    Assert.True(result.IsSuccessStatusCode);
    Assert.Equal(
      """{"metadata":{"totalCount":1,"page":1,"pageSize":50,"totalPages":1},"data":[{"id":"68bd8e86765fc5112cd9f2e3","name":"doc 2","description":null,"deletedAt":null,"notifConfigs":null}]}""",
      body
    );
  }

  [Fact]
  public async Task EntityData_Post_ItShouldInsertEntityDataInDbAndReturnInsertedDocuments()
  {
    Entity[] entitiesSeedData = [
      new Entity {
        Id = ObjectId.GenerateNewId().ToString(),
        Name = "doc 1",
      },
    ];

    TestData[] entityDataSeedData = [
      new TestData {
        Name = "test data 1",
      },
      new TestData {
        Id = ObjectId.GenerateNewId().ToString(),
        Name = "test data 2",
      },
    ];

    await this._dbFixtures.InsertFixtures(
      [ENTITIES_COLL_NAME],
      new Dictionary<string, Entity[]>
      {
        { ENTITIES_COLL_NAME, entitiesSeedData },
      }
    );
    await this._dbFixtures.InsertFixtures(
      ["doc 1"],
      new Dictionary<string, TestData[]>
      {
        { "doc 1", entityDataSeedData },
      }
    );

    TestData[] data = [
      new TestData {
        Name = "test data 3",
      },
      new TestData {
        Name = "test data 4",
      },
    ];
    HttpContent content = new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json");

    var result = await this._httpClient.PostAsync($"http://api:10000/v1/data/{entitiesSeedData[0].Id}", content);
    string body = await result.Content.ReadAsStringAsync();

    Assert.True(result.IsSuccessStatusCode, body);

    var docs = await this._client.GetDatabase(DB_NAME).GetCollection<TestData>(entitiesSeedData[0].Name)
      .Find<TestData>(FilterDefinition<TestData>.Empty).ToListAsync();

    Assert.Equal(4, docs.Count);
    entityDataSeedData[0].Id = docs[0].Id;
    Assert.Equal(JsonConvert.SerializeObject(entityDataSeedData[0]), JsonConvert.SerializeObject(docs[0]));
    Assert.Equal(JsonConvert.SerializeObject(entityDataSeedData[1]), JsonConvert.SerializeObject(docs[1]));
    data[0].Id = docs[2].Id;
    Assert.Equal(JsonConvert.SerializeObject(data[0]), JsonConvert.SerializeObject(docs[2]));
    data[1].Id = docs[3].Id;
    Assert.Equal(JsonConvert.SerializeObject(data[1]), JsonConvert.SerializeObject(docs[3]));

    // I'm deserializing and then serializing the body to make sure the properties are in the same order as in data, since the API is returning
    // them in swapped order
    Assert.Equal(JsonConvert.SerializeObject(data), JsonConvert.SerializeObject(JsonConvert.DeserializeObject<TestData[]>(body)));
  }

  [Fact]
  public async Task EntityData_Put_ItShouldReplaceTheProvidedEntityDataInTheDbAndReturnTheUpdatedDocument()
  {
    Entity[] seedData = [
      new Entity {
        Id = ObjectId.GenerateNewId().ToString(),
        Name = "doc 1",
      },
    ];

    TestData[] entityDataSeedData = [
      new TestData {
        Name = "test data 1",
      },
      new TestData {
        Id = ObjectId.GenerateNewId().ToString(),
        Name = "test data 2",
      },
    ];

    await this._dbFixtures.InsertFixtures(
      [ENTITIES_COLL_NAME],
      new Dictionary<string, Entity[]>
      {
        { ENTITIES_COLL_NAME, seedData }
      }
    );
    await this._dbFixtures.InsertFixtures(
      ["doc 1"],
      new Dictionary<string, TestData[]>
      {
        { "doc 1", entityDataSeedData },
      }
    );

    var docId = entityDataSeedData[1].Id;
    entityDataSeedData[1].Id = null;
    entityDataSeedData[1].Desc = "test data 2 dec";

    HttpContent content = new StringContent(JsonConvert.SerializeObject(entityDataSeedData[1]), Encoding.UTF8, "application/json");

    var result = await this._httpClient.PutAsync($"http://api:10000/v1/data/{seedData[0].Id}/{docId}", content);
    string body = await result.Content.ReadAsStringAsync();

    Assert.True(result.IsSuccessStatusCode, body);

    var docs = await this._client.GetDatabase(DB_NAME).GetCollection<TestData>("doc 1")
      .Find<TestData>(FilterDefinition<TestData>.Empty).ToListAsync();

    Assert.Equal(2, docs.Count);
    entityDataSeedData[0].Id = docs[0].Id;
    Assert.Equal(JsonConvert.SerializeObject(entityDataSeedData[0]), JsonConvert.SerializeObject(docs[0]));
    entityDataSeedData[1].Id = docId;
    var docStr = JsonConvert.SerializeObject(entityDataSeedData[1]);
    Assert.Equal(docStr, JsonConvert.SerializeObject(docs[1]));

    // I'm deserializing and then serializing the body to make sure the properties are in the same order as in data, since the API is returning
    // them in swapped order
    Assert.Equal(docStr, JsonConvert.SerializeObject(JsonConvert.DeserializeObject<TestData>(body)));
  }

  [Fact]
  public async Task EntityData_Delete_ItShouldSoftDeleteTheProvidedEntityDataInTheDbAndReturnASuccessResponse()
  {
    Entity[] seedData = [
      new Entity {
        Id = ObjectId.GenerateNewId().ToString(),
        Name = "doc 1",
      },
    ];

    TestData[] entityDataSeedData = [
      new TestData {
        Name = "test data 1",
      },
      new TestData {
        Id = ObjectId.GenerateNewId().ToString(),
        Name = "test data 2",
      },
    ];

    await this._dbFixtures.InsertFixtures(
      [ENTITIES_COLL_NAME],
      new Dictionary<string, Entity[]>
      {
        { ENTITIES_COLL_NAME, seedData }
      }
    );
    await this._dbFixtures.InsertFixtures(
      ["doc 1"],
      new Dictionary<string, TestData[]>
      {
        { "doc 1", entityDataSeedData },
      }
    );

    var startTs = DateTime.Now;
    var result = await this._httpClient.DeleteAsync($"http://api:10000/v1/data/{seedData[0].Id}/{entityDataSeedData[1].Id}");
    var endTs = DateTime.Now;

    Assert.True(result.IsSuccessStatusCode);

    var docs = await this._client.GetDatabase(DB_NAME).GetCollection<TestData>("doc 1")
      .Find<TestData>(FilterDefinition<TestData>.Empty).ToListAsync();

    entityDataSeedData[1].DeletedAt = docs[1].DeletedAt;
    var docStr = JsonConvert.SerializeObject(entityDataSeedData[1]);

    Assert.Equal(2, docs.Count);
    Assert.Equal(JsonConvert.SerializeObject(entityDataSeedData[0]), JsonConvert.SerializeObject(docs[0]));
    Assert.NotNull(docs[1].DeletedAt);
    Assert.InRange<DateTime>((DateTime)(docs[1].DeletedAt), startTs, endTs);
    Assert.Equal(docStr, JsonConvert.SerializeObject(docs[1]));
  }

  [Fact]
  public async Task EntityData_GetAll_WithEntityId_ItShouldQueryTheEntitiesCollectionInTheDbAndReturnAllRelevantDocumentsPaginated()
  {
    Entity[] seedData = [
      new Entity {
        Id = "68bd8e86765fc5112cd9f2e2",
        Name = "doc 1",
      },
    ];

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

    await this._dbFixtures.InsertFixtures(
      [ENTITIES_COLL_NAME],
      new Dictionary<string, Entity[]>
      {
        { ENTITIES_COLL_NAME, seedData }
      }
    );
    await this._dbFixtures.InsertFixtures(
      ["doc 1"],
      new Dictionary<string, TestData[]>
      {
        { "doc 1", entityDataSeedData },
      }
    );

    var result = await this._httpClient.GetAsync($"http://api:10000/v1/data/{seedData[0].Id}");
    string body = await result.Content.ReadAsStringAsync();

    Assert.True(result.IsSuccessStatusCode);
    Assert.Equal(
      """{"metadata":{"totalCount":2,"page":1,"pageSize":50,"totalPages":1},"data":[{"name":"test data 2","description":null,"deletedAt":null,"id":"68c0072234336093835452c3"},{"name":"test data 1","description":null,"deletedAt":null,"id":"68c0072634336093835452c4"}]}""",
      body
    );
  }

  [Fact]
  public async Task EntityData_GetById_WithEntityId_ItShouldQueryTheEntitiesCollectionInTheDbAndReturnTheRequestedDocument()
  {
    Entity[] seedData = [
      new Entity {
        Id = "68bd8e86765fc5112cd9f2e2",
        Name = "doc 1",
      },
    ];

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

    await this._dbFixtures.InsertFixtures(
      [ENTITIES_COLL_NAME],
      new Dictionary<string, Entity[]>
      {
        { ENTITIES_COLL_NAME, seedData }
      }
    );
    await this._dbFixtures.InsertFixtures(
      ["doc 1"],
      new Dictionary<string, TestData[]>
      {
        { "doc 1", entityDataSeedData },
      }
    );

    var result = await this._httpClient.GetAsync($"http://api:10000/v1/data/{seedData[0].Id}/{entityDataSeedData[0].Id}");
    string body = await result.Content.ReadAsStringAsync();

    Assert.True(result.IsSuccessStatusCode);
    Assert.Equal(
      """{"metadata":{"totalCount":1,"page":1,"pageSize":50,"totalPages":1},"data":[{"name":"test data 1","description":null,"deletedAt":null,"id":"68c0072634336093835452c4"}]}""",
      body
    );
  }

  [Fact]
  public async Task EntityData_GetAll_WithEntityName_ItShouldQueryTheEntitiesCollectionInTheDbAndReturnAllRelevantDocumentsPaginated()
  {
    Entity[] seedData = [
      new Entity {
        Id = "68bd8e86765fc5112cd9f2e2",
        Name = "doc 1",
      },
    ];

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

    await this._dbFixtures.InsertFixtures(
      [ENTITIES_COLL_NAME],
      new Dictionary<string, Entity[]>
      {
        { ENTITIES_COLL_NAME, seedData }
      }
    );
    await this._dbFixtures.InsertFixtures(
      ["doc 1"],
      new Dictionary<string, TestData[]>
      {
        { "doc 1", entityDataSeedData },
      }
    );

    var result = await this._httpClient.GetAsync($"http://api:10000/v1/data/name/{seedData[0].Name}");
    string body = await result.Content.ReadAsStringAsync();

    Assert.True(result.IsSuccessStatusCode);
    Assert.Equal(
      """{"metadata":{"totalCount":2,"page":1,"pageSize":50,"totalPages":1},"data":[{"name":"test data 2","description":null,"deletedAt":null,"id":"68c0072234336093835452c3"},{"name":"test data 1","description":null,"deletedAt":null,"id":"68c0072634336093835452c4"}]}""",
      body
    );
  }

  [Fact]
  public async Task EntityData_GetById_WithEntityName_ItShouldQueryTheEntitiesCollectionInTheDbAndReturnTheRequestedDocument()
  {
    Entity[] seedData = [
      new Entity {
        Id = "68bd8e86765fc5112cd9f2e2",
        Name = "doc 1",
      },
    ];

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

    await this._dbFixtures.InsertFixtures(
      [ENTITIES_COLL_NAME],
      new Dictionary<string, Entity[]>
      {
        { ENTITIES_COLL_NAME, seedData }
      }
    );
    await this._dbFixtures.InsertFixtures(
      ["doc 1"],
      new Dictionary<string, TestData[]>
      {
        { "doc 1", entityDataSeedData },
      }
    );

    var result = await this._httpClient.GetAsync($"http://api:10000/v1/data/name/{seedData[0].Name}/{entityDataSeedData[0].Id}");
    string body = await result.Content.ReadAsStringAsync();

    Assert.True(result.IsSuccessStatusCode);
    Assert.Equal(
      """{"metadata":{"totalCount":1,"page":1,"pageSize":50,"totalPages":1},"data":[{"name":"test data 1","description":null,"deletedAt":null,"id":"68c0072634336093835452c4"}]}""",
      body
    );
  }
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