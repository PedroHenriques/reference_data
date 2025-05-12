using Api.Configs;
using Api.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using Toolkit.Types;

namespace Api.Tests.Configs;

[CollectionDefinition("MongoIndexesTests", DisableParallelization = true)]
[Trait("Type", "Unit")]
public class MongoIndexesTests : IDisposable
{
  private readonly Mock<IMongodb> _mongoMock;

  public MongoIndexesTests()
  {
    Environment.SetEnvironmentVariable("MONGO_CON_STR", "test db con str");
    Environment.SetEnvironmentVariable("MONGO_DB_NAME", "RefData");
    Environment.SetEnvironmentVariable("MONGO_COL_NAME", "Entities");

    this._mongoMock = new Mock<IMongodb>(MockBehavior.Strict);

    this._mongoMock.Setup(s => s.CreateOneIndex<Entity>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BsonDocument>(), It.IsAny<CreateIndexOptions?>()))
      .Returns(Task.FromResult(""));
  }

  public void Dispose()
  {
    Environment.SetEnvironmentVariable("MONGO_CON_STR", null);
    Environment.SetEnvironmentVariable("MONGO_DB_NAME", null);
    Environment.SetEnvironmentVariable("MONGO_COL_NAME", null);

    this._mongoMock.Reset();
  }

  [Fact]
  public async Task Create_ItShouldCallCreateOneIndexOnTheProvidedIMongodbInstanceForTheUniqueIndexOnTheNamePropertyOfTheCollectionWithTheRegisteredEntities()
  {
    MongoIndexes.Create(this._mongoMock.Object);
    await Task.Delay(50);

    this._mongoMock.Verify(m => m.CreateOneIndex<Entity>(
      "RefData",
      "Entities",
      new BsonDocument { { "name", 1 } },
      It.IsAny<CreateIndexOptions?>()
    ));
  }

  [Fact]
  public async Task Create_ItShouldCallCreateOneIndexOnTheProvidedIMongodbInstanceForTheUniqueIndexOnTheNamePropertyOfTheCollectionWithTheRegisteredEntitiesWithTheExpectedOptions()
  {
    MongoIndexes.Create(this._mongoMock.Object);
    await Task.Delay(50);

    // Xunit can't assert the equality of the contents of 2 instances of CreateIndexOptions
    var opts = this._mongoMock.Invocations[0].Arguments[3] as CreateIndexOptions;
    Assert.True(opts.Unique);
    Assert.Equal("name_unique", opts.Name);
  }
}