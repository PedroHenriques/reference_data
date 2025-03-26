using DbListener.Services;
using Moq;
using Newtonsoft.Json;
using Toolkit.Types;
using SharedLibs.Types;
using Xunit;

namespace DbListener.Tests.Services;

[Trait("Type", "Unit")]
public class DbStreamTests : IDisposable
{
  private readonly Mock<ICache> _cacheMock;
  private readonly Mock<IQueue> _queueMock;
  private readonly Mock<IMongodb> _mongodbMock;

  public DbStreamTests()
  {
    Environment.SetEnvironmentVariable("REDIS_CON_STR", "test redis con str");
    Environment.SetEnvironmentVariable("DBLISTENER_CACHE_CHANGE_DATA_KEY", "change_resume_data");
    Environment.SetEnvironmentVariable("DBLISTENER_CACHE_CHANGES_QUEUE_KEY", "mongo_changes");
    Environment.SetEnvironmentVariable("MONGO_CON_STR", "test mongo con str");
    Environment.SetEnvironmentVariable("MONGO_DB_NAME", "RefData");

    this._cacheMock = new Mock<ICache>(MockBehavior.Strict);
    this._queueMock = new Mock<IQueue>(MockBehavior.Strict);
    this._mongodbMock = new Mock<IMongodb>(MockBehavior.Strict);

    this._cacheMock.Setup(s => s.GetString(It.IsAny<string>()))
      .Returns(Task.FromResult<string?>(null));
    this._cacheMock.Setup(s => s.Set(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
      .Returns(Task.FromResult(true));
    this._queueMock.Setup(s => s.Enqueue(It.IsAny<string>(), It.IsAny<string[]>()))
      .Returns(Task.FromResult((long)1));
    this._mongodbMock.Setup(s => s.WatchDb(It.IsAny<string>(), It.IsAny<ResumeData?>()))
      .Returns((new[] { new WatchData { ChangeTime = DateTime.Now, ResumeData = new ResumeData { }, Source = new ChangeSource { } } }).ToAsyncEnumerable());
  }

  public void Dispose()
  {
    Environment.SetEnvironmentVariable("REDIS_CON_STR", null);
    Environment.SetEnvironmentVariable("DBLISTENER_CACHE_CHANGE_DATA_KEY", null);
    Environment.SetEnvironmentVariable("DBLISTENER_CACHE_CHANGES_QUEUE_KEY", null);
    Environment.SetEnvironmentVariable("MONGO_CON_STR", null);
    Environment.SetEnvironmentVariable("MONGO_DB_NAME", null);

    this._cacheMock.Reset();
    this._queueMock.Reset();
    this._mongodbMock.Reset();
  }

  [Fact]
  public async void Watch_ItShouldCallGetOnTheICacheInstanceOnce()
  {
    await DbStream.Watch(this._cacheMock.Object, this._queueMock.Object, this._mongodbMock.Object);
    this._cacheMock.Verify(m => m.GetString("change_resume_data"), Times.Once());
  }

  [Fact]
  public async void Watch_ItShouldCallWatchOnTheIDbInstanceOnce()
  {
    await DbStream.Watch(this._cacheMock.Object, this._queueMock.Object, this._mongodbMock.Object);
    this._mongodbMock.Verify(s => s.WatchDb("RefData", null), Times.Once());
  }

  [Fact]
  public async void Watch_IfThereIsNoResumeDataReturnedFromICache_ItShouldCallWatchOnTheIDbInstanceOnce()
  {
    ResumeData testData = new ResumeData { ResumeToken = "test token", ClusterTime = "test time" };
    this._cacheMock.Setup(s => s.GetString(It.IsAny<string>()))
      .Returns(Task.FromResult<string?>(JsonConvert.SerializeObject(testData)));

    await DbStream.Watch(this._cacheMock.Object, this._queueMock.Object, this._mongodbMock.Object);
    this._mongodbMock.Verify(s => s.WatchDb("RefData", testData), Times.Once());
  }

  [Fact]
  public async void Watch_If2ItemsAreReceivedFromTheDbWatch_ItShouldCallEnqueueOnTheICacheInstanceTwice()
  {
    this._mongodbMock.Setup(s => s.WatchDb(It.IsAny<string>(), It.IsAny<ResumeData?>()))
      .Returns((new[] {
        new WatchData { ChangeRecord = new ChangeRecord { Id = "", ChangeType = ChangeRecordTypes.Delete }, ChangeTime = DateTime.Now, ResumeData = new ResumeData{}, Source = new ChangeSource {} },
        new WatchData { ChangeRecord = new ChangeRecord { Id = "", ChangeType = ChangeRecordTypes.Delete }, ChangeTime = DateTime.Now, ResumeData = new ResumeData{}, Source = new ChangeSource {} },
      }).ToAsyncEnumerable());

    await DbStream.Watch(this._cacheMock.Object, this._queueMock.Object, this._mongodbMock.Object);
    this._queueMock.Verify(m => m.Enqueue("mongo_changes", It.IsAny<string[]>()), Times.Exactly(2));
  }

  [Fact]
  public async void Watch_If2ItemsAreReceivedFromTheDbWatch_ItShouldCallEnqueueOnTheICacheInstanceWithTheExpectedFirstItem()
  {
    var expectedChangeRecord = new ChangeRecord { ChangeType = ChangeRecordTypes.Insert, Id = "test change record" };
    var testTime = DateTime.Now;
    this._mongodbMock.Setup(s => s.WatchDb(It.IsAny<string>(), It.IsAny<ResumeData?>()))
      .Returns((new[] {
        new WatchData { ChangeRecord = expectedChangeRecord, Source = new ChangeSource { DbName = "test db name", CollName = "test coll name" }, ChangeTime = testTime, ResumeData = new ResumeData{} },
        new WatchData { ChangeRecord = new ChangeRecord { ChangeType = ChangeRecordTypes.Insert, Id = "not the correct one" }, ChangeTime = testTime, ResumeData = new ResumeData{}, Source = new ChangeSource {} },
      }).ToAsyncEnumerable());

    await DbStream.Watch(this._cacheMock.Object, this._queueMock.Object, this._mongodbMock.Object);
    Assert.Equal(
      new[] {
        JsonConvert.SerializeObject(new ChangeQueueItem{
          ChangeTime = testTime,
          ChangeRecord = JsonConvert.SerializeObject(expectedChangeRecord),
          Source = JsonConvert.SerializeObject(new ChangeSource{ DbName = "test db name", CollName = "test coll name" }),
        }),
      },
      this._queueMock.Invocations[0].Arguments[1]
    );
  }

  [Fact]
  public async void Watch_If2ItemsAreReceivedFromTheDbWatch_ItShouldCallEnqueueOnTheICacheInstanceWithTheExpectedSecondItem()
  {
    var expectedChangeRecord = new ChangeRecord { ChangeType = ChangeRecordTypes.Insert, Id = "another test change record" };
    var testTime = DateTime.Now;
    this._mongodbMock.Setup(s => s.WatchDb(It.IsAny<string>(), It.IsAny<ResumeData?>()))
      .Returns((new[] {
        new WatchData { ChangeRecord = new ChangeRecord { ChangeType = ChangeRecordTypes.Insert, Id = "not the correct one" }, ChangeTime = testTime, ResumeData = new ResumeData{}, Source = new ChangeSource {} },
        new WatchData { ChangeRecord = expectedChangeRecord, Source = new ChangeSource { DbName = "another test db name", CollName = "another test coll name" }, ChangeTime = testTime, ResumeData = new ResumeData{} },
      }).ToAsyncEnumerable());

    await DbStream.Watch(this._cacheMock.Object, this._queueMock.Object, this._mongodbMock.Object);
    Assert.Equal(
      new[] {
        JsonConvert.SerializeObject(new ChangeQueueItem{
          ChangeTime = testTime,
          ChangeRecord = JsonConvert.SerializeObject(expectedChangeRecord),
          Source = JsonConvert.SerializeObject(new ChangeSource{ DbName = "another test db name", CollName = "another test coll name" }),
        }),
      },
      this._queueMock.Invocations[1].Arguments[1]
    );
  }

  [Fact]
  public async void Watch_If2ItemsAreReceivedFromTheDbWatch_ItShouldCallSetOnTheICacheInstanceTwice()
  {
    this._mongodbMock.Setup(s => s.WatchDb(It.IsAny<string>(), It.IsAny<ResumeData?>()))
      .Returns((new[] {
        new WatchData { ChangeRecord = new ChangeRecord { Id = "", ChangeType = ChangeRecordTypes.Delete }, ChangeTime = DateTime.Now, ResumeData = new ResumeData{}, Source = new ChangeSource {} },
        new WatchData { ChangeRecord = new ChangeRecord { Id = "", ChangeType = ChangeRecordTypes.Delete }, ChangeTime = DateTime.Now, ResumeData = new ResumeData{}, Source = new ChangeSource {} },
      }).ToAsyncEnumerable());

    await DbStream.Watch(this._cacheMock.Object, this._queueMock.Object, this._mongodbMock.Object);
    this._cacheMock.Verify(m => m.Set("change_resume_data", It.IsAny<string>(), null), Times.Exactly(2));
  }

  [Fact]
  public async void Watch_If2ItemsAreReceivedFromTheDbWatch_ItShouldCallSetOnTheICacheInstanceWithTheExpectedFirstResumeData()
  {
    ResumeData expectedResumeData = new ResumeData { ResumeToken = "test resume token", ClusterTime = "test cluster time" };
    this._mongodbMock.Setup(s => s.WatchDb(It.IsAny<string>(), It.IsAny<ResumeData?>()))
      .Returns((new[] {
        new WatchData { ResumeData = expectedResumeData, ChangeRecord = new ChangeRecord { Id = "", ChangeType = ChangeRecordTypes.Delete }, ChangeTime = DateTime.Now, Source = new ChangeSource {} },
        new WatchData { ChangeRecord = new ChangeRecord { Id = "", ChangeType = ChangeRecordTypes.Delete }, ChangeTime = DateTime.Now, ResumeData = new ResumeData{}, Source = new ChangeSource {} },
      }).ToAsyncEnumerable());

    await DbStream.Watch(this._cacheMock.Object, this._queueMock.Object, this._mongodbMock.Object);
    Assert.Equal(
      JsonConvert.SerializeObject(expectedResumeData),
      this._cacheMock.Invocations[1].Arguments[1]
    );
  }

  [Fact]
  public async void Watch_If2ItemsAreReceivedFromTheDbWatch_ItShouldCallSetOnTheICacheInstanceWithTheExpectedSecondResumeData()
  {
    ResumeData expectedResumeData = new ResumeData { ResumeToken = "another test resume token", ClusterTime = "another test cluster time" };
    this._mongodbMock.Setup(s => s.WatchDb(It.IsAny<string>(), It.IsAny<ResumeData?>()))
      .Returns((new[] {
        new WatchData { ChangeRecord = new ChangeRecord { Id = "", ChangeType = ChangeRecordTypes.Delete }, ChangeTime = DateTime.Now, ResumeData = new ResumeData{}, Source = new ChangeSource {} },
        new WatchData { ResumeData = expectedResumeData, ChangeRecord = new ChangeRecord { Id = "", ChangeType = ChangeRecordTypes.Delete }, ChangeTime = DateTime.Now, Source = new ChangeSource {} },
      }).ToAsyncEnumerable());

    await DbStream.Watch(this._cacheMock.Object, this._queueMock.Object, this._mongodbMock.Object);
    Assert.Equal(
      JsonConvert.SerializeObject(expectedResumeData),
      this._cacheMock.Invocations[2].Arguments[1]
    );
  }

  [Fact]
  public async void Watch_IfTheAnItemReceivedFromTheDbWatchHasANullChangeRecord_ItShouldNotCallEnqueueOnTheICacheInstance()
  {
    this._mongodbMock.Setup(s => s.WatchDb(It.IsAny<string>(), It.IsAny<ResumeData?>()))
      .Returns((new[] {
        new WatchData { ChangeTime = DateTime.Now, ResumeData = new ResumeData{}, Source = new ChangeSource {} },
      }).ToAsyncEnumerable());

    await DbStream.Watch(this._cacheMock.Object, this._queueMock.Object, this._mongodbMock.Object);
    this._queueMock.Verify(m => m.Enqueue("mongo_changes", It.IsAny<string[]>()), Times.Never);
  }

  [Fact]
  public async void Watch_IfTheAnItemReceivedFromTheDbWatchHasANullChangeRecord_ItShouldCallSetOnTheICacheInstanceWithTheExpectedValue()
  {
    ResumeData expectedResumeData = new ResumeData { ResumeToken = "another test resume token", ClusterTime = "another test cluster time" };
    this._mongodbMock.Setup(s => s.WatchDb(It.IsAny<string>(), It.IsAny<ResumeData?>()))
      .Returns((new[] {
        new WatchData { ResumeData = expectedResumeData, ChangeTime = DateTime.Now, Source = new ChangeSource {} },
      }).ToAsyncEnumerable());

    await DbStream.Watch(this._cacheMock.Object, this._queueMock.Object, this._mongodbMock.Object);
    Assert.Equal(
      JsonConvert.SerializeObject(expectedResumeData),
      this._cacheMock.Invocations[1].Arguments[1]
    );
  }
}