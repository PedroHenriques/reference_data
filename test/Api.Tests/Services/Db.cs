using Api.Models;
using Api.Services;
using MongoDB.Driver;
using Moq;

namespace Api.Tests.Services;

public class DbTests : IDisposable
{
  private Mock<IMongoClient> dbClientMock;
  private Mock<IMongoDatabase> dbDatabaseMock;
  private Mock<IMongoCollection<Entity>> dbCollectionMock;

  public DbTests()
  {
    this.dbClientMock = new Mock<IMongoClient>(MockBehavior.Strict);
    this.dbDatabaseMock = new Mock<IMongoDatabase>(MockBehavior.Strict);
    this.dbCollectionMock = new Mock<IMongoCollection<Entity>>(MockBehavior.Strict);

    this.dbClientMock.Setup(s => s.GetDatabase(It.IsAny<string>(), null))
      .Returns(this.dbDatabaseMock.Object);
    this.dbDatabaseMock.Setup(s => s.GetCollection<Entity>(It.IsAny<string>(), null))
      .Returns(this.dbCollectionMock.Object);
    this.dbCollectionMock.Setup(s => s.InsertOneAsync(It.IsAny<Entity>(), null, default))
      .Returns(Task.Delay(1));
  }

  public void Dispose()
  {
    this.dbClientMock.Reset();
    this.dbDatabaseMock.Reset();
  }

  [Fact]
  public void InsertOne_ItShouldCallGetDatabaseFromTheMongoClientOnceWithTheProvidedDbName()
  {
    IDb sut = new Db(this.dbClientMock.Object);

    sut.InsertOne<Entity>("test db name", "", new Entity { Name = "" });
    this.dbClientMock.Verify(m => m.GetDatabase("test db name", null), Times.Once());
  }

  [Fact]
  public void InsertOne_ItShouldCallGetCollectionFromTheMongoDatabaseOnceWithTheProvidedCollectionName()
  {
    IDb sut = new Db(this.dbClientMock.Object);

    sut.InsertOne<Entity>("", "test col name", new Entity { Name = "" });
    this.dbDatabaseMock.Verify(m => m.GetCollection<Entity>("test col name", null), Times.Once());
  }
  
  [Fact]
  public void InsertOne_ItShouldCallInsertOneAsyncFromTheMongoCollectionOnceWithTheProvidedDocument()
  {
    IDb sut = new Db(this.dbClientMock.Object);

    Entity testDoc = new Entity { Name = "" };
    sut.InsertOne<Entity>("", "", testDoc);
    this.dbCollectionMock.Verify(m => m.InsertOneAsync(testDoc, null, default), Times.Once());
  }
}