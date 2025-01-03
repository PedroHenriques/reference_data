using Api.Handlers;
using EntityModel = Api.Models.Entity;
using Api.Services;
using Api.Services.Types.Db;
using MongoDB.Bson;
using Moq;
using System.Dynamic;

namespace Api.Tests.Handlers;

public class EntityDataTests : IDisposable
{
  private readonly Mock<IDb> _dbClientMock;

  public EntityDataTests()
  {
    this._dbClientMock = new Mock<IDb>(MockBehavior.Strict);

    this._dbClientMock.Setup(s => s.Find<dynamic>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<BsonDocument>()))
      .Returns(Task.FromResult(new FindResult<dynamic> { Data = Array.Empty<dynamic>() }));
    this._dbClientMock.Setup(s => s.Find<EntityModel>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<BsonDocument>()))
      .Returns(Task.FromResult(new FindResult<EntityModel> { Metadata = new FindResultMetadata { TotalCount = 1 }, Data = new[] { new EntityModel { Name = "" } } }));
    this._dbClientMock.Setup(s => s.InsertOne<dynamic>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()))
      .Returns(Task.Delay(1));
    this._dbClientMock.Setup(s => s.ReplaceOne<dynamic>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>()))
      .Returns(Task.Delay(1));
    this._dbClientMock.Setup(s => s.DeleteOne<dynamic>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.Delay(1));
  }

  public void Dispose()
  {
    this._dbClientMock.Reset();
  }

  [Fact]
  public async void Create_ItShouldCallFindOfTheDbServiceToCheckIfTheRequestedEntityExistsAndIsActive()
  {
    ObjectId testDocId = ObjectId.GenerateNewId();

    await EntityData.Create(this._dbClientMock.Object, testDocId.ToString(), new ExpandoObject());
    this._dbClientMock.Verify(
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
        }
      ),
      Times.Once()
    );
  }

  [Fact]
  public async void Create_ItShouldCallInsertOneFromTheProvidedDbClientOnceWithTheExpectedArguments()
  {
    this._dbClientMock.Setup(s => s.Find<EntityModel>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<BsonDocument>()))
      .Returns(Task.FromResult(new FindResult<EntityModel> { Metadata = new FindResultMetadata { TotalCount = 1 }, Data = new[] { new EntityModel { Name = "rng entity name" } } }));

    ExpandoObject testDoc = new ExpandoObject();
    string testDocId = ObjectId.GenerateNewId().ToString();

    await EntityData.Create(this._dbClientMock.Object, testDocId, testDoc);
    this._dbClientMock.Verify(m => m.InsertOne<dynamic>("RefData", "rng entity name", testDoc), Times.Once());
  }

  [Fact]
  public async void Create_ItShouldReturnTheInsertedDocument()
  {
    string testDocId = ObjectId.GenerateNewId().ToString();
    dynamic testDoc = new ExpandoObject { };
    testDoc.prop1 = "prop1 value";
    testDoc.prop2 = false;

    var res = await EntityData.Create(this._dbClientMock.Object, testDocId, testDoc);
    Assert.Equal(testDoc, res);
  }

  [Fact]
  public async void Create_IfThereIsNoActiveEntityWithProvidedName_ItShouldNotCallInsertOneFromTheProvidedDbClient()
  {
    this._dbClientMock.Setup(s => s.Find<EntityModel>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<BsonDocument>()))
      .Returns(Task.FromResult(new FindResult<EntityModel> { Metadata = new FindResultMetadata { TotalCount = 0 } }));

    try
    {
      await EntityData.Create(this._dbClientMock.Object, "rng entity name", new ExpandoObject());
      Assert.Fail();
    }
    catch (System.Exception)
    {
      this._dbClientMock.Verify(m => m.InsertOne<dynamic>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()), Times.Never());
    }
  }

  [Fact]
  public async void Create_IfThereIsNoActiveEntityWithProvidedName_ItShouldThrowAnException()
  {
    this._dbClientMock.Setup(s => s.Find<EntityModel>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<BsonDocument>()))
      .Returns(Task.FromResult(new FindResult<EntityModel> { Metadata = new FindResultMetadata { TotalCount = 0 } }));

    string testDocId = ObjectId.GenerateNewId().ToString();

    Exception e = await Assert.ThrowsAsync<Exception>(() => EntityData.Create(this._dbClientMock.Object, testDocId, new ExpandoObject()));
    Assert.Equal($"No valid entity with the ID '{testDocId}' exists.", e.Message);
  }

  [Fact]
  public async void Replace_ItShouldCallFindOfTheDbServiceToCheckIfTheRequestedEntityExistsAndIsActive()
  {
    ObjectId testEntityId = ObjectId.GenerateNewId();
    ObjectId testDocId = ObjectId.GenerateNewId();

    await EntityData.Replace(this._dbClientMock.Object, testEntityId.ToString(), testDocId.ToString(), new ExpandoObject());
    this._dbClientMock.Verify(
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
        }
      ),
      Times.Once()
    );
  }

  [Fact]
  public async void Replace_ItShouldCallReplaceOneFromTheProvidedDbClientOnceWithTheExpectedArguments()
  {
    this._dbClientMock.Setup(s => s.Find<EntityModel>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<BsonDocument>()))
      .Returns(Task.FromResult(new FindResult<EntityModel> { Metadata = new FindResultMetadata { TotalCount = 1 }, Data = new[] { new EntityModel { Name = "test entity name" } } }));

    ObjectId testEntityId = ObjectId.GenerateNewId();
    ObjectId testDocId = ObjectId.GenerateNewId();
    ExpandoObject testDoc = new ExpandoObject();

    await EntityData.Replace(this._dbClientMock.Object, testEntityId.ToString(), testDocId.ToString(), testDoc);
    this._dbClientMock.Verify(m => m.ReplaceOne<dynamic>("RefData", "test entity name", testDoc, testDocId.ToString()), Times.Once());
  }

  [Fact]
  public async void Replace_ItShouldReturnTheInsertedDocument()
  {
    ObjectId testEntityId = ObjectId.GenerateNewId();
    ObjectId testDocId = ObjectId.GenerateNewId();
    dynamic testDoc = new ExpandoObject { };
    testDoc.prop1 = "prop1 value";
    testDoc.prop2 = false;

    var res = await EntityData.Replace(this._dbClientMock.Object, testEntityId.ToString(), testDocId.ToString(), testDoc);
    Assert.Equal(testDoc, res);
  }

  [Fact]
  public async void Replace_IfThereIsNoActiveEntityWithProvidedName_ItShouldNotCallInsertOneFromTheProvidedDbClient()
  {
    this._dbClientMock.Setup(s => s.Find<EntityModel>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<BsonDocument>()))
      .Returns(Task.FromResult(new FindResult<EntityModel> { Metadata = new FindResultMetadata { TotalCount = 0 } }));

    try
    {
      await EntityData.Replace(this._dbClientMock.Object, ObjectId.GenerateNewId().ToString(), ObjectId.GenerateNewId().ToString(), new ExpandoObject());
      Assert.Fail();
    }
    catch (System.Exception)
    {
      this._dbClientMock.Verify(m => m.InsertOne<dynamic>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()), Times.Never());
    }
  }

  [Fact]
  public async void Replace_IfThereIsNoActiveEntityWithProvidedName_ItShouldThrowAnException()
  {
    this._dbClientMock.Setup(s => s.Find<EntityModel>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<BsonDocument>()))
      .Returns(Task.FromResult(new FindResult<EntityModel> { Metadata = new FindResultMetadata { TotalCount = 0 } }));

    string testEntityId = ObjectId.GenerateNewId().ToString();

    Exception e = await Assert.ThrowsAsync<Exception>(() => EntityData.Replace(this._dbClientMock.Object, testEntityId, ObjectId.GenerateNewId().ToString(), new ExpandoObject()));
    Assert.Equal($"No valid entity with the ID '{testEntityId}' exists.", e.Message);
  }

  [Fact]
  public async void Delete_ItShouldCallFindOfTheDbServiceToCheckIfTheRequestedEntityExistsAndIsActive()
  {
    ObjectId testEntityId = ObjectId.GenerateNewId();
    ObjectId testDocId = ObjectId.GenerateNewId();

    await EntityData.Delete(this._dbClientMock.Object, testEntityId.ToString(), testDocId.ToString());
    this._dbClientMock.Verify(
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
        }
      ),
      Times.Once()
    );
  }

  [Fact]
  public async void Delete_ItShouldCallDeleteOneFromTheProvidedDbClientOnceWithTheExpectedArguments()
  {
    this._dbClientMock.Setup(s => s.Find<EntityModel>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<BsonDocument>()))
      .Returns(Task.FromResult(new FindResult<EntityModel> { Metadata = new FindResultMetadata { TotalCount = 1 }, Data = new[] { new EntityModel { Name = "rng test entity name" } } }));

    ObjectId testEntityId = ObjectId.GenerateNewId();
    ObjectId testDocId = ObjectId.GenerateNewId();

    await EntityData.Delete(this._dbClientMock.Object, testEntityId.ToString(), testDocId.ToString());
    this._dbClientMock.Verify(m => m.DeleteOne<EntityModel>("RefData", "rng test entity name", testDocId.ToString()), Times.Once());
  }

  [Fact]
  public async void Delete_IfThereIsNoActiveEntityWithProvidedName_ItShouldNotCallInsertOneFromTheProvidedDbClient()
  {
    this._dbClientMock.Setup(s => s.Find<EntityModel>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<BsonDocument>()))
      .Returns(Task.FromResult(new FindResult<EntityModel> { Metadata = new FindResultMetadata { TotalCount = 0 } }));

    try
    {
      await EntityData.Delete(this._dbClientMock.Object, ObjectId.GenerateNewId().ToString(), ObjectId.GenerateNewId().ToString());
      Assert.Fail();
    }
    catch (System.Exception)
    {
      this._dbClientMock.Verify(m => m.InsertOne<dynamic>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()), Times.Never());
    }
  }

  [Fact]
  public async void Delete_IfThereIsNoActiveEntityWithProvidedName_ItShouldThrowAnException()
  {
    this._dbClientMock.Setup(s => s.Find<EntityModel>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<BsonDocument>()))
      .Returns(Task.FromResult(new FindResult<EntityModel> { Metadata = new FindResultMetadata { TotalCount = 0 } }));

    string testEntityId = ObjectId.GenerateNewId().ToString();

    Exception e = await Assert.ThrowsAsync<Exception>(() => EntityData.Delete(this._dbClientMock.Object, testEntityId, ObjectId.GenerateNewId().ToString()));
    Assert.Equal($"No valid entity with the ID '{testEntityId}' exists.", e.Message);
  }

  [Fact]
  public async void Select_ItShouldCallFindOfTheDbServiceToCheckIfTheRequestedEntityExistsAndIsActive()
  {
    ObjectId testEntityId = ObjectId.GenerateNewId();
    ObjectId testDocId = ObjectId.GenerateNewId();

    await EntityData.Select(this._dbClientMock.Object, testEntityId.ToString(), 1, 1);
    this._dbClientMock.Verify(
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
        }
      ),
      Times.Once()
    );
  }

  [Fact]
  public async void Select_ItShouldCallFindFromTheProvidedDbClientOnceWithTheExpectedArguments()
  {
    this._dbClientMock.Setup(s => s.Find<EntityModel>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<BsonDocument>()))
      .Returns(Task.FromResult(new FindResult<EntityModel> { Metadata = new FindResultMetadata { TotalCount = 1 }, Data = new[] { new EntityModel { Name = "some test entity name" } } }));

    string testEntityId = ObjectId.GenerateNewId().ToString();

    await EntityData.Select(this._dbClientMock.Object, testEntityId, 123, 635);
    this._dbClientMock.Verify(m => m.Find<dynamic>("RefData", "some test entity name", 123, 635, null), Times.Once());
  }

  [Fact]
  public async void Select_ItShouldReturnTheResultOfCallingFindFromTheProvidedDbClient()
  {
    var expectedResult = new FindResult<dynamic> { Metadata = new FindResultMetadata { Page = 6 }, Data = Array.Empty<dynamic>() };
    this._dbClientMock.Setup(s => s.Find<dynamic>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<BsonDocument>()))
      .Returns(Task.FromResult(expectedResult));
    this._dbClientMock.Setup(s => s.Find<EntityModel>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<BsonDocument>()))
      .Returns(Task.FromResult(new FindResult<EntityModel> { Metadata = new FindResultMetadata { TotalCount = 1 }, Data = new[] { new EntityModel { Name = "" } } }));

    string testEntityId = ObjectId.GenerateNewId().ToString();

    Assert.Equal(expectedResult, await EntityData.Select(this._dbClientMock.Object, testEntityId, 73, 9410));
  }

  [Fact]
  public async void Select_IfThereIsNoActiveEntityWithProvidedName_ItShouldThrowAnException()
  {
    this._dbClientMock.Setup(s => s.Find<EntityModel>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<BsonDocument>()))
      .Returns(Task.FromResult(new FindResult<EntityModel> { Metadata = new FindResultMetadata { TotalCount = 0 } }));

    string testEntityId = ObjectId.GenerateNewId().ToString();

    Exception e = await Assert.ThrowsAsync<Exception>(() => EntityData.Select(this._dbClientMock.Object, testEntityId, 1, 1));
    Assert.Equal($"No valid entity with the ID '{testEntityId}' exists.", e.Message);
  }

}