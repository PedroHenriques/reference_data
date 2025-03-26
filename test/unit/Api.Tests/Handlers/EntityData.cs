using Api.Handlers;
using EntityModel = Api.Models.Entity;
using MongoDB.Bson;
using Moq;
using System.Dynamic;
using Toolkit.Types;

namespace Api.Tests.Handlers;

[Trait("Type", "Unit")]
public class EntityDataTests : IDisposable
{
  private readonly Mock<IMongodb> _mongodbMock;

  public EntityDataTests()
  {
    Environment.SetEnvironmentVariable("MONGO_CON_STR", "test db con str");
    Environment.SetEnvironmentVariable("MONGO_DB_NAME", "RefData");
    Environment.SetEnvironmentVariable("MONGO_COL_NAME", "Entities");

    this._mongodbMock = new Mock<IMongodb>(MockBehavior.Strict);

    this._mongodbMock.Setup(s => s.Find<dynamic>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<BsonDocument>(), It.IsAny<bool>()))
      .Returns(Task.FromResult(new FindResult<dynamic> { Data = Array.Empty<dynamic>() }));
    this._mongodbMock.Setup(s => s.Find<EntityModel>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<BsonDocument>(), It.IsAny<bool>()))
      .Returns(Task.FromResult(new FindResult<EntityModel> { Metadata = new FindResultMetadata { TotalCount = 1 }, Data = new[] { new EntityModel { Name = "" } } }));
    this._mongodbMock.Setup(s => s.InsertMany<dynamic>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object[]>()))
      .Returns(Task.Delay(1));
    this._mongodbMock.Setup(s => s.ReplaceOne<dynamic>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>()))
      .Returns(Task.Delay(1));
    this._mongodbMock.Setup(s => s.DeleteOne<dynamic>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.Delay(1));
  }

  public void Dispose()
  {
    Environment.SetEnvironmentVariable("MONGO_CON_STR", null);
    Environment.SetEnvironmentVariable("MONGO_DB_NAME", null);
    Environment.SetEnvironmentVariable("MONGO_COL_NAME", null);

    this._mongodbMock.Reset();
  }

  [Fact]
  public async void Create_ItShouldCallFindOfTheDbServiceToCheckIfTheRequestedEntityExistsAndIsActive()
  {
    ObjectId testDocId = ObjectId.GenerateNewId();
    object[] data = new[] { new ExpandoObject() };

    await EntityData.Create(this._mongodbMock.Object, testDocId.ToString(), data);
    this._mongodbMock.Verify(
      m => m.Find<EntityModel>(
        "RefData",
        "Entities",
        1,
        1,
        new BsonDocument {
          {
            "$and",
            new BsonArray {
              new BsonDocument { { "_id", testDocId } },
              new BsonDocument { { "deleted_at", BsonNull.Value } },
            }
          }
        },
        false
      ),
      Times.Once()
    );
  }

  [Fact]
  public async void Create_ItShouldCallInsertManyFromTheProvidedDbClientOnceWithTheExpectedArguments()
  {
    this._mongodbMock.Setup(s => s.Find<EntityModel>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<BsonDocument>(), It.IsAny<bool>()))
      .Returns(Task.FromResult(new FindResult<EntityModel> { Metadata = new FindResultMetadata { TotalCount = 1 }, Data = new[] { new EntityModel { Name = "rng entity name" } } }));

    object[] data = new[] { new ExpandoObject(), new ExpandoObject() };
    string testDocId = ObjectId.GenerateNewId().ToString();

    await EntityData.Create(this._mongodbMock.Object, testDocId, data);
    this._mongodbMock.Verify(m => m.InsertMany<dynamic>("RefData", "rng entity name", data), Times.Once());
  }

  [Fact]
  public async void Create_ItShouldReturnTheInsertedDocument()
  {
    string testDocId = ObjectId.GenerateNewId().ToString();
    dynamic testDoc1 = new ExpandoObject { };
    testDoc1.prop1 = "prop1 value";
    testDoc1.prop2 = false;
    dynamic testDoc2 = new ExpandoObject { };
    testDoc2.prop1 = "prop2 value";
    testDoc2.prop2 = true;
    object[] data = new[] { testDoc1, testDoc2 };

    var res = await EntityData.Create(this._mongodbMock.Object, testDocId, data);
    Assert.Equal(data, res);
  }

  [Fact]
  public async void Create_IfTheReceivedDataIsNotIterable_ItShouldThrowAnException()
  {
    string testDocId = ObjectId.GenerateNewId().ToString();

    var e = await Assert.ThrowsAsync<Exception>(() => EntityData.Create(this._mongodbMock.Object, testDocId, new ExpandoObject()));
    Assert.Equal("The body of the request must be an array of documents.", e.Message);
  }

  [Fact]
  public async void Create_IfThereIsNoActiveEntityWithProvidedName_ItShouldNotCallInsertOneFromTheProvidedDbClient()
  {
    this._mongodbMock.Setup(s => s.Find<EntityModel>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<BsonDocument>(), It.IsAny<bool>()))
      .Returns(Task.FromResult(new FindResult<EntityModel> { Metadata = new FindResultMetadata { TotalCount = 0 } }));

    try
    {
      object[] data = new[] { new ExpandoObject(), new ExpandoObject() };
      await EntityData.Create(this._mongodbMock.Object, "rng entity name", data);
      Assert.Fail();
    }
    catch (System.Exception)
    {
      this._mongodbMock.Verify(m => m.InsertOne<dynamic>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()), Times.Never());
    }
  }

  [Fact]
  public async void Create_IfThereIsNoActiveEntityWithProvidedName_ItShouldThrowAnException()
  {
    this._mongodbMock.Setup(s => s.Find<EntityModel>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<BsonDocument>(), It.IsAny<bool>()))
      .Returns(Task.FromResult(new FindResult<EntityModel> { Metadata = new FindResultMetadata { TotalCount = 0 } }));

    string testDocId = ObjectId.GenerateNewId().ToString();
    object[] data = new[] { new ExpandoObject(), new ExpandoObject() };

    Exception e = await Assert.ThrowsAsync<Exception>(() => EntityData.Create(this._mongodbMock.Object, testDocId, data));
    Assert.Equal($"No valid entity with the ID '{testDocId}' exists.", e.Message);
  }

  [Fact]
  public async void Replace_ItShouldCallFindOfTheDbServiceToCheckIfTheRequestedEntityExistsAndIsActive()
  {
    ObjectId testEntityId = ObjectId.GenerateNewId();
    ObjectId testDocId = ObjectId.GenerateNewId();

    await EntityData.Replace(this._mongodbMock.Object, testEntityId.ToString(), testDocId.ToString(), new ExpandoObject());
    this._mongodbMock.Verify(
      m => m.Find<EntityModel>(
        "RefData",
        "Entities",
        1,
        1,
        new BsonDocument {
          {
            "$and",
            new BsonArray {
              new BsonDocument { { "_id", testEntityId } },
              new BsonDocument { { "deleted_at", BsonNull.Value } },
            }
          }
        },
        false
      ),
      Times.Once()
    );
  }

  [Fact]
  public async void Replace_ItShouldCallReplaceOneFromTheProvidedDbClientOnceWithTheExpectedArguments()
  {
    this._mongodbMock.Setup(s => s.Find<EntityModel>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<BsonDocument>(), It.IsAny<bool>()))
      .Returns(Task.FromResult(new FindResult<EntityModel> { Metadata = new FindResultMetadata { TotalCount = 1 }, Data = new[] { new EntityModel { Name = "test entity name" } } }));

    ObjectId testEntityId = ObjectId.GenerateNewId();
    ObjectId testDocId = ObjectId.GenerateNewId();
    ExpandoObject testDoc = new ExpandoObject();

    await EntityData.Replace(this._mongodbMock.Object, testEntityId.ToString(), testDocId.ToString(), testDoc);
    this._mongodbMock.Verify(m => m.ReplaceOne<dynamic>("RefData", "test entity name", testDoc, testDocId.ToString()), Times.Once());
  }

  [Fact]
  public async void Replace_ItShouldReturnTheInsertedDocument()
  {
    ObjectId testEntityId = ObjectId.GenerateNewId();
    ObjectId testDocId = ObjectId.GenerateNewId();
    dynamic testDoc = new ExpandoObject { };
    testDoc.prop1 = "prop1 value";
    testDoc.prop2 = false;

    var res = await EntityData.Replace(this._mongodbMock.Object, testEntityId.ToString(), testDocId.ToString(), testDoc);
    Assert.Equal(testDoc, res);
  }

  [Fact]
  public async void Replace_IfThereIsNoActiveEntityWithProvidedName_ItShouldNotCallInsertOneFromTheProvidedDbClient()
  {
    this._mongodbMock.Setup(s => s.Find<EntityModel>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<BsonDocument>(), It.IsAny<bool>()))
      .Returns(Task.FromResult(new FindResult<EntityModel> { Metadata = new FindResultMetadata { TotalCount = 0 } }));

    try
    {
      await EntityData.Replace(this._mongodbMock.Object, ObjectId.GenerateNewId().ToString(), ObjectId.GenerateNewId().ToString(), new ExpandoObject());
      Assert.Fail();
    }
    catch (System.Exception)
    {
      this._mongodbMock.Verify(m => m.InsertOne<dynamic>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()), Times.Never());
    }
  }

  [Fact]
  public async void Replace_IfThereIsNoActiveEntityWithProvidedName_ItShouldThrowAnException()
  {
    this._mongodbMock.Setup(s => s.Find<EntityModel>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<BsonDocument>(), It.IsAny<bool>()))
      .Returns(Task.FromResult(new FindResult<EntityModel> { Metadata = new FindResultMetadata { TotalCount = 0 } }));

    string testEntityId = ObjectId.GenerateNewId().ToString();

    Exception e = await Assert.ThrowsAsync<Exception>(() => EntityData.Replace(this._mongodbMock.Object, testEntityId, ObjectId.GenerateNewId().ToString(), new ExpandoObject()));
    Assert.Equal($"No valid entity with the ID '{testEntityId}' exists.", e.Message);
  }

  [Fact]
  public async void Delete_ItShouldCallFindOfTheDbServiceToCheckIfTheRequestedEntityExistsAndIsActive()
  {
    ObjectId testEntityId = ObjectId.GenerateNewId();
    ObjectId testDocId = ObjectId.GenerateNewId();

    await EntityData.Delete(this._mongodbMock.Object, testEntityId.ToString(), testDocId.ToString());
    this._mongodbMock.Verify(
      m => m.Find<EntityModel>(
        "RefData",
        "Entities",
        1,
        1,
        new BsonDocument {
          {
            "$and",
            new BsonArray {
              new BsonDocument { { "_id", testEntityId } },
              new BsonDocument { { "deleted_at", BsonNull.Value } },
            }
          }
        },
        false
      ),
      Times.Once()
    );
  }

  [Fact]
  public async void Delete_ItShouldCallDeleteOneFromTheProvidedDbClientOnceWithTheExpectedArguments()
  {
    this._mongodbMock.Setup(s => s.Find<EntityModel>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<BsonDocument>(), It.IsAny<bool>()))
      .Returns(Task.FromResult(new FindResult<EntityModel> { Metadata = new FindResultMetadata { TotalCount = 1 }, Data = new[] { new EntityModel { Name = "rng test entity name" } } }));

    ObjectId testEntityId = ObjectId.GenerateNewId();
    ObjectId testDocId = ObjectId.GenerateNewId();

    await EntityData.Delete(this._mongodbMock.Object, testEntityId.ToString(), testDocId.ToString());
    this._mongodbMock.Verify(m => m.DeleteOne<EntityModel>("RefData", "rng test entity name", testDocId.ToString()), Times.Once());
  }

  [Fact]
  public async void Delete_IfThereIsNoActiveEntityWithProvidedName_ItShouldNotCallInsertOneFromTheProvidedDbClient()
  {
    this._mongodbMock.Setup(s => s.Find<EntityModel>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<BsonDocument>(), It.IsAny<bool>()))
      .Returns(Task.FromResult(new FindResult<EntityModel> { Metadata = new FindResultMetadata { TotalCount = 0 } }));

    try
    {
      await EntityData.Delete(this._mongodbMock.Object, ObjectId.GenerateNewId().ToString(), ObjectId.GenerateNewId().ToString());
      Assert.Fail();
    }
    catch (System.Exception)
    {
      this._mongodbMock.Verify(m => m.InsertOne<dynamic>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()), Times.Never());
    }
  }

  [Fact]
  public async void Delete_IfThereIsNoActiveEntityWithProvidedName_ItShouldThrowAnException()
  {
    this._mongodbMock.Setup(s => s.Find<EntityModel>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<BsonDocument>(), It.IsAny<bool>()))
      .Returns(Task.FromResult(new FindResult<EntityModel> { Metadata = new FindResultMetadata { TotalCount = 0 } }));

    string testEntityId = ObjectId.GenerateNewId().ToString();

    Exception e = await Assert.ThrowsAsync<Exception>(() => EntityData.Delete(this._mongodbMock.Object, testEntityId, ObjectId.GenerateNewId().ToString()));
    Assert.Equal($"No valid entity with the ID '{testEntityId}' exists.", e.Message);
  }

  [Fact]
  public async void Select_ItShouldCallFindOfTheDbServiceToCheckIfTheRequestedEntityExistsAndIsActive()
  {
    ObjectId testEntityId = ObjectId.GenerateNewId();

    await EntityData.Select(this._mongodbMock.Object, testEntityId.ToString());
    this._mongodbMock.Verify(
      m => m.Find<EntityModel>(
        "RefData",
        "Entities",
        1,
        1,
        new BsonDocument {
          {
            "$and",
            new BsonArray {
              new BsonDocument { { "_id", testEntityId } },
              new BsonDocument { { "deleted_at", BsonNull.Value } },
            }
          }
        },
        false
      ),
      Times.Once()
    );
  }

  [Fact]
  public async void Select_IfAValueForTheEntityNameArgumentIsProvided_ItShouldCallFindOfTheDbServiceToCheckIfTheRequestedEntityExistsAndIsActive()
  {
    string testEntityName = "some name";

    await EntityData.Select(this._mongodbMock.Object, null, testEntityName);
    this._mongodbMock.Verify(
      m => m.Find<EntityModel>(
        "RefData",
        "Entities",
        1,
        1,
        new BsonDocument {
          {
            "$and",
            new BsonArray {
              new BsonDocument { { "name", testEntityName } },
              new BsonDocument { { "deleted_at", BsonNull.Value } },
            }
          }
        },
        false
      ),
      Times.Once()
    );
  }

  [Fact]
  public async void Select_IfNeitherAValueForTheEntityIdNorTheNameAreProvided_ItShouldThrowAnException()
  {
    Exception e = await Assert.ThrowsAsync<Exception>(() => EntityData.Select(this._mongodbMock.Object));
    Assert.Equal("Neither an entity Id nor an entity name were provided.", e.Message);
  }

  [Fact]
  public async void Select_ItShouldCallFindFromTheProvidedDbClientOnceWithTheExpectedArguments()
  {
    this._mongodbMock.Setup(s => s.Find<EntityModel>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<BsonDocument>(), It.IsAny<bool>()))
      .Returns(Task.FromResult(new FindResult<EntityModel> { Metadata = new FindResultMetadata { TotalCount = 1 }, Data = new[] { new EntityModel { Name = "some test entity name" } } }));

    string testEntityId = ObjectId.GenerateNewId().ToString();

    await EntityData.Select(this._mongodbMock.Object, testEntityId);
    this._mongodbMock.Verify(m => m.Find<dynamic>("RefData", "some test entity name", 1, 50, null, false), Times.Once());
  }

  [Fact]
  public async void Select_IfAValueForTheDocIdArgumentIsProvided_ItShouldCallFindFromTheProvidedDbClientOnceWithTheExpectedArguments()
  {
    this._mongodbMock.Setup(s => s.Find<EntityModel>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<BsonDocument>(), It.IsAny<bool>()))
      .Returns(Task.FromResult(new FindResult<EntityModel> { Metadata = new FindResultMetadata { TotalCount = 1 }, Data = new[] { new EntityModel { Name = "some test entity name" } } }));

    string testEntityId = ObjectId.GenerateNewId().ToString();
    var testDocId = ObjectId.GenerateNewId();
    BsonDocument expectedMatch = new BsonDocument{
      { "_id", testDocId },
    };

    await EntityData.Select(this._mongodbMock.Object, testEntityId, null, testDocId.ToString());
    this._mongodbMock.Verify(m => m.Find<dynamic>("RefData", "some test entity name", 1, 50, expectedMatch, false), Times.Once());
  }

  [Fact]
  public async void Select_IfAValueForTheMatchArgumentIsProvided_ItShouldCallFindFromTheProvidedDbClientOnceWithTheExpectedArguments()
  {
    this._mongodbMock.Setup(s => s.Find<EntityModel>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<BsonDocument>(), It.IsAny<bool>()))
      .Returns(Task.FromResult(new FindResult<EntityModel> { Metadata = new FindResultMetadata { TotalCount = 1 }, Data = new[] { new EntityModel { Name = "some test entity name" } } }));

    string testEntityId = ObjectId.GenerateNewId().ToString();
    BsonDocument expectedMatch = new BsonDocument{
      { "some", "value" },
      { "prop 1", BsonNull.Value },
    };

    await EntityData.Select(this._mongodbMock.Object, testEntityId, null, null, null, null, expectedMatch.ToString());
    this._mongodbMock.Verify(m => m.Find<dynamic>("RefData", "some test entity name", 1, 50, expectedMatch, false), Times.Once());
  }

  [Fact]
  public async void Select_IfAValueForTheDocIdAndTheMatchArgumentsAreProvided_ItShouldCallFindFromTheProvidedDbClientOnceWithTheExpectedArguments()
  {
    this._mongodbMock.Setup(s => s.Find<EntityModel>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<BsonDocument>(), It.IsAny<bool>()))
      .Returns(Task.FromResult(new FindResult<EntityModel> { Metadata = new FindResultMetadata { TotalCount = 1 }, Data = new[] { new EntityModel { Name = "some test entity name" } } }));

    string testEntityId = ObjectId.GenerateNewId().ToString();
    var testDocId = ObjectId.GenerateNewId();
    BsonDocument testMatch = new BsonDocument{
      { "some", "value" },
      { "prop 1", BsonNull.Value },
    };
    var expectedMatch = new BsonDocument{
      { "$and", new BsonArray {
        new BsonDocument{ { "_id", testDocId } },
        testMatch
      } },
    };

    await EntityData.Select(this._mongodbMock.Object, testEntityId, null, testDocId.ToString(), null, null, testMatch.ToString());
    this._mongodbMock.Verify(m => m.Find<dynamic>("RefData", "some test entity name", 1, 50, expectedMatch, false), Times.Once());
  }

  [Fact]
  public async void Select_ItShouldReturnTheResultOfCallingFindFromTheProvidedDbClient()
  {
    var expectedResult = new FindResult<dynamic> { Metadata = new FindResultMetadata { Page = 6 }, Data = Array.Empty<dynamic>() };
    this._mongodbMock.Setup(s => s.Find<dynamic>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<BsonDocument>(), It.IsAny<bool>()))
      .Returns(Task.FromResult(expectedResult));
    this._mongodbMock.Setup(s => s.Find<EntityModel>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<BsonDocument>(), It.IsAny<bool>()))
      .Returns(Task.FromResult(new FindResult<EntityModel> { Metadata = new FindResultMetadata { TotalCount = 1 }, Data = new[] { new EntityModel { Name = "" } } }));

    string testEntityId = ObjectId.GenerateNewId().ToString();

    Assert.Equal(expectedResult, await EntityData.Select(this._mongodbMock.Object, testEntityId));
  }

  [Fact]
  public async void Select_IfThereIsNoActiveEntityWithProvidedId_ItShouldThrowAnException()
  {
    this._mongodbMock.Setup(s => s.Find<EntityModel>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<BsonDocument>(), It.IsAny<bool>()))
      .Returns(Task.FromResult(new FindResult<EntityModel> { Metadata = new FindResultMetadata { TotalCount = 0 } }));

    string testEntityId = ObjectId.GenerateNewId().ToString();

    Exception e = await Assert.ThrowsAsync<Exception>(() => EntityData.Select(this._mongodbMock.Object, testEntityId));
    Assert.Equal($"No valid entity with the ID '{testEntityId}' exists.", e.Message);
  }

  [Fact]
  public async void Select_IfThereIsNoActiveEntityWithProvidedName_ItShouldThrowAnException()
  {
    this._mongodbMock.Setup(s => s.Find<EntityModel>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<BsonDocument>(), It.IsAny<bool>()))
      .Returns(Task.FromResult(new FindResult<EntityModel> { Metadata = new FindResultMetadata { TotalCount = 0 } }));

    string testEntityName = "test name";

    Exception e = await Assert.ThrowsAsync<Exception>(() => EntityData.Select(this._mongodbMock.Object, null, testEntityName));
    Assert.Equal($"No valid entity with the NAME '{testEntityName}' exists.", e.Message);
  }
}