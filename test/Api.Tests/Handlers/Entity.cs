using EntityModel = Api.Models.Entity;
using Api.Services;
using Moq;
using Api.Handlers;

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
  }

  public void Dispose()
  {
    this._dbClientMock.Reset();
  }

  [Fact]
  public async void Create_ItShouldCallInsertOneFromTheProvidedDbClientOnceWithTheExpectedArguments()
  {
    EntityModel testEntity = new EntityModel{Name = ""};

    await Entity.Create(this._dbClientMock.Object, testEntity);
    this._dbClientMock.Verify(m => m.InsertOne<EntityModel>("RefData", "Entities", testEntity), Times.Once());
  }

  [Fact]
  public async void Replace_ItShouldCallReplaceOneFromTheProvidedDbClientOnceWithTheExpectedArguments()
  {
    EntityModel testEntity = new EntityModel{ Id = "test id" };

    await Entity.Replace(this._dbClientMock.Object, testEntity);
    this._dbClientMock.Verify(m => m.ReplaceOne<EntityModel>("RefData", "Entities", testEntity, "test id"), Times.Once());
  }

  [Fact]
  public async void Replace_IfTheProvidedEntityDoesNotHaveAnId_ItShouldThrowAnException()
  {
    EntityModel testEntity = new EntityModel{};

    Exception e = await Assert.ThrowsAsync<Exception>(() => Entity.Replace(this._dbClientMock.Object, testEntity));
    Assert.Equal("Couldn't determine the Entity's ID.", e.Message);
  }

  [Fact]
  public async void Replace_IfTheProvidedEntityDoesNotHaveAnId_ItShouldNotCallReplaceOneFromTheProvidedDbClient()
  {
    EntityModel testEntity = new EntityModel{};

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
}