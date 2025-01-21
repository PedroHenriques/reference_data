using DbListener.Services;
using Moq;
using Newtonsoft.Json;
using SharedLibs;
using SharedLibs.Types;
using SharedLibs.Types.Db;
using Xunit;

namespace DbListener.Tests.Services;

[Trait("Type", "Unit")]
public class DbStreamTests : IDisposable
{
  private readonly Mock<ICache> _cacheMock;
  private readonly Mock<IQueue> _queueMock;
  private readonly Mock<IDb> _dbSharedLibMock;

  public DbStreamTests()
  {
    this._cacheMock = new Mock<ICache>(MockBehavior.Strict);
    this._queueMock = new Mock<IQueue>(MockBehavior.Strict);
    this._dbSharedLibMock = new Mock<IDb>(MockBehavior.Strict);

    this._cacheMock.Setup(s => s.Get(It.IsAny<string>()))
      .Returns(Task.FromResult<string?>(null));
    this._cacheMock.Setup(s => s.Set(It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.FromResult(true));
    this._queueMock.Setup(s => s.Enqueue(It.IsAny<string>(), It.IsAny<string[]>()))
      .Returns(Task.FromResult((long)1));
    this._dbSharedLibMock.Setup(s => s.WatchDb(It.IsAny<string>(), It.IsAny<ResumeData?>()))
      .Returns((new[] { new WatchData { } }).ToAsyncEnumerable());
  }

  public void Dispose()
  {
    this._cacheMock.Reset();
    this._queueMock.Reset();
    this._dbSharedLibMock.Reset();
  }

  [Fact]
  public async void Watch_ItShouldCallGetOnTheICacheInstanceOnce()
  {
    await DbStream.Watch(this._cacheMock.Object, this._queueMock.Object, this._dbSharedLibMock.Object);
    this._cacheMock.Verify(m => m.Get("change_resume_data"), Times.Once());
  }

  [Fact]
  public async void Watch_ItShouldCallWatchOnTheIDbInstanceOnce()
  {
    await DbStream.Watch(this._cacheMock.Object, this._queueMock.Object, this._dbSharedLibMock.Object);
    this._dbSharedLibMock.Verify(s => s.WatchDb("RefData", null), Times.Once());
  }

  [Fact]
  public async void Watch_IfThereIsNoResumeDataReturnedFromICache_ItShouldCallWatchOnTheIDbInstanceOnce()
  {
    ResumeData testData = new ResumeData { ResumeToken = "test token", ClusterTime = "test time" };
    this._cacheMock.Setup(s => s.Get(It.IsAny<string>()))
      .Returns(Task.FromResult<string?>(JsonConvert.SerializeObject(testData)));

    await DbStream.Watch(this._cacheMock.Object, this._queueMock.Object, this._dbSharedLibMock.Object);
    this._dbSharedLibMock.Verify(s => s.WatchDb("RefData", testData), Times.Once());
  }

  [Fact]
  public async void Watch_If2ItemsAreReceivedFromTheDbWatch_ItShouldCallEnqueueOnTheICacheInstanceTwice()
  {
    this._dbSharedLibMock.Setup(s => s.WatchDb(It.IsAny<string>(), It.IsAny<ResumeData?>()))
      .Returns((new[] { new WatchData { }, new WatchData { } }).ToAsyncEnumerable());

    await DbStream.Watch(this._cacheMock.Object, this._queueMock.Object, this._dbSharedLibMock.Object);
    this._queueMock.Verify(m => m.Enqueue("mongo_changes", It.IsAny<string[]>()), Times.Exactly(2));
  }

  [Fact]
  public async void Watch_If2ItemsAreReceivedFromTheDbWatch_ItShouldCallEnqueueOnTheICacheInstanceWithTheExpectedFirstItem()
  {
    var expectedChangeRecord = new ChangeRecord { ChangeType = ChangeRecordTypes.Insert, Id = "test change record" };
    this._dbSharedLibMock.Setup(s => s.WatchDb(It.IsAny<string>(), It.IsAny<ResumeData?>()))
      .Returns((new[] { new WatchData { ChangeRecord = expectedChangeRecord, Source = new ChangeSource { DbName = "test db name", CollName = "test coll name" } }, new WatchData { ChangeRecord = new ChangeRecord { ChangeType = ChangeRecordTypes.Insert, Id = "not the correct one" } } }).ToAsyncEnumerable());

    await DbStream.Watch(this._cacheMock.Object, this._queueMock.Object, this._dbSharedLibMock.Object);
    Assert.Equal(
      new[] {
        JsonConvert.SerializeObject(new ChangeQueueItem{
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
    this._dbSharedLibMock.Setup(s => s.WatchDb(It.IsAny<string>(), It.IsAny<ResumeData?>()))
      .Returns((new[] { new WatchData { ChangeRecord = new ChangeRecord { ChangeType = ChangeRecordTypes.Insert, Id = "not the correct one" } }, new WatchData { ChangeRecord = expectedChangeRecord, Source = new ChangeSource { DbName = "another test db name", CollName = "another test coll name" } } }).ToAsyncEnumerable());

    await DbStream.Watch(this._cacheMock.Object, this._queueMock.Object, this._dbSharedLibMock.Object);
    Assert.Equal(
      new[] {
        JsonConvert.SerializeObject(new ChangeQueueItem{
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
    this._dbSharedLibMock.Setup(s => s.WatchDb(It.IsAny<string>(), It.IsAny<ResumeData?>()))
      .Returns((new[] { new WatchData { }, new WatchData { } }).ToAsyncEnumerable());

    await DbStream.Watch(this._cacheMock.Object, this._queueMock.Object, this._dbSharedLibMock.Object);
    this._cacheMock.Verify(m => m.Set("change_resume_data", It.IsAny<string>()), Times.Exactly(2));
  }

  [Fact]
  public async void Watch_If2ItemsAreReceivedFromTheDbWatch_ItShouldCallSetOnTheICacheInstanceWithTheExpectedFirstResumeData()
  {
    ResumeData expectedResumeData = new ResumeData { ResumeToken = "test resume token", ClusterTime = "test cluster time" };
    this._dbSharedLibMock.Setup(s => s.WatchDb(It.IsAny<string>(), It.IsAny<ResumeData?>()))
      .Returns((new[] { new WatchData { ResumeData = expectedResumeData }, new WatchData { } }).ToAsyncEnumerable());

    await DbStream.Watch(this._cacheMock.Object, this._queueMock.Object, this._dbSharedLibMock.Object);
    Assert.Equal(
      JsonConvert.SerializeObject(expectedResumeData),
      this._cacheMock.Invocations[1].Arguments[1]
    );
  }

  [Fact]
  public async void Watch_If2ItemsAreReceivedFromTheDbWatch_ItShouldCallSetOnTheICacheInstanceWithTheExpectedSecondResumeData()
  {
    ResumeData expectedResumeData = new ResumeData { ResumeToken = "another test resume token", ClusterTime = "another test cluster time" };
    this._dbSharedLibMock.Setup(s => s.WatchDb(It.IsAny<string>(), It.IsAny<ResumeData?>()))
      .Returns((new[] { new WatchData { }, new WatchData { ResumeData = expectedResumeData } }).ToAsyncEnumerable());

    await DbStream.Watch(this._cacheMock.Object, this._queueMock.Object, this._dbSharedLibMock.Object);
    Assert.Equal(
      JsonConvert.SerializeObject(expectedResumeData),
      this._cacheMock.Invocations[2].Arguments[1]
    );
  }
}