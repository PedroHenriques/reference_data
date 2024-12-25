using EntityModel = Api.Models.Entity;
using Api.Services;
using Moq;
using Api.Handlers;

namespace Api.Tests.Handlers;

public class EntityTests : IDisposable
{
  private Mock<IDb> dbClientMock;
  public EntityTests()
  {
    this.dbClientMock = new Mock<IDb>(MockBehavior.Strict);

    this.dbClientMock.Setup(s => s.InsertOne<EntityModel>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EntityModel>()));
  }

  public void Dispose()
  {
    this.dbClientMock.Reset();
  }

  [Fact]
  public void Create_ItShouldCallInsertOneFromTheProvidedDbClientOnceWithTheExpectedArguments()
  {
    EntityModel testEntity = new EntityModel{Name = ""};

    Entity.Create(this.dbClientMock.Object, testEntity);
    this.dbClientMock.Verify(m => m.InsertOne<EntityModel>("RefData", "Entities", testEntity), Times.Once());
  }
}