using DbListener.Services;
using Moq;
using Newtonsoft.Json;
using Toolkit.Types;
using SharedLibs.Types;
using Xunit;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk;

namespace DbListener.Tests.Services;

[Trait("Type", "Unit")]
public class DbStreamTests : IDisposable
{
  private readonly Mock<ICache> _cacheMock;
  private readonly Mock<IQueue> _queueMock;
  private readonly Mock<IMongodb> _mongodbMock;
  private readonly Mock<IFeatureFlags> _ffMock;

  public DbStreamTests()
  {
    Environment.SetEnvironmentVariable("REDIS_CON_HOST", "test redis con host");
    Environment.SetEnvironmentVariable("REDIS_CON_PORT", "test redis con port");
    Environment.SetEnvironmentVariable("REDIS_PW", "test redis pw");
    Environment.SetEnvironmentVariable("DBLISTENER_CACHE_CHANGE_DATA_KEY", "change_resume_data");
    Environment.SetEnvironmentVariable("DBLISTENER_CACHE_CHANGES_QUEUE_KEY", "mongo_changes");
    Environment.SetEnvironmentVariable("MONGO_CON_STR", "test mongo con str");
    Environment.SetEnvironmentVariable("MONGO_DB_NAME", "RefData");
    Environment.SetEnvironmentVariable("LD_DBLISTENER_ACTIVE_KEY", "test flag key");

    this._cacheMock = new Mock<ICache>(MockBehavior.Strict);
    this._queueMock = new Mock<IQueue>(MockBehavior.Strict);
    this._mongodbMock = new Mock<IMongodb>(MockBehavior.Strict);
    this._ffMock = new Mock<IFeatureFlags>(MockBehavior.Strict);

    this._cacheMock.Setup(s => s.GetString(It.IsAny<string>()))
      .Returns(Task.FromResult<string?>(null));
    this._cacheMock.Setup(s => s.Set(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
      .Returns(Task.FromResult(true));

    this._queueMock.Setup(s => s.Enqueue(It.IsAny<string>(), It.IsAny<string[]>()))
      .Returns(Task.FromResult((long)1));

    this._mongodbMock.Setup(s => s.WatchDb(It.IsAny<string>(), It.IsAny<ResumeData?>(), It.IsAny<CancellationToken>()))
      .Returns((new[] { new WatchData { ChangeTime = DateTime.Now, ResumeData = new ResumeData { }, Source = new ChangeSource { } } }).ToAsyncEnumerable());

    this._ffMock.Setup(s => s.GetBoolFlagValue(It.IsAny<string>()))
      .Returns(true);
    this._ffMock.Setup(s => s.SubscribeToValueChanges(It.IsAny<string>(), It.IsAny<Action<FlagValueChangeEvent>>()));
  }

  public void Dispose()
  {
    Environment.SetEnvironmentVariable("REDIS_CON_HOST", null);
    Environment.SetEnvironmentVariable("REDIS_CON_PORT", null);
    Environment.SetEnvironmentVariable("REDIS_PW", null);
    Environment.SetEnvironmentVariable("DBLISTENER_CACHE_CHANGE_DATA_KEY", null);
    Environment.SetEnvironmentVariable("DBLISTENER_CACHE_CHANGES_QUEUE_KEY", null);
    Environment.SetEnvironmentVariable("MONGO_CON_STR", null);
    Environment.SetEnvironmentVariable("MONGO_DB_NAME", null);
    Environment.SetEnvironmentVariable("LD_DBLISTENER_ACTIVE_KEY", null);

    this._cacheMock.Reset();
    this._queueMock.Reset();
    this._mongodbMock.Reset();
    this._ffMock.Reset();
  }

  [Fact]
  public async Task Watch_ItShouldCallGetOnTheICacheInstanceOnce()
  {
    await DbStream.Watch(this._cacheMock.Object, this._queueMock.Object, this._mongodbMock.Object, this._ffMock.Object);
    this._cacheMock.Verify(m => m.GetString("change_resume_data"), Times.Once());
  }

  [Fact]
  public async Task Watch_ItShouldCallGetBoolFlagValueOnTheIFeatureFlagsInstanceOnce()
  {
    await DbStream.Watch(this._cacheMock.Object, this._queueMock.Object, this._mongodbMock.Object, this._ffMock.Object);
    this._ffMock.Verify(s => s.GetBoolFlagValue("test flag key"), Times.Once());
  }

  [Fact]
  public async Task Watch_ItShouldCallSubscribeToValueChangesOnTheIFeatureFlagsInstanceOnce()
  {
    await DbStream.Watch(this._cacheMock.Object, this._queueMock.Object, this._mongodbMock.Object, this._ffMock.Object);
    this._ffMock.Verify(s => s.SubscribeToValueChanges("test flag key", It.IsAny<Action<FlagValueChangeEvent>>()), Times.Once());
  }

  [Fact]
  public async Task Watch_InvokingTheCallbackPassedToSubscribeToValueChangesOnTheIFeatureFlagsInstance_IfTheChangeEventHasANewValueOfTrue_ItShouldCallGetOnTheICacheInstanceOnce()
  {
    var oldValue = LdValue.Of(false);
    var newValue = LdValue.Of(true);
    var testEvent = new FlagValueChangeEvent("test flag key", oldValue, newValue);
    this._ffMock.Setup(s => s.GetBoolFlagValue(It.IsAny<string>()))
      .Returns(false);

    await DbStream.Watch(this._cacheMock.Object, this._queueMock.Object, this._mongodbMock.Object, this._ffMock.Object);
    (this._ffMock.Invocations[1].Arguments[1] as Action<FlagValueChangeEvent>)(testEvent);

    this._cacheMock.Verify(m => m.GetString("change_resume_data"), Times.Once());
  }

  [Fact]
  public async Task Watch_InvokingTheCallbackPassedToSubscribeToValueChangesOnTheIFeatureFlagsInstance_IfTheChangeEventHasANewValueOfTrue_ItShouldCallWatchOnTheIDbInstanceOnce()
  {
    var oldValue = LdValue.Of(false);
    var newValue = LdValue.Of(true);
    var testEvent = new FlagValueChangeEvent("test flag key", oldValue, newValue);
    this._ffMock.Setup(s => s.GetBoolFlagValue(It.IsAny<string>()))
      .Returns(false);
    var testResumeData = new ResumeData { ResumeToken = "test resume token", ClusterTime = "another test cluster time" };
    this._cacheMock.Setup(s => s.GetString(It.IsAny<string>()))
      .Returns(Task.FromResult<string?>(JsonConvert.SerializeObject(testResumeData)));

    await DbStream.Watch(this._cacheMock.Object, this._queueMock.Object, this._mongodbMock.Object, this._ffMock.Object);
    (this._ffMock.Invocations[1].Arguments[1] as Action<FlagValueChangeEvent>)(testEvent);

    this._mongodbMock.Verify(s => s.WatchDb("RefData", testResumeData, It.IsAny<CancellationToken>()), Times.Once());
  }

  [Fact]
  public async Task Watch_InvokingTheCallbackPassedToSubscribeToValueChangesOnTheIFeatureFlagsInstance_IfTheChangeEventHasANewValueOfFalse_ItShouldCancelTheTokenPassedToWatchOnTheIDbInstance()
  {
    var oldValue = LdValue.Of(true);
    var newValue = LdValue.Of(false);
    var testEvent = new FlagValueChangeEvent("test flag key", oldValue, newValue);

    await DbStream.Watch(this._cacheMock.Object, this._queueMock.Object, this._mongodbMock.Object, this._ffMock.Object);
    var token = (CancellationToken)this._mongodbMock.Invocations[0].Arguments[2];
    (this._ffMock.Invocations[1].Arguments[1] as Action<FlagValueChangeEvent>)(testEvent);

    Assert.True(token.IsCancellationRequested);
  }

  [Fact]
  public async Task Watch_ItShouldCallWatchOnTheIDbInstanceOnce()
  {
    await DbStream.Watch(this._cacheMock.Object, this._queueMock.Object, this._mongodbMock.Object, this._ffMock.Object);
    this._mongodbMock.Verify(s => s.WatchDb("RefData", null, It.IsAny<CancellationToken>()), Times.Once());
  }

  [Fact]
  public async Task Watch_IfThereIsNoResumeDataReturnedFromICache_ItShouldCallWatchOnTheIDbInstanceOnce()
  {
    ResumeData testData = new ResumeData { ResumeToken = "test token", ClusterTime = "test time" };
    this._cacheMock.Setup(s => s.GetString(It.IsAny<string>()))
      .Returns(Task.FromResult<string?>(JsonConvert.SerializeObject(testData)));

    await DbStream.Watch(this._cacheMock.Object, this._queueMock.Object, this._mongodbMock.Object, this._ffMock.Object);
    this._mongodbMock.Verify(s => s.WatchDb("RefData", testData, It.IsAny<CancellationToken>()), Times.Once());
  }

  [Fact]
  public async Task Watch_If2ItemsAreReceivedFromTheDbWatch_ItShouldCallEnqueueOnTheICacheInstanceTwice()
  {
    this._mongodbMock.Setup(s => s.WatchDb(It.IsAny<string>(), It.IsAny<ResumeData?>(), It.IsAny<CancellationToken>()))
      .Returns((new[] {
        new WatchData { ChangeRecord = new ChangeRecord { Id = "", ChangeType = ChangeRecordTypes.Delete }, ChangeTime = DateTime.Now, ResumeData = new ResumeData{}, Source = new ChangeSource {} },
        new WatchData { ChangeRecord = new ChangeRecord { Id = "", ChangeType = ChangeRecordTypes.Delete }, ChangeTime = DateTime.Now, ResumeData = new ResumeData{}, Source = new ChangeSource {} },
      }).ToAsyncEnumerable());

    await DbStream.Watch(this._cacheMock.Object, this._queueMock.Object, this._mongodbMock.Object, this._ffMock.Object);
    this._queueMock.Verify(m => m.Enqueue("mongo_changes", It.IsAny<string[]>()), Times.Exactly(2));
  }

  [Fact]
  public async Task Watch_If2ItemsAreReceivedFromTheDbWatch_ItShouldCallEnqueueOnTheICacheInstanceWithTheExpectedFirstItem()
  {
    var expectedChangeRecord = new ChangeRecord { ChangeType = ChangeRecordTypes.Insert, Id = "test change record" };
    var testTime = DateTime.Now;
    this._mongodbMock.Setup(s => s.WatchDb(It.IsAny<string>(), It.IsAny<ResumeData?>(), It.IsAny<CancellationToken>()))
      .Returns((new[] {
        new WatchData { ChangeRecord = expectedChangeRecord, Source = new ChangeSource { DbName = "test db name", CollName = "test coll name" }, ChangeTime = testTime, ResumeData = new ResumeData{} },
        new WatchData { ChangeRecord = new ChangeRecord { ChangeType = ChangeRecordTypes.Insert, Id = "not the correct one" }, ChangeTime = testTime, ResumeData = new ResumeData{}, Source = new ChangeSource {} },
      }).ToAsyncEnumerable());

    await DbStream.Watch(this._cacheMock.Object, this._queueMock.Object, this._mongodbMock.Object, this._ffMock.Object);
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
  public async Task Watch_If2ItemsAreReceivedFromTheDbWatch_ItShouldCallEnqueueOnTheICacheInstanceWithTheExpectedSecondItem()
  {
    var expectedChangeRecord = new ChangeRecord { ChangeType = ChangeRecordTypes.Insert, Id = "another test change record" };
    var testTime = DateTime.Now;
    this._mongodbMock.Setup(s => s.WatchDb(It.IsAny<string>(), It.IsAny<ResumeData?>(), It.IsAny<CancellationToken>()))
      .Returns((new[] {
        new WatchData { ChangeRecord = new ChangeRecord { ChangeType = ChangeRecordTypes.Insert, Id = "not the correct one" }, ChangeTime = testTime, ResumeData = new ResumeData{}, Source = new ChangeSource {} },
        new WatchData { ChangeRecord = expectedChangeRecord, Source = new ChangeSource { DbName = "another test db name", CollName = "another test coll name" }, ChangeTime = testTime, ResumeData = new ResumeData{} },
      }).ToAsyncEnumerable());

    await DbStream.Watch(this._cacheMock.Object, this._queueMock.Object, this._mongodbMock.Object, this._ffMock.Object);
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
  public async Task Watch_If2ItemsAreReceivedFromTheDbWatch_ItShouldCallSetOnTheICacheInstanceTwice()
  {
    this._mongodbMock.Setup(s => s.WatchDb(It.IsAny<string>(), It.IsAny<ResumeData?>(), It.IsAny<CancellationToken>()))
      .Returns((new[] {
        new WatchData { ChangeRecord = new ChangeRecord { Id = "", ChangeType = ChangeRecordTypes.Delete }, ChangeTime = DateTime.Now, ResumeData = new ResumeData{}, Source = new ChangeSource {} },
        new WatchData { ChangeRecord = new ChangeRecord { Id = "", ChangeType = ChangeRecordTypes.Delete }, ChangeTime = DateTime.Now, ResumeData = new ResumeData{}, Source = new ChangeSource {} },
      }).ToAsyncEnumerable());

    await DbStream.Watch(this._cacheMock.Object, this._queueMock.Object, this._mongodbMock.Object, this._ffMock.Object);
    this._cacheMock.Verify(m => m.Set("change_resume_data", It.IsAny<string>(), null), Times.Exactly(2));
  }

  [Fact]
  public async Task Watch_If2ItemsAreReceivedFromTheDbWatch_ItShouldCallSetOnTheICacheInstanceWithTheExpectedFirstResumeData()
  {
    ResumeData expectedResumeData = new ResumeData { ResumeToken = "test resume token", ClusterTime = "test cluster time" };
    this._mongodbMock.Setup(s => s.WatchDb(It.IsAny<string>(), It.IsAny<ResumeData?>(), It.IsAny<CancellationToken>()))
      .Returns((new[] {
        new WatchData { ResumeData = expectedResumeData, ChangeRecord = new ChangeRecord { Id = "", ChangeType = ChangeRecordTypes.Delete }, ChangeTime = DateTime.Now, Source = new ChangeSource {} },
        new WatchData { ChangeRecord = new ChangeRecord { Id = "", ChangeType = ChangeRecordTypes.Delete }, ChangeTime = DateTime.Now, ResumeData = new ResumeData{}, Source = new ChangeSource {} },
      }).ToAsyncEnumerable());

    await DbStream.Watch(this._cacheMock.Object, this._queueMock.Object, this._mongodbMock.Object, this._ffMock.Object);
    Assert.Equal(
      JsonConvert.SerializeObject(expectedResumeData),
      this._cacheMock.Invocations[1].Arguments[1]
    );
  }

  [Fact]
  public async Task Watch_If2ItemsAreReceivedFromTheDbWatch_ItShouldCallSetOnTheICacheInstanceWithTheExpectedSecondResumeData()
  {
    ResumeData expectedResumeData = new ResumeData { ResumeToken = "another test resume token", ClusterTime = "another test cluster time" };
    this._mongodbMock.Setup(s => s.WatchDb(It.IsAny<string>(), It.IsAny<ResumeData?>(), It.IsAny<CancellationToken>()))
      .Returns((new[] {
        new WatchData { ChangeRecord = new ChangeRecord { Id = "", ChangeType = ChangeRecordTypes.Delete }, ChangeTime = DateTime.Now, ResumeData = new ResumeData{}, Source = new ChangeSource {} },
        new WatchData { ResumeData = expectedResumeData, ChangeRecord = new ChangeRecord { Id = "", ChangeType = ChangeRecordTypes.Delete }, ChangeTime = DateTime.Now, Source = new ChangeSource {} },
      }).ToAsyncEnumerable());

    await DbStream.Watch(this._cacheMock.Object, this._queueMock.Object, this._mongodbMock.Object, this._ffMock.Object);
    Assert.Equal(
      JsonConvert.SerializeObject(expectedResumeData),
      this._cacheMock.Invocations[2].Arguments[1]
    );
  }

  [Fact]
  public async Task Watch_IfTheItemReceivedFromTheDbWatchHasANullChangeRecord_ItShouldNotCallEnqueueOnTheICacheInstance()
  {
    this._mongodbMock.Setup(s => s.WatchDb(It.IsAny<string>(), It.IsAny<ResumeData?>(), It.IsAny<CancellationToken>()))
      .Returns((new[] {
        new WatchData { ChangeTime = DateTime.Now, ResumeData = new ResumeData{}, Source = new ChangeSource {} },
      }).ToAsyncEnumerable());

    await DbStream.Watch(this._cacheMock.Object, this._queueMock.Object, this._mongodbMock.Object, this._ffMock.Object);
    this._queueMock.Verify(m => m.Enqueue("mongo_changes", It.IsAny<string[]>()), Times.Never);
  }

  [Fact]
  public async Task Watch_IfTheItemReceivedFromTheDbWatchHasANullChangeRecord_ItShouldCallSetOnTheICacheInstanceWithTheExpectedValue()
  {
    ResumeData expectedResumeData = new ResumeData { ResumeToken = "another test resume token", ClusterTime = "another test cluster time" };
    this._mongodbMock.Setup(s => s.WatchDb(It.IsAny<string>(), It.IsAny<ResumeData?>(), It.IsAny<CancellationToken>()))
      .Returns((new[] {
        new WatchData { ResumeData = expectedResumeData, ChangeTime = DateTime.Now, Source = new ChangeSource {} },
      }).ToAsyncEnumerable());

    await DbStream.Watch(this._cacheMock.Object, this._queueMock.Object, this._mongodbMock.Object, this._ffMock.Object);
    Assert.Equal(
      JsonConvert.SerializeObject(expectedResumeData),
      this._cacheMock.Invocations[1].Arguments[1]
    );
  }

  [Fact]
  public async Task Watch_IfTheCallToGetBoolFlagValueOnTheIFeatureFlagsInstanceReturnsFalse_ItShouldNotCallWatchOnTheIDbInstance()
  {
    this._ffMock.Setup(s => s.GetBoolFlagValue(It.IsAny<string>()))
      .Returns(false);

    await DbStream.Watch(this._cacheMock.Object, this._queueMock.Object, this._mongodbMock.Object, this._ffMock.Object);

    this._mongodbMock.Verify(m => m.WatchDb(It.IsAny<string>(), It.IsAny<ResumeData?>(), It.IsAny<CancellationToken>()), Times.Never());
  }
}