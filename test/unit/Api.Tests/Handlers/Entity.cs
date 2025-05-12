using EntityModel = Api.Models.Entity;
using Moq;
using Api.Handlers;
using MongoDB.Bson;
using Toolkit.Types;

namespace Api.Tests.Handlers;

[CollectionDefinition("EntityTests", DisableParallelization = true)]
[Trait("Type", "Unit")]
public class EntityTests : IDisposable
{
  private readonly Mock<IMongodb> _mongodbMock;

  public EntityTests()
  {
    Environment.SetEnvironmentVariable("MONGO_CON_STR", "test db con str");
    Environment.SetEnvironmentVariable("MONGO_DB_NAME", "RefData");
    Environment.SetEnvironmentVariable("MONGO_COL_NAME", "Entities");

    this._mongodbMock = new Mock<IMongodb>(MockBehavior.Strict);

    this._mongodbMock.Setup(s => s.InsertMany<EntityModel>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EntityModel[]>()))
      .Returns(Task.Delay(1));
    this._mongodbMock.Setup(s => s.ReplaceOne<EntityModel>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EntityModel>(), It.IsAny<string>()))
      .Returns(Task.Delay(1));
    this._mongodbMock.Setup(s => s.DeleteOne<EntityModel>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.Delay(1));
    this._mongodbMock.Setup(s => s.Find<EntityModel>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<BsonDocument>(), It.IsAny<bool>()))
      .Returns(Task.FromResult(new FindResult<EntityModel> { }));
  }

  public void Dispose()
  {
    Environment.SetEnvironmentVariable("MONGO_CON_STR", null);
    Environment.SetEnvironmentVariable("MONGO_DB_NAME", null);
    Environment.SetEnvironmentVariable("MONGO_COL_NAME", null);

    this._mongodbMock.Reset();
  }

  [Fact]
  public async Task Create_ItShouldCallInsertManyFromTheProvidedDbClientOnceWithTheExpectedArguments()
  {
    EntityModel[] testEntities = new EntityModel[] {
      new EntityModel { Name = "" },
      new EntityModel { Name = "" },
    };

    await Entity.Create(this._mongodbMock.Object, testEntities);
    this._mongodbMock.Verify(m => m.InsertMany<EntityModel>("RefData", "Entities", testEntities), Times.Once());
  }

  [Fact]
  public async Task Replace_ItShouldCallReplaceOneFromTheProvidedDbClientOnceWithTheExpectedArguments()
  {
    EntityModel testEntity = new EntityModel { Id = "test id", Name = "" };

    await Entity.Replace(this._mongodbMock.Object, testEntity);
    this._mongodbMock.Verify(m => m.ReplaceOne<EntityModel>("RefData", "Entities", testEntity, "test id"), Times.Once());
  }

  [Fact]
  public async Task Replace_IfTheProvidedEntityDoesNotHaveAnId_ItShouldThrowAnException()
  {
    EntityModel testEntity = new EntityModel { Name = "" };

    Exception e = await Assert.ThrowsAsync<Exception>(() => Entity.Replace(this._mongodbMock.Object, testEntity));
    Assert.Equal("Couldn't determine the Entity's ID.", e.Message);
  }

  [Fact]
  public async Task Replace_IfTheProvidedEntityDoesNotHaveAnId_ItShouldNotCallReplaceOneFromTheProvidedDbClient()
  {
    EntityModel testEntity = new EntityModel { Name = "" };

    await Assert.ThrowsAsync<Exception>(() => Entity.Replace(this._mongodbMock.Object, testEntity));
    this._mongodbMock.Verify(m => m.ReplaceOne<EntityModel>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EntityModel>(), It.IsAny<string>()), Times.Never());
  }

  [Fact]
  public async Task Delete_ItShouldCallDeleteOneFromTheProvidedDbClientOnceWithTheExpectedArguments()
  {
    string testId = "rng test id";

    await Entity.Delete(this._mongodbMock.Object, testId);
    this._mongodbMock.Verify(m => m.DeleteOne<EntityModel>("RefData", "Entities", testId), Times.Once());
  }

  [Fact]
  public async Task Select_ItShouldCallFindFromTheProvidedDbClientOnceWithTheExpectedArguments()
  {
    await Entity.Select(this._mongodbMock.Object);
    this._mongodbMock.Verify(m => m.Find<EntityModel>("RefData", "Entities", 1, 50, null, false), Times.Once());
  }

  [Fact]
  public async Task Select_ItShouldReturnTheResultOfCallingFindFromTheProvidedDbClient()
  {
    var expectedResult = new FindResult<EntityModel> { };
    this._mongodbMock.Setup(s => s.Find<EntityModel>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<BsonDocument>(), It.IsAny<bool>()))
      .Returns(Task.FromResult(expectedResult));

    Assert.Equal(expectedResult, await Entity.Select(this._mongodbMock.Object));
  }

  [Fact]
  public async Task Select_IfAValueForThePageArgumentIsProvided_ItShouldCallFindFromTheProvidedDbClientOnceWithTheExpectedArguments()
  {
    int page = 456;

    await Entity.Select(this._mongodbMock.Object, page);
    this._mongodbMock.Verify(m => m.Find<EntityModel>("RefData", "Entities", page, 50, null, false), Times.Once());
  }

  [Fact]
  public async Task Select_IfAValueForTheSizeArgumentIsProvided_ItShouldCallFindFromTheProvidedDbClientOnceWithTheExpectedArguments()
  {
    int size = 987;

    await Entity.Select(this._mongodbMock.Object, null, size);
    this._mongodbMock.Verify(m => m.Find<EntityModel>("RefData", "Entities", 1, size, null, false), Times.Once());
  }

  [Fact]
  public async Task Select_IfAValueForTheIdArgumentIsProvided_ItShouldCallFindFromTheProvidedDbClientOnceWithTheExpectedArguments()
  {
    var testId = ObjectId.GenerateNewId();
    var expectedMatch = new BsonDocument{
      { "_id", testId },
    };

    await Entity.Select(this._mongodbMock.Object, 73, 9410, testId.ToString());
    this._mongodbMock.Verify(m => m.Find<EntityModel>("RefData", "Entities", 73, 9410, expectedMatch, false), Times.Once());
  }

  [Fact]
  public async Task Select_IfAValueForTheMatchArgumentIsProvided_ItShouldCallFindFromTheProvidedDbClientOnceWithTheExpectedArguments()
  {
    var expectedMatch = new BsonDocument{
      { "hello", "world" },
      { "something", true },
    };
    await Entity.Select(this._mongodbMock.Object, 73, 9410, null, expectedMatch.ToString());
    this._mongodbMock.Verify(m => m.Find<EntityModel>("RefData", "Entities", 73, 9410, expectedMatch, false), Times.Once());
  }

  [Fact]
  public async Task Select_IfAValueForTheIdAndTheMatchArgumentsAreProvided_ItShouldCallFindFromTheProvidedDbClientOnceWithTheExpectedArguments()
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

    await Entity.Select(this._mongodbMock.Object, 73, 9410, testId.ToString(), testMatch.ToString());
    this._mongodbMock.Verify(m => m.Find<EntityModel>("RefData", "Entities", 73, 9410, expectedMatch, false), Times.Once());
  }
}