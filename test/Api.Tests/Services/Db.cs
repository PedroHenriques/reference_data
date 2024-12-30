using Api.Models;
using Api.Services;
using MongoDB.Bson;
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
    this.dbCollectionMock.Setup(s => s.ReplaceOneAsync(It.IsAny<BsonDocumentFilterDefinition<Entity>>(), It.IsAny<Entity>(), null as ReplaceOptions, default))
      .Returns(Task.FromResult(new ReplaceOneResult.Acknowledged(1, 1, null) as ReplaceOneResult));
    this.dbCollectionMock.Setup(s => s.UpdateOneAsync(It.IsAny<BsonDocumentFilterDefinition<Entity>>(), It.IsAny<BsonDocumentUpdateDefinition<Entity>>(), null, default))
      .Returns(Task.FromResult(new UpdateResult.Acknowledged(1, 1, null) as UpdateResult));
  }

  public void Dispose()
  {
    this.dbClientMock.Reset();
    this.dbDatabaseMock.Reset();
    this.dbCollectionMock.Reset();
  }

  [Fact]
  public async void InsertOne_ItShouldCallGetDatabaseFromTheMongoClientOnceWithTheProvidedDbName()
  {
    IDb sut = new Db(this.dbClientMock.Object);

    await sut.InsertOne<Entity>("test db name", "", new Entity { Name = "" });
    this.dbClientMock.Verify(m => m.GetDatabase("test db name", null), Times.Once());
  }

  [Fact]
  public async void InsertOne_ItShouldCallGetCollectionFromTheMongoDatabaseOnceWithTheProvidedCollectionName()
  {
    IDb sut = new Db(this.dbClientMock.Object);

    await sut.InsertOne<Entity>("", "test col name", new Entity { Name = "" });
    this.dbDatabaseMock.Verify(m => m.GetCollection<Entity>("test col name", null), Times.Once());
  }
  
  [Fact]
  public async void InsertOne_ItShouldCallInsertOneAsyncFromTheMongoCollectionOnceWithTheProvidedDocument()
  {
    IDb sut = new Db(this.dbClientMock.Object);

    Entity testDoc = new Entity { Name = "" };
    await sut.InsertOne<Entity>("", "", testDoc);
    this.dbCollectionMock.Verify(m => m.InsertOneAsync(testDoc, null, default), Times.Once());
  }

  [Fact]
  public async void ReplaceOne_ItShouldCallGetDatabaseFromTheMongoClientOnceWithTheProvidedDbName()
  {
    IDb sut = new Db(this.dbClientMock.Object);

    await sut.ReplaceOne<Entity>("some test db name", "", new Entity {}, ObjectId.GenerateNewId().ToString());
    this.dbClientMock.Verify(m => m.GetDatabase("some test db name", null), Times.Once());
  }

  [Fact]
  public async void ReplaceOne_ItShouldCallGetCollectionFromTheMongoDatabaseOnceWithTheProvidedCollectionName()
  {
    IDb sut = new Db(this.dbClientMock.Object);

    await sut.ReplaceOne<Entity>("", "another test col name", new Entity {}, ObjectId.GenerateNewId().ToString());
    this.dbDatabaseMock.Verify(m => m.GetCollection<Entity>("another test col name", null), Times.Once());
  }

  [Fact]
  public async void ReplaceOne_ItShouldCallReplaceOneAsyncFromTheMongoCollectionOnceWithTheCorrectFilter()
  {
    IDb sut = new Db(this.dbClientMock.Object);

    Entity testDoc = new Entity {};
    ObjectId testId = ObjectId.GenerateNewId();
    await sut.ReplaceOne<Entity>("", "", testDoc, testId.ToString());

    this.dbCollectionMock.Verify(m => m.ReplaceOneAsync(It.IsAny<BsonDocumentFilterDefinition<Entity>>(), It.IsAny<Entity>(), null as ReplaceOptions, default));
    Assert.Equal(
      new BsonDocument {
        {
          "_id",  testId
        }
      },
      (this.dbCollectionMock.Invocations[0].Arguments[0] as dynamic).Document
    );
  }

  [Fact]
  public async void ReplaceOne_ItShouldCallReplaceOneAsyncFromTheMongoCollectionOnceWithTheProvidedDocument()
  {
    IDb sut = new Db(this.dbClientMock.Object);

    Entity testDoc = new Entity {};
    ObjectId testId = ObjectId.GenerateNewId();
    await sut.ReplaceOne<Entity>("", "", testDoc, testId.ToString());

    this.dbCollectionMock.Verify(m => m.ReplaceOneAsync(It.IsAny<BsonDocumentFilterDefinition<Entity>>(), testDoc, null as ReplaceOptions, default));
  }

  [Fact]
  public async void ReplaceOne_IfNoDocumentIsFound_ItShouldThrowAKeyNotFoundException()
  {
    this.dbCollectionMock.Setup(s => s.ReplaceOneAsync(It.IsAny<BsonDocumentFilterDefinition<Entity>>(), It.IsAny<Entity>(), null as ReplaceOptions, default))
      .Returns(Task.FromResult(new ReplaceOneResult.Acknowledged(0, 1, null) as ReplaceOneResult));

    IDb sut = new Db(this.dbClientMock.Object);

    Entity testDoc = new Entity {};
    ObjectId testId = ObjectId.GenerateNewId();

    KeyNotFoundException exception = await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.ReplaceOne<Entity>("", "", testDoc, testId.ToString()));
    Assert.Equal($"Could not find the document with ID '{testId}'", exception.Message);
  }

  [Fact]
  public async void ReplaceOne_IfNoDocumentIsReplaced_ItShouldThrowAnException()
  {
    this.dbCollectionMock.Setup(s => s.ReplaceOneAsync(It.IsAny<BsonDocumentFilterDefinition<Entity>>(), It.IsAny<Entity>(), null as ReplaceOptions, default))
      .Returns(Task.FromResult(new ReplaceOneResult.Acknowledged(1, 0, null) as ReplaceOneResult));

    IDb sut = new Db(this.dbClientMock.Object);

    Entity testDoc = new Entity {};
    ObjectId testId = ObjectId.GenerateNewId();

    Exception exception = await Assert.ThrowsAsync<Exception>(() => sut.ReplaceOne<Entity>("", "", testDoc, testId.ToString()));
    Assert.Equal($"Could not replace the document with ID '{testId}'", exception.Message);
  }

  [Fact]
  public async void DeleteOne_ItShouldCallGetDatabaseFromTheMongoClientOnceWithTheProvidedDbName()
  {
    IDb sut = new Db(this.dbClientMock.Object);

    await sut.DeleteOne<Entity>("another test db name", "", ObjectId.GenerateNewId().ToString());
    this.dbClientMock.Verify(m => m.GetDatabase("another test db name", null), Times.Once());
  }

    [Fact]
  public async void DeleteOne_ItShouldCallGetCollectionFromTheMongoDatabaseOnceWithTheProvidedCollectionName()
  {
    IDb sut = new Db(this.dbClientMock.Object);

    await sut.DeleteOne<Entity>("", "random test col name", ObjectId.GenerateNewId().ToString());
    this.dbDatabaseMock.Verify(m => m.GetCollection<Entity>("random test col name", null), Times.Once());
  }

  [Fact]
  public async void DeleteOne_ItShouldCallUpdateOneAsyncFromTheMongoCollectionOnceWithTheCorrectFilter()
  {
    IDb sut = new Db(this.dbClientMock.Object);

    ObjectId testId = ObjectId.GenerateNewId();
    await sut.DeleteOne<Entity>("", "", testId.ToString());

    this.dbCollectionMock.Verify(m => m.UpdateOneAsync(It.IsAny<BsonDocumentFilterDefinition<Entity>>(), It.IsAny<BsonDocumentUpdateDefinition<Entity>>(), null, default));
    Assert.Equal(
      new BsonDocument {
        {
          "_id",  testId
        }
      },
      (this.dbCollectionMock.Invocations[0].Arguments[0] as dynamic).Document
    );
  }

  [Fact]
  public async void DeleteOne_ItShouldCallUpdateOneAsyncFromTheMongoCollectionOnceWithTheCorrectUpdateDefinition()
  {
    IDb sut = new Db(this.dbClientMock.Object);

    ObjectId testId = ObjectId.GenerateNewId();
    await sut.DeleteOne<Entity>("", "", testId.ToString());

    this.dbCollectionMock.Verify(m => m.UpdateOneAsync(It.IsAny<BsonDocumentFilterDefinition<Entity>>(), It.IsAny<BsonDocumentUpdateDefinition<Entity>>(), null, default));
    Assert.Equal(
      new BsonDocument {
        {
          "$currentDate", new BsonDocument {
            { "deleted_at", true }
          }
        }
      },
      (this.dbCollectionMock.Invocations[0].Arguments[1] as dynamic).Document
    );
  }

    [Fact]
  public async void DeleteOne_IfNoDocumentIsFound_ItShouldThrowAKeyNotFoundException()
  {
    this.dbCollectionMock.Setup(s => s.UpdateOneAsync(It.IsAny<BsonDocumentFilterDefinition<Entity>>(), It.IsAny<BsonDocumentUpdateDefinition<Entity>>(), null, default))
      .Returns(Task.FromResult(new UpdateResult.Acknowledged(0, 1, null) as UpdateResult));

    IDb sut = new Db(this.dbClientMock.Object);

    ObjectId testId = ObjectId.GenerateNewId();

    KeyNotFoundException exception = await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.DeleteOne<Entity>("", "", testId.ToString()));
    Assert.Equal($"Could not find the document with ID '{testId}'", exception.Message);
  }

  [Fact]
  public async void DeleteOne_IfNoDocumentIsUpdated_ItShouldThrowAnException()
  {
    this.dbCollectionMock.Setup(s => s.UpdateOneAsync(It.IsAny<BsonDocumentFilterDefinition<Entity>>(), It.IsAny<BsonDocumentUpdateDefinition<Entity>>(), null, default))
      .Returns(Task.FromResult(new UpdateResult.Acknowledged(1, 0, null) as UpdateResult));

    IDb sut = new Db(this.dbClientMock.Object);

    ObjectId testId = ObjectId.GenerateNewId();

    Exception exception = await Assert.ThrowsAsync<Exception>(() => sut.DeleteOne<Entity>("", "", testId.ToString()));
    Assert.Equal($"Could not update the document with ID '{testId}'", exception.Message);
  }
}