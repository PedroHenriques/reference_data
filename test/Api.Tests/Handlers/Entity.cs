using EntityModel = Api.Models.Entity;
using Moq;
using Api.Handlers;
using MongoDB.Bson;
using SharedLibs;
using SharedLibs.Types.Db;

namespace Api.Tests.Handlers;

public class EntityTests : IDisposable
{
  private readonly Mock<IDb> _dbClientMock;

  public EntityTests()
  {
    this._dbClientMock = new Mock<IDb>(MockBehavior.Strict);

    this._dbClientMock.Setup(s => s.InsertOne<EntityModel>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EntityModel>()))
      .Returns(Task.Delay(1));
    this._dbClientMock.Setup(s => s.ReplaceOne<EntityModel>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EntityModel>(), It.IsAny<string>()))
      .Returns(Task.Delay(1));
    this._dbClientMock.Setup(s => s.DeleteOne<EntityModel>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.Delay(1));
    this._dbClientMock.Setup(s => s.Find<EntityModel>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<BsonDocument>(), It.IsAny<bool>()))
      .Returns(Task.FromResult(new FindResult<EntityModel> { }));
  }

  public void Dispose()
  {
    this._dbClientMock.Reset();
  }

  [Fact]
  public async void Create_ItShouldCallInsertOneFromTheProvidedDbClientOnceWithTheExpectedArguments()
  {
    EntityModel testEntity = new EntityModel { Name = "" };

    await Entity.Create(this._dbClientMock.Object, testEntity);
    this._dbClientMock.Verify(m => m.InsertOne<EntityModel>("RefData", "Entities", testEntity), Times.Once());
  }

  [Fact]
  public async void Replace_ItShouldCallReplaceOneFromTheProvidedDbClientOnceWithTheExpectedArguments()
  {
    EntityModel testEntity = new EntityModel { Id = "test id", Name = "" };

    await Entity.Replace(this._dbClientMock.Object, testEntity);
    this._dbClientMock.Verify(m => m.ReplaceOne<EntityModel>("RefData", "Entities", testEntity, "test id"), Times.Once());
  }

  [Fact]
  public async void Replace_IfTheProvidedEntityDoesNotHaveAnId_ItShouldThrowAnException()
  {
    EntityModel testEntity = new EntityModel { Name = "" };

    Exception e = await Assert.ThrowsAsync<Exception>(() => Entity.Replace(this._dbClientMock.Object, testEntity));
    Assert.Equal("Couldn't determine the Entity's ID.", e.Message);
  }

  [Fact]
  public async void Replace_IfTheProvidedEntityDoesNotHaveAnId_ItShouldNotCallReplaceOneFromTheProvidedDbClient()
  {
    EntityModel testEntity = new EntityModel { Name = "" };

    await Assert.ThrowsAsync<Exception>(() => Entity.Replace(this._dbClientMock.Object, testEntity));
    this._dbClientMock.Verify(m => m.ReplaceOne<EntityModel>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EntityModel>(), It.IsAny<string>()), Times.Never());
  }

  [Fact]
  public async void Delete_ItShouldCallDeleteOneFromTheProvidedDbClientOnceWithTheExpectedArguments()
  {
    string testId = "rng test id";

    await Entity.Delete(this._dbClientMock.Object, testId);
    this._dbClientMock.Verify(m => m.DeleteOne<EntityModel>("RefData", "Entities", testId), Times.Once());
  }

  [Fact]
  public async void Select_ItShouldCallFindFromTheProvidedDbClientOnceWithTheExpectedArguments()
  {
    await Entity.Select(this._dbClientMock.Object);
    this._dbClientMock.Verify(m => m.Find<EntityModel>("RefData", "Entities", 1, 50, null, false), Times.Once());
  }

  [Fact]
  public async void Select_ItShouldReturnTheResultOfCallingFindFromTheProvidedDbClient()
  {
    var expectedResult = new FindResult<EntityModel> { };
    this._dbClientMock.Setup(s => s.Find<EntityModel>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<BsonDocument>(), It.IsAny<bool>()))
      .Returns(Task.FromResult(expectedResult));

    Assert.Equal(expectedResult, await Entity.Select(this._dbClientMock.Object));
  }

  [Fact]
  public async void Select_IfAValueForThePageArgumentIsProvided_ItShouldCallFindFromTheProvidedDbClientOnceWithTheExpectedArguments()
  {
    int page = 456;

    await Entity.Select(this._dbClientMock.Object, page);
    this._dbClientMock.Verify(m => m.Find<EntityModel>("RefData", "Entities", page, 50, null, false), Times.Once());
  }

  [Fact]
  public async void Select_IfAValueForTheSizeArgumentIsProvided_ItShouldCallFindFromTheProvidedDbClientOnceWithTheExpectedArguments()
  {
    int size = 987;

    await Entity.Select(this._dbClientMock.Object, null, size);
    this._dbClientMock.Verify(m => m.Find<EntityModel>("RefData", "Entities", 1, size, null, false), Times.Once());
  }

  [Fact]
  public async void Select_IfAValueForTheIdArgumentIsProvided_ItShouldCallFindFromTheProvidedDbClientOnceWithTheExpectedArguments()
  {
    var testId = ObjectId.GenerateNewId();
    var expectedMatch = new BsonDocument{
      { "_id", testId },
    };

    await Entity.Select(this._dbClientMock.Object, 73, 9410, testId.ToString());
    this._dbClientMock.Verify(m => m.Find<EntityModel>("RefData", "Entities", 73, 9410, expectedMatch, false), Times.Once());
  }

  [Fact]
  public async void Select_IfAValueForTheMatchArgumentIsProvided_ItShouldCallFindFromTheProvidedDbClientOnceWithTheExpectedArguments()
  {
    var expectedMatch = new BsonDocument{
      { "hello", "world" },
      { "something", true },
    };
    await Entity.Select(this._dbClientMock.Object, 73, 9410, null, expectedMatch.ToString());
    this._dbClientMock.Verify(m => m.Find<EntityModel>("RefData", "Entities", 73, 9410, expectedMatch, false), Times.Once());
  }

  [Fact]
  public async void Select_IfAValueForTheIdAndTheMatchArgumentsAreProvided_ItShouldCallFindFromTheProvidedDbClientOnceWithTheExpectedArguments()
  {
    var testId = ObjectId.GenerateNewId();
    var testMatch = new BsonDocument{
      { "hello", "new world" },
      { "p1", 3243 },
    };
    var expectedMatch = new BsonDocument{
      { "$and", new BsonArray {
        new BsonDocument{ { "_id", testId } },
        testMatch,
      } },
    };

    await Entity.Select(this._dbClientMock.Object, 73, 9410, testId.ToString(), testMatch.ToString());
    this._dbClientMock.Verify(m => m.Find<EntityModel>("RefData", "Entities", 73, 9410, expectedMatch, false), Times.Once());
  }
}