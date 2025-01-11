using DbListener.Services;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using Newtonsoft.Json;
using SharedLibs;
using SharedLibs.Types;
using SharedLibs.Types.Db;
using StackExchange.Redis;
using Xunit;

namespace DbListener.Tests.Services;

public class DbStreamTests : IDisposable
{
  private readonly Mock<ICache> _cacheMock;
  private readonly Mock<IDb> _dbSharedLibMock;

  public DbStreamTests()
  {
    this._cacheMock = new Mock<ICache>(MockBehavior.Strict);
    this._dbSharedLibMock = new Mock<IDb>(MockBehavior.Strict);

    this._cacheMock.Setup(s => s.Get(It.IsAny<CacheTypes>(), It.IsAny<string>()))
      .Returns(Task.FromResult(new RedisValue { }));
    this._cacheMock.Setup(s => s.Enqueue(It.IsAny<string>(), It.IsAny<RedisValue[]>()))
      .Returns(Task.FromResult((long)1));
    this._cacheMock.Setup(s => s.Set(It.IsAny<CacheTypes>(), It.IsAny<string>(), It.IsAny<RedisValue>()))
      .Returns(Task.FromResult(true));
    this._dbSharedLibMock.Setup(s => s.WatchDb(It.IsAny<string>(), It.IsAny<ChangeStreamOptions>()))
      .Returns((new[] { new WatchData { } }).ToAsyncEnumerable());
  }

  public void Dispose()
  {
    this._cacheMock.Reset();
    this._dbSharedLibMock.Reset();
  }

  [Fact]
  public async void Watch_ItShouldCallGetOnTheICacheInstanceOnce()
  {
    await DbStream.Watch(this._cacheMock.Object, this._dbSharedLibMock.Object);
    this._cacheMock.Verify(m => m.Get(CacheTypes.String, "change_resume_data"), Times.Once());
  }

  [Fact]
  public async void Watch_ItShouldCallWatchOnTheIDbInstanceOnce()
  {
    await DbStream.Watch(this._cacheMock.Object, this._dbSharedLibMock.Object);
    this._dbSharedLibMock.Verify(s => s.WatchDb("RefData", null), Times.Once());
  }

  [Fact]
  public async void Watch_IfThereIsAResumeTokenDocId_ItShouldCallWatchOnTheIDbInstanceOnceWithTheCorrectDatabaseName()
  {
    ResumeData testToken = new ResumeData
    {
      ResumeToken = new BsonDocument().ToJson()
    };
    this._cacheMock.Setup(s => s.Get(It.IsAny<CacheTypes>(), It.IsAny<string>()))
      .Returns(Task.FromResult(new RedisValue(JsonConvert.SerializeObject(testToken))));

    await DbStream.Watch(this._cacheMock.Object, this._dbSharedLibMock.Object);
    Assert.Equal("RefData", this._dbSharedLibMock.Invocations[0].Arguments[0]);
  }

  [Fact]
  public async void Watch_IfThereIsAResumeTokenDocId_ItShouldCallWatchOnTheIDbInstanceOnceWithTheCorrectOptions()
  {
    BsonDocument testResumeToken = new BsonDocument { { "some key", new BsonDocument { } } };
    ResumeData testToken = new ResumeData
    {
      ResumeToken = testResumeToken.ToJson(),
      ClusterTime = new BsonTimestamp(1).ToString()
    };
    this._cacheMock.Setup(s => s.Get(It.IsAny<CacheTypes>(), It.IsAny<string>()))
      .Returns(Task.FromResult(new RedisValue(JsonConvert.SerializeObject(testToken))));

    await DbStream.Watch(this._cacheMock.Object, this._dbSharedLibMock.Object);
    Assert.Equal(
      testResumeToken,
      (this._dbSharedLibMock.Invocations[0].Arguments[1] as dynamic).ResumeAfter
    );
  }

  [Fact]
  public async void Watch_IfThereIsNoResumeTokenDocIdButThereIsAClusterTime_ItShouldCallWatchOnTheIDbInstanceOnceWithTheCorrectDatabaseName()
  {
    ResumeData testToken = new ResumeData
    {
      ClusterTime = new BsonTimestamp(1).ToString()
    };
    this._cacheMock.Setup(s => s.Get(It.IsAny<CacheTypes>(), It.IsAny<string>()))
      .Returns(Task.FromResult(new RedisValue(JsonConvert.SerializeObject(testToken))));

    await DbStream.Watch(this._cacheMock.Object, this._dbSharedLibMock.Object);
    Assert.Equal("RefData", this._dbSharedLibMock.Invocations[0].Arguments[0]);
  }

  [Fact]
  public async void Watch_IfThereIsNoResumeTokenDocIdButThereIsAClusterTime_ItShouldCallWatchOnTheIDbInstanceOnceWithTheCorrectOptions()
  {
    BsonTimestamp testClusterTime = new BsonTimestamp(1000);
    ResumeData testToken = new ResumeData
    {
      ClusterTime = testClusterTime.ToString()
    };
    this._cacheMock.Setup(s => s.Get(It.IsAny<CacheTypes>(), It.IsAny<string>()))
      .Returns(Task.FromResult(new RedisValue(JsonConvert.SerializeObject(testToken))));

    await DbStream.Watch(this._cacheMock.Object, this._dbSharedLibMock.Object);
    Assert.Equal(
      testClusterTime,
      (this._dbSharedLibMock.Invocations[0].Arguments[1] as dynamic).StartAtOperationTime
    );
  }

  [Fact]
  public async void Watch_If2ItemsAreReceivedFromTheDbWatch_ItShouldCallEnqueueOnTheICacheInstanceTwice()
  {
    this._dbSharedLibMock.Setup(s => s.WatchDb(It.IsAny<string>(), It.IsAny<ChangeStreamOptions>()))
      .Returns((new[] { new WatchData { }, new WatchData { } }).ToAsyncEnumerable());

    await DbStream.Watch(this._cacheMock.Object, this._dbSharedLibMock.Object);
    this._cacheMock.Verify(m => m.Enqueue("mongo_changes", It.IsAny<RedisValue[]>()), Times.Exactly(2));
  }

  [Fact]
  public async void Watch_If2ItemsAreReceivedFromTheDbWatch_ItShouldCallEnqueueOnTheICacheInstanceWithTheExpectedFirstItem()
  {
    this._dbSharedLibMock.Setup(s => s.WatchDb(It.IsAny<string>(), It.IsAny<ChangeStreamOptions>()))
      .Returns((new[] { new WatchData { ChangeRecord = "test change record", Source = new ChangeSource { DbName = "test db name", CollName = "test coll name" } }, new WatchData { ChangeRecord = "not the correct one" } }).ToAsyncEnumerable());

    await DbStream.Watch(this._cacheMock.Object, this._dbSharedLibMock.Object);
    Assert.Equal(
      new[] {
        new RedisValue(
          JsonConvert.SerializeObject(new ChangeQueueItem{
            ChangeRecord = "test change record",
            Source = JsonConvert.SerializeObject(new ChangeSource{ DbName = "test db name", CollName = "test coll name" }),
          })
        ),
      },
      this._cacheMock.Invocations[1].Arguments[1]
    );
  }

  [Fact]
  public async void Watch_If2ItemsAreReceivedFromTheDbWatch_ItShouldCallEnqueueOnTheICacheInstanceWithTheExpectedSecondItem()
  {
    this._dbSharedLibMock.Setup(s => s.WatchDb(It.IsAny<string>(), It.IsAny<ChangeStreamOptions>()))
      .Returns((new[] { new WatchData { ChangeRecord = "not the correct one" }, new WatchData { ChangeRecord = "another test change record", Source = new ChangeSource { DbName = "another test db name", CollName = "another test coll name" } } }).ToAsyncEnumerable());

    await DbStream.Watch(this._cacheMock.Object, this._dbSharedLibMock.Object);
    Assert.Equal(
      new[] {
        new RedisValue(
          JsonConvert.SerializeObject(new ChangeQueueItem{
            ChangeRecord = "another test change record",
            Source = JsonConvert.SerializeObject(new ChangeSource{ DbName = "another test db name", CollName = "another test coll name" }),
          })
        ),
      },
      this._cacheMock.Invocations[3].Arguments[1]
    );
  }

  [Fact]
  public async void Watch_If2ItemsAreReceivedFromTheDbWatch_ItShouldCallSetOnTheICacheInstanceTwice()
  {
    this._dbSharedLibMock.Setup(s => s.WatchDb(It.IsAny<string>(), It.IsAny<ChangeStreamOptions>()))
      .Returns((new[] { new WatchData { }, new WatchData { } }).ToAsyncEnumerable());

    await DbStream.Watch(this._cacheMock.Object, this._dbSharedLibMock.Object);
    this._cacheMock.Verify(m => m.Set(CacheTypes.String, "change_resume_data", It.IsAny<RedisValue>()), Times.Exactly(2));
  }

  [Fact]
  public async void Watch_If2ItemsAreReceivedFromTheDbWatch_ItShouldCallSetOnTheICacheInstanceWithTheExpectedFirstResumeData()
  {
    ResumeData expectedResumeData = new ResumeData { ResumeToken = "test resume token", ClusterTime = "test cluster time" };
    this._dbSharedLibMock.Setup(s => s.WatchDb(It.IsAny<string>(), It.IsAny<ChangeStreamOptions>()))
      .Returns((new[] { new WatchData { ResumeData = expectedResumeData }, new WatchData { } }).ToAsyncEnumerable());

    await DbStream.Watch(this._cacheMock.Object, this._dbSharedLibMock.Object);
    Assert.Equal(
      new RedisValue(JsonConvert.SerializeObject(expectedResumeData)),
      this._cacheMock.Invocations[2].Arguments[2]
    );
  }

  [Fact]
  public async void Watch_If2ItemsAreReceivedFromTheDbWatch_ItShouldCallSetOnTheICacheInstanceWithTheExpectedSecondResumeData()
  {
    ResumeData expectedResumeData = new ResumeData { ResumeToken = "another test resume token", ClusterTime = "another test cluster time" };
    this._dbSharedLibMock.Setup(s => s.WatchDb(It.IsAny<string>(), It.IsAny<ChangeStreamOptions>()))
      .Returns((new[] { new WatchData { }, new WatchData { ResumeData = expectedResumeData } }).ToAsyncEnumerable());

    await DbStream.Watch(this._cacheMock.Object, this._dbSharedLibMock.Object);
    Assert.Equal(
      new RedisValue(JsonConvert.SerializeObject(expectedResumeData)),
      this._cacheMock.Invocations[4].Arguments[2]
    );
  }
}