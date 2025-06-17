using System.Net;
using LaunchDarkly.Sdk.Server.Interfaces;
using MongoDB.Bson;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;
using Notification.Types;
using Notification.Utils;
using SharedLibs.Types;
using Toolkit;
using Toolkit.Types;

namespace Notification.Tests.Utils;

[Trait("Type", "Unit")]
public class NotifyTests : IDisposable
{
  private readonly Mock<ICache> _cacheNotifMock;
  private readonly Mock<IQueue> _queueDblistenerMock;
  private readonly Mock<IQueue> _queueNotifMock;
  private readonly Mock<HttpMessageHandler> _httpClientMock;
  private readonly Mock<IDispatchers> _dispatchersMock;
  private readonly Mock<IDispatcher> _webhookDispatcherMock;
  private readonly Mock<IDispatcher> _kafkaDispatcherMock;
  private readonly Mock<ILdClient> _ldClientMock;
  private readonly Mock<ILogger> _loggerMock;
  private readonly LaunchDarkly.Sdk.Context _testLdContext;
  private readonly FeatureFlags _testFeatureFlags;

  public NotifyTests()
  {
    Environment.SetEnvironmentVariable("REDIS_CON_HOST", "test redis con host");
    Environment.SetEnvironmentVariable("REDIS_CON_PORT", "test redis con port");
    Environment.SetEnvironmentVariable("REDIS_PW", "test redis pw");
    Environment.SetEnvironmentVariable("REDIS_CON_HOST_QUEUE", "test redis con host queue");
    Environment.SetEnvironmentVariable("REDIS_CON_PORT_QUEUE", "test redis con port queue");
    Environment.SetEnvironmentVariable("REDIS_PW_QUEUE", "test redis pw queue");
    Environment.SetEnvironmentVariable("DBLISTENER_CHANGES_QUEUE_KEY", "hello world");
    Environment.SetEnvironmentVariable("DISPATCHER_RETRY_QUEUE_KEY", "some queue key");
    Environment.SetEnvironmentVariable("MONGO_COL_NAME", "Entities");
    Environment.SetEnvironmentVariable("LD_NOTIFICATION_ACTIVE_KEY", "test ff key");
    Environment.SetEnvironmentVariable("LD_NOTIFICATION_RETRY_ACTIVE_KEY", "another test ff key");
    Environment.SetEnvironmentVariable("CHANGES_QUEUE_RETRY_COUNT", "5");
    Environment.SetEnvironmentVariable("DISPATCHER_RETRY_COUNT", "10");

    this._cacheNotifMock = new Mock<ICache>(MockBehavior.Strict);
    this._queueDblistenerMock = new Mock<IQueue>(MockBehavior.Strict);
    this._queueNotifMock = new Mock<IQueue>(MockBehavior.Strict);
    this._httpClientMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
    this._dispatchersMock = new Mock<IDispatchers>(MockBehavior.Strict);
    this._webhookDispatcherMock = new Mock<IDispatcher>(MockBehavior.Strict);
    this._kafkaDispatcherMock = new Mock<IDispatcher>(MockBehavior.Strict);
    this._ldClientMock = new Mock<ILdClient>(MockBehavior.Strict);
    this._loggerMock = new Mock<ILogger>(MockBehavior.Strict);
    this._testLdContext = new LaunchDarkly.Sdk.Context { };

    this._cacheNotifMock.Setup(s => s.Set(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
      .Returns(Task.FromResult(true));
    this._cacheNotifMock.Setup(s => s.GetString(It.IsAny<string>()))
      .Returns(Task.FromResult<string?>(JsonConvert.SerializeObject(new NotifConfig[] { new NotifConfig { Protocol = "kafka", TargetURL = "some url" }, })));

    this._queueDblistenerMock.Setup(s => s.Dequeue(It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.FromResult(("some id", JsonConvert.SerializeObject(new ChangeQueueItem { ChangeRecord = JsonConvert.SerializeObject(new ChangeRecord { Id = "", ChangeType = ChangeRecordTypes.Insert }), ChangeTime = DateTime.Now, Source = JsonConvert.SerializeObject(new ChangeSource { }) }))));
    this._queueDblistenerMock.Setup(s => s.Enqueue(It.IsAny<string>(), It.IsAny<string[]>()))
      .Returns(Task.FromResult<string[]>([""]));
    this._queueDblistenerMock.Setup(s => s.Ack(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
      .Returns(Task.FromResult(true));
    this._queueDblistenerMock.Setup(s => s.Nack(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
      .Returns(Task.FromResult(true));

    this._queueNotifMock.Setup(s => s.Dequeue(It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.FromResult(("some other id", JsonConvert.SerializeObject(new ChangeQueueItem { ChangeRecord = JsonConvert.SerializeObject(new ChangeRecord { Id = "", ChangeType = ChangeRecordTypes.Insert }), ChangeTime = DateTime.Now, Source = JsonConvert.SerializeObject(new ChangeSource { }), NotifConfigs = new NotifConfig[] { } }))));
    this._queueNotifMock.Setup(s => s.Enqueue(It.IsAny<string>(), It.IsAny<string[]>()))
      .Returns(Task.FromResult<string[]>([""]));
    this._queueNotifMock.Setup(s => s.Ack(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
      .Returns(Task.FromResult(true));
    this._queueNotifMock.Setup(s => s.Nack(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
      .Returns(Task.FromResult(true));

    HttpContent entityGetResContent = new StringContent(JsonConvert.SerializeObject(new FindResult<dynamic> { }));
    this._httpClientMock.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
      .Returns(Task.FromResult(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = entityGetResContent }));

    this._dispatchersMock.SetupSequence(s => s.GetDispatcher("kafka"))
      .Returns(this._kafkaDispatcherMock.Object);
    this._dispatchersMock.SetupSequence(s => s.GetDispatcher("webhook"))
      .Returns(this._webhookDispatcherMock.Object);

    this._webhookDispatcherMock.Setup(s => s.Dispatch(It.IsAny<NotifData>(), It.IsAny<string>(), It.IsAny<Action<bool>>()))
      .Returns(Task.FromResult(true));

    this._kafkaDispatcherMock.Setup(s => s.Dispatch(It.IsAny<NotifData>(), It.IsAny<string>(), It.IsAny<Action<bool>>()))
      .Returns(Task.FromResult(true));

    this._ldClientMock.Setup(m => m.BoolVariation(It.IsAny<string>(), It.IsAny<LaunchDarkly.Sdk.Context>(), It.IsAny<bool>()))
      .Returns(true);

    this._loggerMock.Setup(s => s.Log(It.IsAny<Microsoft.Extensions.Logging.LogLevel>(), It.IsAny<Exception?>(), It.IsAny<string>()));

    this._testFeatureFlags = new FeatureFlags(new FeatureFlagsInputs
    {
      Client = this._ldClientMock.Object,
      Context = this._testLdContext,
    });
    this._testFeatureFlags.GetBoolFlagValue("test ff key");
    this._testFeatureFlags.GetBoolFlagValue("another test ff key");
  }

  public void Dispose()
  {
    Environment.SetEnvironmentVariable("REDIS_CON_HOST", null);
    Environment.SetEnvironmentVariable("REDIS_CON_PORT", null);
    Environment.SetEnvironmentVariable("REDIS_PW", null);
    Environment.SetEnvironmentVariable("REDIS_CON_HOST_QUEUE", null);
    Environment.SetEnvironmentVariable("REDIS_CON_PORT_QUEUE", null);
    Environment.SetEnvironmentVariable("REDIS_PW_QUEUE", null);
    Environment.SetEnvironmentVariable("DBLISTENER_CHANGES_QUEUE_KEY", null);
    Environment.SetEnvironmentVariable("DISPATCHER_RETRY_QUEUE_KEY", null);
    Environment.SetEnvironmentVariable("MONGO_COL_NAME", null);
    Environment.SetEnvironmentVariable("LD_NOTIFICATION_ACTIVE_KEY", null);
    Environment.SetEnvironmentVariable("LD_NOTIFICATION_RETRY_ACTIVE_KEY", null);
    Environment.SetEnvironmentVariable("CHANGES_QUEUE_RETRY_COUNT", null);
    Environment.SetEnvironmentVariable("DISPATCHER_RETRY_COUNT", null);

    this._cacheNotifMock.Reset();
    this._queueDblistenerMock.Reset();
    this._queueNotifMock.Reset();
    this._httpClientMock.Reset();
    this._dispatchersMock.Reset();
    this._webhookDispatcherMock.Reset();
    this._kafkaDispatcherMock.Reset();
    this._ldClientMock.Reset();
    this._loggerMock.Reset();
  }

  [Fact]
  public async Task ProcessMessage_ItShouldCallDequeueOnTheProvidedIQueueInstanceOnce()
  {
    await Notify.ProcessMessage(this._queueDblistenerMock.Object, this._cacheNotifMock.Object, this._queueNotifMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object), this._loggerMock.Object, NotifyMode.MongoChanges, "test id");
    this._queueDblistenerMock.Verify(m => m.Dequeue("hello world", "thread-MongoChanges-test id"), Times.Once());
  }

  [Fact]
  public async Task ProcessMessage_IfTheFeatureFlagForTheDispatchersBeingActiveIsFalse_ItShouldNotCallDequeueOnTheProvidedIQueueInstance()
  {
    this._ldClientMock.Setup(m => m.BoolVariation("test ff key", this._testLdContext, It.IsAny<bool>()))
      .Returns(false);
    this._testFeatureFlags.GetBoolFlagValue("test ff key");

    await Notify.ProcessMessage(this._queueDblistenerMock.Object, this._cacheNotifMock.Object, this._queueNotifMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object), this._loggerMock.Object, NotifyMode.MongoChanges, "yup");
    this._queueDblistenerMock.Verify(m => m.Dequeue("hello world", "thread-yup"), Times.Never());
  }

  [Fact]
  public async Task ProcessMessage_IfTheFeatureFlagForTheDispatchersBeingActiveIsFalse_ItShouldNotCallGetDispatcherOnTheProvidedIDispatchers()
  {
    this._ldClientMock.Setup(m => m.BoolVariation("test ff key", this._testLdContext, It.IsAny<bool>()))
      .Returns(false);
    this._testFeatureFlags.GetBoolFlagValue("test ff key");

    await Notify.ProcessMessage(this._queueDblistenerMock.Object, this._cacheNotifMock.Object, this._queueNotifMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object), this._loggerMock.Object, NotifyMode.MongoChanges, "");
    this._dispatchersMock.Verify(m => m.GetDispatcher(It.IsAny<string>()), Times.Never());
  }

  [Fact]
  public async Task ProcessMessage_IfThereAreNoMessagesInTheQueue_ItShouldNotCallTheICacheInstance()
  {
    this._queueDblistenerMock.Setup(s => s.Dequeue(It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.FromResult<(string?, string?)>((null, null)));

    await Notify.ProcessMessage(this._queueDblistenerMock.Object, this._cacheNotifMock.Object, this._queueNotifMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object), this._loggerMock.Object, NotifyMode.MongoChanges, "");
    this._cacheNotifMock.Verify(m => m.Set(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()), Times.Never());
    this._cacheNotifMock.Verify(m => m.GetString(It.IsAny<string>()), Times.Never());
  }

  [Fact]
  public async Task ProcessMessage_IfTheSourceCollNameIsEntities_ItShouldCallSetOnTheProvidedICacheInstanceOnceWithTheExpectedData()
  {
    var notifConfigs = new NotifConfig[] {
      new NotifConfig { Protocol = "kafka", TargetURL = "some url" },
    };
    var notifConfigsStr = JsonConvert.SerializeObject(notifConfigs);
    var source = new ChangeSource
    {
      DbName = "test db name",
      CollName = "Entities",
    };
    var changeRecord = new ChangeRecord
    {
      Id = "",
      ChangeType = ChangeRecordTypes.Insert,
      Document = new Dictionary<string, dynamic?> {
        { "name", "test entity name" },
        { "notifConfigs", notifConfigs },
      },
    };
    var change = new ChangeQueueItem
    {
      ChangeTime = DateTime.Now,
      Source = JsonConvert.SerializeObject(source),
      ChangeRecord = JsonConvert.SerializeObject(changeRecord),
    };
    this._queueDblistenerMock.Setup(s => s.Dequeue(It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.FromResult(("random id", JsonConvert.SerializeObject(change))));

    await Notify.ProcessMessage(this._queueDblistenerMock.Object, this._cacheNotifMock.Object, this._queueNotifMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object), this._loggerMock.Object, NotifyMode.MongoChanges, "");
    this._cacheNotifMock.Verify(m => m.Set("entity:test entity name|notif configs", notifConfigsStr, It.IsAny<TimeSpan?>()), Times.Once());
  }

  [Fact]
  public async Task ProcessMessage_IfTheSourceCollNameIsEntities_ItShouldCallAckOnTheProvidedIQueueInstanceOnceWithTheExpectedData()
  {
    var notifConfigs = new NotifConfig[] {
      new NotifConfig { Protocol = "kafka", TargetURL = "some url" },
    };
    var notifConfigsStr = JsonConvert.SerializeObject(notifConfigs);
    var source = new ChangeSource
    {
      DbName = "test db name",
      CollName = "Entities",
    };
    var changeRecord = new ChangeRecord
    {
      Id = "",
      ChangeType = ChangeRecordTypes.Insert,
      Document = new Dictionary<string, dynamic?> {
        { "name", "test entity name" },
        { "notif_configs", notifConfigsStr },
      },
    };
    var change = new ChangeQueueItem
    {
      ChangeTime = DateTime.Now,
      Source = JsonConvert.SerializeObject(source),
      ChangeRecord = JsonConvert.SerializeObject(changeRecord),
    };
    var changeStr = JsonConvert.SerializeObject(change);
    this._queueDblistenerMock.Setup(s => s.Dequeue(It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.FromResult(("hello", changeStr)));

    await Notify.ProcessMessage(this._queueDblistenerMock.Object, this._cacheNotifMock.Object, this._queueNotifMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object), this._loggerMock.Object, NotifyMode.MongoChanges, "");
    this._queueDblistenerMock.Verify(m => m.Ack("hello world", "hello", true), Times.Once());
  }

  [Fact]
  public async Task ProcessMessage_IfTheSourceCollNameIsEntities_IfTheChangeDocumentDoesNotHaveNotifConfigs_ItShouldCallSetOnTheProvidedICacheInstanceWithTheExpectedData()
  {
    var source = new ChangeSource
    {
      DbName = "test db name",
      CollName = "Entities",
    };
    var changeRecord = new ChangeRecord
    {
      Id = "",
      ChangeType = ChangeRecordTypes.Insert,
      Document = new Dictionary<string, dynamic?> {
        { "name", "test entity name" },
      },
    };
    var change = new ChangeQueueItem
    {
      ChangeTime = DateTime.Now,
      Source = JsonConvert.SerializeObject(source),
      ChangeRecord = JsonConvert.SerializeObject(changeRecord),
    };
    this._queueDblistenerMock.Setup(s => s.Dequeue(It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.FromResult(("world", JsonConvert.SerializeObject(change))));

    await Notify.ProcessMessage(this._queueDblistenerMock.Object, this._cacheNotifMock.Object, this._queueNotifMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object), this._loggerMock.Object, NotifyMode.MongoChanges, "");
    this._cacheNotifMock.Verify(m => m.Set("entity:test entity name|notif configs", "\"\"", It.IsAny<TimeSpan?>()), Times.Once());
  }

  [Fact]
  public async Task ProcessMessage_IfTheSourceCollNameIsEntities_IfTheChangeDocumentHasANotifConfigsThatIsTheStringNull_ItShouldCallSetOnTheProvidedICacheInstanceWithTheExpectedData()
  {
    var source = new ChangeSource
    {
      DbName = "test db name",
      CollName = "Entities",
    };
    var changeRecord = new ChangeRecord
    {
      Id = "",
      ChangeType = ChangeRecordTypes.Insert,
      Document = new Dictionary<string, dynamic?> {
        { "name", "test entity name" },
        { "notif_configs", null },
      },
    };
    var change = new ChangeQueueItem
    {
      ChangeTime = DateTime.Now,
      Source = JsonConvert.SerializeObject(source),
      ChangeRecord = JsonConvert.SerializeObject(changeRecord),
    };
    this._queueDblistenerMock.Setup(s => s.Dequeue(It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.FromResult(("bread", JsonConvert.SerializeObject(change))));

    await Notify.ProcessMessage(this._queueDblistenerMock.Object, this._cacheNotifMock.Object, this._queueNotifMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object), this._loggerMock.Object, NotifyMode.MongoChanges, "");
    this._cacheNotifMock.Verify(m => m.Set("entity:test entity name|notif configs", "\"\"", It.IsAny<TimeSpan?>()), Times.Once());
  }

  [Fact]
  public async Task ProcessMessage_IfTheSourceCollNameIsNotEntities_ItShouldCallGetOnTheProvidedICacheInstanceOnceWithTheExpectedData()
  {
    var source = new ChangeSource
    {
      DbName = "some test db name",
      CollName = "Not Entities",
    };
    var changeRecord = new ChangeRecord
    {
      Id = "",
      ChangeType = ChangeRecordTypes.Insert,
      Document = new Dictionary<string, dynamic?> { },
    };
    var change = new ChangeQueueItem
    {
      ChangeTime = DateTime.Now,
      Source = JsonConvert.SerializeObject(source),
      ChangeRecord = JsonConvert.SerializeObject(changeRecord),
    };
    this._queueDblistenerMock.Setup(s => s.Dequeue(It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.FromResult(("butter", JsonConvert.SerializeObject(change))));

    await Notify.ProcessMessage(this._queueDblistenerMock.Object, this._cacheNotifMock.Object, this._queueNotifMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object), this._loggerMock.Object, NotifyMode.MongoChanges, "");
    this._cacheNotifMock.Verify(m => m.GetString("entity:Not Entities|notif configs"), Times.Once());
  }

  [Fact]
  public async Task ProcessMessage_IfTheSourceCollNameIsNotEntities_IfTheEntityOfTheChangeHas2NotifDestinations_ItShouldCallGetDispatcherOnTheProvidedIDispatchersTwice()
  {
    var notifConfigs = new NotifConfig[] {
      new NotifConfig { Protocol = "kafka", TargetURL = "some kafka url" },
      new NotifConfig { Protocol = "webhook", TargetURL = "some webhook url" },
    };
    var notifConfigsStr = JsonConvert.SerializeObject(notifConfigs);
    var source = new ChangeSource
    {
      DbName = "some test db name",
      CollName = "Not Entities",
    };
    var changeRecord = new ChangeRecord
    {
      Id = "",
      ChangeType = ChangeRecordTypes.Insert,
      Document = new Dictionary<string, dynamic?> { },
    };
    var change = new ChangeQueueItem
    {
      ChangeTime = DateTime.Now,
      Source = JsonConvert.SerializeObject(source),
      ChangeRecord = JsonConvert.SerializeObject(changeRecord),
    };
    this._queueDblistenerMock.Setup(s => s.Dequeue(It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.FromResult(("something", JsonConvert.SerializeObject(change))));
    this._cacheNotifMock.Setup(s => s.GetString(It.IsAny<string>()))
      .Returns(Task.FromResult<string?>(notifConfigsStr));

    await Notify.ProcessMessage(this._queueDblistenerMock.Object, this._cacheNotifMock.Object, this._queueNotifMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object), this._loggerMock.Object, NotifyMode.MongoChanges, "");
    this._dispatchersMock.Verify(m => m.GetDispatcher(It.IsAny<string>()), Times.Exactly(2));
  }

  [Fact]
  public async Task ProcessMessage_IfTheSourceCollNameIsNotEntities_IfTheEntityOfTheChangeHas2NotifDestinations_ItShouldCallGetDispatcherOnTheProvidedIDispatchersWithTheExpectedFirstProtocol()
  {
    var notifConfigs = new NotifConfig[] {
      new NotifConfig { Protocol = "kafka", TargetURL = "some kafka url" },
      new NotifConfig { Protocol = "webhook", TargetURL = "some webhook url" },
    };
    var notifConfigsStr = JsonConvert.SerializeObject(notifConfigs);
    var source = new ChangeSource
    {
      DbName = "some test db name",
      CollName = "Not Entities",
    };
    var changeRecord = new ChangeRecord
    {
      Id = "",
      ChangeType = ChangeRecordTypes.Insert,
      Document = new Dictionary<string, dynamic?> { },
    };
    var change = new ChangeQueueItem
    {
      ChangeTime = DateTime.Now,
      Source = JsonConvert.SerializeObject(source),
      ChangeRecord = JsonConvert.SerializeObject(changeRecord),
    };
    this._queueDblistenerMock.Setup(s => s.Dequeue(It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.FromResult(("test test", JsonConvert.SerializeObject(change))));
    this._cacheNotifMock.Setup(s => s.GetString(It.IsAny<string>()))
      .Returns(Task.FromResult<string?>(notifConfigsStr));

    await Notify.ProcessMessage(this._queueDblistenerMock.Object, this._cacheNotifMock.Object, this._queueNotifMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object), this._loggerMock.Object, NotifyMode.MongoChanges, "");
    Assert.Equal(
      "kafka",
      this._dispatchersMock.Invocations[0].Arguments[0]
    );
  }

  [Fact]
  public async Task ProcessMessage_IfTheSourceCollNameIsNotEntities_IfTheEntityOfTheChangeHas2NotifDestinations_ItShouldCallGetDispatcherOnTheProvidedIDispatchersWithTheExpectedSecondProtocol()
  {
    var notifConfigs = new NotifConfig[] {
      new NotifConfig { Protocol = "kafka", TargetURL = "some kafka url" },
      new NotifConfig { Protocol = "webhook", TargetURL = "some webhook url" },
    };
    var notifConfigsStr = JsonConvert.SerializeObject(notifConfigs);
    var source = new ChangeSource
    {
      DbName = "some test db name",
      CollName = "Not Entities",
    };
    var changeRecord = new ChangeRecord
    {
      Id = "",
      ChangeType = ChangeRecordTypes.Insert,
      Document = new Dictionary<string, dynamic?> { },
    };
    var change = new ChangeQueueItem
    {
      ChangeTime = DateTime.Now,
      Source = JsonConvert.SerializeObject(source),
      ChangeRecord = JsonConvert.SerializeObject(changeRecord),
    };
    this._queueDblistenerMock.Setup(s => s.Dequeue(It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.FromResult(("a", JsonConvert.SerializeObject(change))));
    this._cacheNotifMock.Setup(s => s.GetString(It.IsAny<string>()))
      .Returns(Task.FromResult<string?>(notifConfigsStr));

    await Notify.ProcessMessage(this._queueDblistenerMock.Object, this._cacheNotifMock.Object, this._queueNotifMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object), this._loggerMock.Object, NotifyMode.MongoChanges, "");
    Assert.Equal(
      "webhook",
      this._dispatchersMock.Invocations[1].Arguments[0]
    );
  }

  [Fact]
  public async Task ProcessMessage_IfTheSourceCollNameIsNotEntities_IfTheEntityOfTheChangeHas2NotifDestinations_ItShouldCallDispatchOnTheFirstDispatcherOnce()
  {
    var notifConfigs = new NotifConfig[] {
      new NotifConfig { Protocol = "kafka", TargetURL = "some kafka url" },
      new NotifConfig { Protocol = "webhook", TargetURL = "some webhook url" },
    };
    var notifConfigsStr = JsonConvert.SerializeObject(notifConfigs);
    var source = new ChangeSource
    {
      DbName = "some test db name",
      CollName = "Not Entities",
    };
    var changeRecord = new ChangeRecord
    {
      Id = "test doc id",
      ChangeType = ChangeRecordTypes.Insert,
      Document = new Dictionary<string, dynamic?> { },
    };
    var change = new ChangeQueueItem
    {
      ChangeTime = DateTime.Now,
      Source = JsonConvert.SerializeObject(source),
      ChangeRecord = JsonConvert.SerializeObject(changeRecord),
    };
    this._queueDblistenerMock.Setup(s => s.Dequeue(It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.FromResult(("aa", JsonConvert.SerializeObject(change))));
    this._cacheNotifMock.Setup(s => s.GetString(It.IsAny<string>()))
      .Returns(Task.FromResult<string?>(notifConfigsStr));

    NotifData expectedData = new NotifData
    {
      ChangeTime = change.ChangeTime,
      ChangeType = changeRecord.ChangeType.Name,
      Entity = source.CollName,
      EventTime = DateTime.Now,
      Id = changeRecord.Id,
    };
    await Notify.ProcessMessage(this._queueDblistenerMock.Object, this._cacheNotifMock.Object, this._queueNotifMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object), this._loggerMock.Object, NotifyMode.MongoChanges, "");
    this._kafkaDispatcherMock.Verify(m => m.Dispatch(It.IsAny<NotifData>(), "some kafka url", It.IsAny<Action<bool>>()), Times.Once());
  }

  [Fact]
  public async Task ProcessMessage_IfTheSourceCollNameIsNotEntities_IfTheEntityOfTheChangeHas2NotifDestinations_ItShouldCallDispatchOnTheFirstDispatcherOnceWithTheExpectedData()
  {
    var notifConfigs = new NotifConfig[] {
      new NotifConfig { Protocol = "kafka", TargetURL = "some kafka url" },
      new NotifConfig { Protocol = "webhook", TargetURL = "some webhook url" },
    };
    var notifConfigsStr = JsonConvert.SerializeObject(notifConfigs);
    var source = new ChangeSource
    {
      DbName = "some test db name",
      CollName = "Not Entities",
    };
    var changeRecord = new ChangeRecord
    {
      Id = "test doc id",
      ChangeType = ChangeRecordTypes.Insert,
      Document = new Dictionary<string, dynamic?> {
        { "_id", ObjectId.GenerateNewId() },
        { "prop1", true },
        { "hello", "world" },
      },
    };
    var change = new ChangeQueueItem
    {
      ChangeTime = DateTime.Now,
      Source = JsonConvert.SerializeObject(source),
      ChangeRecord = JsonConvert.SerializeObject(changeRecord),
    };
    this._queueDblistenerMock.Setup(s => s.Dequeue(It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.FromResult(("ab", JsonConvert.SerializeObject(change))));
    this._cacheNotifMock.Setup(s => s.GetString(It.IsAny<string>()))
      .Returns(Task.FromResult<string?>(notifConfigsStr));

    NotifData expectedData = new NotifData
    {
      EventTime = DateTime.Now,
      ChangeTime = change.ChangeTime,
      ChangeType = changeRecord.ChangeType.Name,
      Entity = source.CollName,
      Id = changeRecord.Id,
      Document = new Dictionary<string, dynamic?> {
        { "id", changeRecord.Id },
        { "prop1", true },
        { "hello", "world" },
      },
    };
    await Notify.ProcessMessage(this._queueDblistenerMock.Object, this._cacheNotifMock.Object, this._queueNotifMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object), this._loggerMock.Object, NotifyMode.MongoChanges, "");
    dynamic actualData = this._kafkaDispatcherMock.Invocations[0].Arguments[0];

    Assert.InRange(actualData.EventTime, expectedData.EventTime, expectedData.EventTime.AddMilliseconds(100));
    Assert.Equal(expectedData.Document, actualData.Document);

    // Check the rest of the object has the same data
    actualData.EventTime = expectedData.EventTime;
    expectedData.Document = null;
    actualData.Document = null;
    Assert.Equal(JsonConvert.SerializeObject(expectedData), JsonConvert.SerializeObject(actualData));
  }

  [Fact]
  public async Task ProcessMessage_IfTheSourceCollNameIsNotEntities_IfTheEntityOfTheChangeHas2NotifDestinations_InvokingTheCallbackPassedInAs3rdArgumentToTheFirstDispatcherWithTrue_ItShouldLogAnInformation()
  {
    var notifConfigs = new NotifConfig[] {
      new NotifConfig { Protocol = "kafka", TargetURL = "some kafka url" },
      new NotifConfig { Protocol = "webhook", TargetURL = "some webhook url" },
    };
    var notifConfigsStr = JsonConvert.SerializeObject(notifConfigs);
    var source = new ChangeSource
    {
      DbName = "some test db name",
      CollName = "Not Entities",
    };
    var changeRecord = new ChangeRecord
    {
      Id = "test doc id",
      ChangeType = ChangeRecordTypes.Insert,
      Document = new Dictionary<string, dynamic?> {
        { "_id", ObjectId.GenerateNewId() },
        { "prop1", true },
        { "hello", "world" },
      },
    };
    var change = new ChangeQueueItem
    {
      ChangeTime = DateTime.Now,
      Source = JsonConvert.SerializeObject(source),
      ChangeRecord = JsonConvert.SerializeObject(changeRecord),
    };
    this._queueDblistenerMock.Setup(s => s.Dequeue(It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.FromResult(("ab", JsonConvert.SerializeObject(change))));
    this._cacheNotifMock.Setup(s => s.GetString(It.IsAny<string>()))
      .Returns(Task.FromResult<string?>(notifConfigsStr));

    NotifData expectedData = new NotifData
    {
      EventTime = DateTime.Now,
      ChangeTime = change.ChangeTime,
      ChangeType = changeRecord.ChangeType.Name,
      Entity = source.CollName,
      Id = changeRecord.Id,
      Document = new Dictionary<string, dynamic?> {
        { "id", changeRecord.Id },
        { "prop1", true },
        { "hello", "world" },
      },
    };
    await Notify.ProcessMessage(this._queueDblistenerMock.Object, this._cacheNotifMock.Object, this._queueNotifMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object), this._loggerMock.Object, NotifyMode.MongoChanges, "");
    var callback = this._kafkaDispatcherMock.Invocations[0].Arguments[2] as Action<bool>;
    callback(true);

    this._loggerMock.Verify(m => m.Log(Microsoft.Extensions.Logging.LogLevel.Information, null, "Dispatcher sent notification successfully for document id: test doc id"), Times.Once());
  }

  [Fact]
  public async Task ProcessMessage_IfTheSourceCollNameIsNotEntities_IfTheEntityOfTheChangeHas2NotifDestinations_InvokingTheCallbackPassedInAs3rdArgumentToTheFirstDispatcherWithFalse_ItShouldLogAnError()
  {
    var notifConfigs = new NotifConfig[] {
      new NotifConfig { Protocol = "kafka", TargetURL = "some kafka url" },
      new NotifConfig { Protocol = "webhook", TargetURL = "some webhook url" },
    };
    var notifConfigsStr = JsonConvert.SerializeObject(notifConfigs);
    var source = new ChangeSource
    {
      DbName = "some test db name",
      CollName = "Not Entities",
    };
    var changeRecord = new ChangeRecord
    {
      Id = "another test doc id",
      ChangeType = ChangeRecordTypes.Insert,
      Document = new Dictionary<string, dynamic?> {
        { "_id", ObjectId.GenerateNewId() },
        { "prop1", true },
        { "hello", "world" },
      },
    };
    var change = new ChangeQueueItem
    {
      ChangeTime = DateTime.Now,
      Source = JsonConvert.SerializeObject(source),
      ChangeRecord = JsonConvert.SerializeObject(changeRecord),
    };
    this._queueDblistenerMock.Setup(s => s.Dequeue(It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.FromResult(("ab", JsonConvert.SerializeObject(change))));
    this._queueNotifMock.Setup(s => s.Enqueue(It.IsAny<string>(), It.IsAny<string[]>()))
      .Returns(Task.FromResult<string[]>(["inserted message id"]));
    this._cacheNotifMock.Setup(s => s.GetString(It.IsAny<string>()))
      .Returns(Task.FromResult<string?>(notifConfigsStr));

    await Notify.ProcessMessage(this._queueDblistenerMock.Object, this._cacheNotifMock.Object, this._queueNotifMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object), this._loggerMock.Object, NotifyMode.MongoChanges, "");
    var callback = this._kafkaDispatcherMock.Invocations[0].Arguments[2] as Action<bool>;
    callback(false);

    this._loggerMock.Verify(m => m.Log(Microsoft.Extensions.Logging.LogLevel.Error, null, "Dispatcher failed to send notification for document id: 'another test doc id' and dispatcher: 'kafka'. Sent to the retry queue with message id: inserted message id"), Times.Once());
  }

  [Fact]
  public async Task ProcessMessage_IfTheSourceCollNameIsNotEntities_IfTheEntityOfTheChangeHas2NotifDestinations_ItShouldCallDispatchOnTheSecondDispatcherOnce()
  {
    var notifConfigs = new NotifConfig[] {
      new NotifConfig { Protocol = "kafka", TargetURL = "some kafka url" },
      new NotifConfig { Protocol = "webhook", TargetURL = "some webhook url" },
    };
    var notifConfigsStr = JsonConvert.SerializeObject(notifConfigs);
    var source = new ChangeSource
    {
      DbName = "some test db name",
      CollName = "Not Entities",
    };
    var changeRecord = new ChangeRecord
    {
      Id = "test doc id",
      ChangeType = ChangeRecordTypes.Insert,
      Document = new Dictionary<string, dynamic?> { },
    };
    var change = new ChangeQueueItem
    {
      ChangeTime = DateTime.Now,
      Source = JsonConvert.SerializeObject(source),
      ChangeRecord = JsonConvert.SerializeObject(changeRecord),
    };
    this._queueDblistenerMock.Setup(s => s.Dequeue(It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.FromResult(("bb", JsonConvert.SerializeObject(change))));
    this._cacheNotifMock.Setup(s => s.GetString(It.IsAny<string>()))
      .Returns(Task.FromResult<string?>(notifConfigsStr));

    NotifData expectedData = new NotifData
    {
      ChangeTime = change.ChangeTime,
      ChangeType = changeRecord.ChangeType.Name,
      Entity = source.CollName,
      EventTime = DateTime.Now,
      Id = changeRecord.Id,
    };
    await Notify.ProcessMessage(this._queueDblistenerMock.Object, this._cacheNotifMock.Object, this._queueNotifMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object), this._loggerMock.Object, NotifyMode.MongoChanges, "");
    this._webhookDispatcherMock.Verify(m => m.Dispatch(It.IsAny<NotifData>(), "some webhook url", It.IsAny<Action<bool>>()), Times.Once());
  }

  [Fact]
  public async Task ProcessMessage_IfTheSourceCollNameIsNotEntities_IfTheEntityOfTheChangeHas2NotifDestinations_ItShouldCallDispatchOnTheSecondDispatcherOnceWithTheExpectedData()
  {
    var notifConfigs = new NotifConfig[] {
      new NotifConfig { Protocol = "kafka", TargetURL = "some kafka url" },
      new NotifConfig { Protocol = "webhook", TargetURL = "some webhook url" },
    };
    var notifConfigsStr = JsonConvert.SerializeObject(notifConfigs);
    var source = new ChangeSource
    {
      DbName = "some test db name",
      CollName = "Not Entities",
    };
    var changeRecord = new ChangeRecord
    {
      Id = "test doc id",
      ChangeType = ChangeRecordTypes.Insert,
      Document = new Dictionary<string, dynamic?> {
        { "_id", ObjectId.GenerateNewId() },
        { "some", "data"},
      },
    };
    var change = new ChangeQueueItem
    {
      ChangeTime = DateTime.Now,
      Source = JsonConvert.SerializeObject(source),
      ChangeRecord = JsonConvert.SerializeObject(changeRecord),
    };
    this._queueDblistenerMock.Setup(s => s.Dequeue(It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.FromResult(("gsiyd", JsonConvert.SerializeObject(change))));
    this._cacheNotifMock.Setup(s => s.GetString(It.IsAny<string>()))
      .Returns(Task.FromResult<string?>(notifConfigsStr));

    NotifData expectedData = new NotifData
    {
      EventTime = DateTime.Now,
      ChangeTime = change.ChangeTime,
      ChangeType = changeRecord.ChangeType.Name,
      Entity = source.CollName,
      Id = changeRecord.Id,
      Document = new Dictionary<string, dynamic?> {
        { "id", changeRecord.Id },
        { "some", "data"},
      },
    };
    await Notify.ProcessMessage(this._queueDblistenerMock.Object, this._cacheNotifMock.Object, this._queueNotifMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object), this._loggerMock.Object, NotifyMode.MongoChanges, "");
    dynamic actualData = this._webhookDispatcherMock.Invocations[0].Arguments[0];

    Assert.InRange(actualData.EventTime, expectedData.EventTime, expectedData.EventTime.AddMilliseconds(100));
    Assert.Equal(expectedData.Document, actualData.Document);

    // Check the rest of the object has the same data
    actualData.EventTime = expectedData.EventTime;
    expectedData.Document = null;
    actualData.Document = null;
    Assert.Equal(JsonConvert.SerializeObject(expectedData), JsonConvert.SerializeObject(actualData));
  }

  [Fact]
  public async Task ProcessMessage_IfTheSourceCollNameIsNotEntities_IfTheEntityOfTheChangeHas2NotifDestinations_InvokingTheCallbackPassedInAs3rdArgumentToTheSecondDispatcherWithTrue_ItShouldLogAnInformation()
  {
    var notifConfigs = new NotifConfig[] {
      new NotifConfig { Protocol = "kafka", TargetURL = "some kafka url" },
      new NotifConfig { Protocol = "webhook", TargetURL = "some webhook url" },
    };
    var notifConfigsStr = JsonConvert.SerializeObject(notifConfigs);
    var source = new ChangeSource
    {
      DbName = "some test db name",
      CollName = "Not Entities",
    };
    var changeRecord = new ChangeRecord
    {
      Id = "test doc id",
      ChangeType = ChangeRecordTypes.Insert,
      Document = new Dictionary<string, dynamic?> {
        { "_id", ObjectId.GenerateNewId() },
        { "prop1", true },
        { "hello", "world" },
      },
    };
    var change = new ChangeQueueItem
    {
      ChangeTime = DateTime.Now,
      Source = JsonConvert.SerializeObject(source),
      ChangeRecord = JsonConvert.SerializeObject(changeRecord),
    };
    this._queueDblistenerMock.Setup(s => s.Dequeue(It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.FromResult(("ab", JsonConvert.SerializeObject(change))));
    this._cacheNotifMock.Setup(s => s.GetString(It.IsAny<string>()))
      .Returns(Task.FromResult<string?>(notifConfigsStr));

    NotifData expectedData = new NotifData
    {
      EventTime = DateTime.Now,
      ChangeTime = change.ChangeTime,
      ChangeType = changeRecord.ChangeType.Name,
      Entity = source.CollName,
      Id = changeRecord.Id,
      Document = new Dictionary<string, dynamic?> {
        { "id", changeRecord.Id },
        { "prop1", true },
        { "hello", "world" },
      },
    };
    await Notify.ProcessMessage(this._queueDblistenerMock.Object, this._cacheNotifMock.Object, this._queueNotifMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object), this._loggerMock.Object, NotifyMode.MongoChanges, "");
    var callback = this._webhookDispatcherMock.Invocations[0].Arguments[2] as Action<bool>;
    callback(true);

    this._loggerMock.Verify(m => m.Log(Microsoft.Extensions.Logging.LogLevel.Information, null, "Dispatcher sent notification successfully for document id: test doc id"), Times.Once());
  }

  [Fact]
  public async Task ProcessMessage_IfTheSourceCollNameIsNotEntities_IfTheEntityOfTheChangeHas2NotifDestinations_InvokingTheCallbackPassedInAs3rdArgumentToTheSecondDispatcherWithFalse_ItShouldLogAnError()
  {
    var notifConfigs = new NotifConfig[] {
      new NotifConfig { Protocol = "kafka", TargetURL = "some kafka url" },
      new NotifConfig { Protocol = "webhook", TargetURL = "some webhook url" },
    };
    var notifConfigsStr = JsonConvert.SerializeObject(notifConfigs);
    var source = new ChangeSource
    {
      DbName = "some test db name",
      CollName = "Not Entities",
    };
    var changeRecord = new ChangeRecord
    {
      Id = "another test doc id",
      ChangeType = ChangeRecordTypes.Insert,
      Document = new Dictionary<string, dynamic?> {
        { "_id", ObjectId.GenerateNewId() },
        { "prop1", true },
        { "hello", "world" },
      },
    };
    var change = new ChangeQueueItem
    {
      ChangeTime = DateTime.Now,
      Source = JsonConvert.SerializeObject(source),
      ChangeRecord = JsonConvert.SerializeObject(changeRecord),
    };
    this._queueDblistenerMock.Setup(s => s.Dequeue(It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.FromResult(("ab", JsonConvert.SerializeObject(change))));
    this._queueNotifMock.Setup(s => s.Enqueue(It.IsAny<string>(), It.IsAny<string[]>()))
      .Returns(Task.FromResult<string[]>(["some id"]));
    this._cacheNotifMock.Setup(s => s.GetString(It.IsAny<string>()))
      .Returns(Task.FromResult<string?>(notifConfigsStr));

    NotifData expectedData = new NotifData
    {
      EventTime = DateTime.Now,
      ChangeTime = change.ChangeTime,
      ChangeType = changeRecord.ChangeType.Name,
      Entity = source.CollName,
      Id = changeRecord.Id,
      Document = new Dictionary<string, dynamic?> {
        { "id", changeRecord.Id },
        { "prop1", true },
        { "hello", "world" },
      },
    };
    await Notify.ProcessMessage(this._queueDblistenerMock.Object, this._cacheNotifMock.Object, this._queueNotifMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object), this._loggerMock.Object, NotifyMode.MongoChanges, "");
    var callback = this._webhookDispatcherMock.Invocations[0].Arguments[2] as Action<bool>;
    callback(false);

    this._loggerMock.Verify(m => m.Log(Microsoft.Extensions.Logging.LogLevel.Error, null, "Dispatcher failed to send notification for document id: 'another test doc id' and dispatcher: 'webhook'. Sent to the retry queue with message id: some id"), Times.Once());
  }

  [Fact]
  public async Task ProcessMessage_IfTheSourceCollNameIsNotEntities_IfTheEntityOfTheChangeHas2NotifDestinations_ItShouldCallAckOnTheIQueueInstanceOnceWithTheExpectedData()
  {
    var notifConfigs = new NotifConfig[] {
      new NotifConfig { Protocol = "kafka", TargetURL = "some kafka url" },
      new NotifConfig { Protocol = "webhook", TargetURL = "some webhook url" },
    };
    var notifConfigsStr = JsonConvert.SerializeObject(notifConfigs);
    var source = new ChangeSource
    {
      DbName = "some test db name",
      CollName = "Not Entities",
    };
    var changeRecord = new ChangeRecord
    {
      Id = "test doc id",
      ChangeType = ChangeRecordTypes.Insert,
      Document = new Dictionary<string, dynamic?> {
        { "some", "data"},
      },
    };
    var change = new ChangeQueueItem
    {
      ChangeTime = DateTime.Now,
      Source = JsonConvert.SerializeObject(source),
      ChangeRecord = JsonConvert.SerializeObject(changeRecord),
    };
    this._queueDblistenerMock.Setup(s => s.Dequeue(It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.FromResult(("auiyf", JsonConvert.SerializeObject(change))));
    this._cacheNotifMock.Setup(s => s.GetString(It.IsAny<string>()))
      .Returns(Task.FromResult<string?>(notifConfigsStr));

    NotifData expectedData = new NotifData
    {
      EventTime = DateTime.Now,
      ChangeTime = change.ChangeTime,
      ChangeType = changeRecord.ChangeType.Name,
      Entity = source.CollName,
      Id = changeRecord.Id,
      Document = changeRecord.Document,
    };
    await Notify.ProcessMessage(this._queueDblistenerMock.Object, this._cacheNotifMock.Object, this._queueNotifMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object), this._loggerMock.Object, NotifyMode.MongoChanges, "");
    this._queueDblistenerMock.Setup(s => s.Ack("hello world", JsonConvert.SerializeObject(change), true));
  }

  [Fact]
  public async Task ProcessMessage_IfTheSourceCollNameIsNotEntities_IfTheEntityOfTheChangeHasZeroNotifDestinations_ItShouldNotCallGetDispatcherOnTheProvidedIDispatchers()
  {
    var source = new ChangeSource
    {
      DbName = "some test db name",
      CollName = "Not Entities",
    };
    var changeRecord = new ChangeRecord
    {
      Id = "",
      ChangeType = ChangeRecordTypes.Insert,
      Document = new Dictionary<string, dynamic?> { },
    };
    var change = new ChangeQueueItem
    {
      ChangeTime = DateTime.Now,
      Source = JsonConvert.SerializeObject(source),
      ChangeRecord = JsonConvert.SerializeObject(changeRecord),
    };
    this._queueDblistenerMock.Setup(s => s.Dequeue(It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.FromResult(("viufhv", JsonConvert.SerializeObject(change))));
    this._cacheNotifMock.Setup(s => s.GetString(It.IsAny<string>()))
      .Returns(Task.FromResult<string?>(""));

    await Notify.ProcessMessage(this._queueDblistenerMock.Object, this._cacheNotifMock.Object, this._queueNotifMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object), this._loggerMock.Object, NotifyMode.MongoChanges, "");
    this._dispatchersMock.Verify(m => m.GetDispatcher(It.IsAny<string>()), Times.Never());
  }

  [Fact]
  public async Task ProcessMessage_IfTheSourceCollNameIsNotEntities_IfTheEntityOfTheChangeDoesNotExistInTheCache_ItShouldCallSendAsyncFromTheProvidedHttpClientOnceWithTheExpectedMethod()
  {
    var source = new ChangeSource
    {
      DbName = "some test db name",
      CollName = "Not Entities",
    };
    var changeRecord = new ChangeRecord
    {
      Id = "",
      ChangeType = ChangeRecordTypes.Insert,
      Document = new Dictionary<string, dynamic?> { },
    };
    var change = new ChangeQueueItem
    {
      ChangeTime = DateTime.Now,
      Source = JsonConvert.SerializeObject(source),
      ChangeRecord = JsonConvert.SerializeObject(changeRecord),
    };
    this._queueDblistenerMock.Setup(s => s.Dequeue(It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.FromResult(("asdsad", JsonConvert.SerializeObject(change))));
    this._cacheNotifMock.Setup(s => s.GetString(It.IsAny<string>()))
      .Returns(Task.FromResult<string?>(null));
    var testHttpClient = new HttpClient(this._httpClientMock.Object);
    testHttpClient.BaseAddress = new Uri("http://localhost");

    await Notify.ProcessMessage(this._queueDblistenerMock.Object, this._cacheNotifMock.Object, this._queueNotifMock.Object, this._dispatchersMock.Object, testHttpClient, this._loggerMock.Object, NotifyMode.MongoChanges, "");
    Assert.Equal(
      HttpMethod.Get,
      (this._httpClientMock.Invocations[0].Arguments[0] as dynamic).Method
    );
  }

  [Fact]
  public async Task ProcessMessage_IfTheSourceCollNameIsNotEntities_IfTheEntityOfTheChangeDoesNotExistInTheCache_ItShouldCallSendAsyncFromTheProvidedHttpClientOnceWithTheExpectedRequestUri()
  {
    var source = new ChangeSource
    {
      DbName = "some test db name",
      CollName = "Not Entities",
    };
    var changeRecord = new ChangeRecord
    {
      Id = "",
      ChangeType = ChangeRecordTypes.Insert,
      Document = new Dictionary<string, dynamic?> { },
    };
    var change = new ChangeQueueItem
    {
      ChangeTime = DateTime.Now,
      Source = JsonConvert.SerializeObject(source),
      ChangeRecord = JsonConvert.SerializeObject(changeRecord),
    };
    this._queueDblistenerMock.Setup(s => s.Dequeue(It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.FromResult(("uifydsuf", JsonConvert.SerializeObject(change))));
    this._cacheNotifMock.Setup(s => s.GetString(It.IsAny<string>()))
      .Returns(Task.FromResult<string?>(null));
    var testHttpClient = new HttpClient(this._httpClientMock.Object);
    testHttpClient.BaseAddress = new Uri("http://localhost");

    await Notify.ProcessMessage(this._queueDblistenerMock.Object, this._cacheNotifMock.Object, this._queueNotifMock.Object, this._dispatchersMock.Object, testHttpClient, this._loggerMock.Object, NotifyMode.MongoChanges, "");
    Assert.Equal(
      new Uri("http://localhost/v1/entities/?filter={\"name\":\"Not Entities\"}"),
      (this._httpClientMock.Invocations[0].Arguments[0] as dynamic).RequestUri
    );
  }

  [Fact]
  public async Task ProcessMessage_IfTheSourceCollNameIsNotEntities_IfTheEntityOfTheChangeDoesNotExistInTheCache_ItShouldCallSetOnTheProvidedICacheInstanceOnceWithTheExpectedData()
  {
    var source = new ChangeSource
    {
      DbName = "some test db name",
      CollName = "Not Entities",
    };
    var changeRecord = new ChangeRecord
    {
      Id = "",
      ChangeType = ChangeRecordTypes.Insert,
      Document = new Dictionary<string, dynamic?> { },
    };
    var change = new ChangeQueueItem
    {
      ChangeTime = DateTime.Now,
      Source = JsonConvert.SerializeObject(source),
      ChangeRecord = JsonConvert.SerializeObject(changeRecord),
    };
    this._queueDblistenerMock.Setup(s => s.Dequeue(It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.FromResult(("dfouisfg", JsonConvert.SerializeObject(change))));
    this._cacheNotifMock.Setup(s => s.GetString(It.IsAny<string>()))
      .Returns(Task.FromResult<string?>(null));
    var notifConfigs = new NotifConfig[] {
      new NotifConfig { Protocol = "kafka", TargetURL = "some url" },
    };
    var entityGetResData = new FindResult<dynamic>
    {
      Metadata = new FindResultMetadata
      {
        TotalCount = 1,
      },
      Data = new dynamic[] {
        new { Name = "Not Entities", notifConfigs = notifConfigs, },
      },
    };
    HttpContent entityGetResContent = new StringContent(JsonConvert.SerializeObject(entityGetResData));
    this._httpClientMock.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
      .Returns(Task.FromResult(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = entityGetResContent }));
    var testHttpClient = new HttpClient(this._httpClientMock.Object);
    testHttpClient.BaseAddress = new Uri("http://localhost");

    await Notify.ProcessMessage(this._queueDblistenerMock.Object, this._cacheNotifMock.Object, this._queueNotifMock.Object, this._dispatchersMock.Object, testHttpClient, this._loggerMock.Object, NotifyMode.MongoChanges, "");
    this._cacheNotifMock.Verify(m => m.Set("entity:Not Entities|notif configs", JsonConvert.SerializeObject(notifConfigs), It.IsAny<TimeSpan?>()), Times.Once());
  }

  [Fact]
  public async Task ProcessMessage_IfTheSourceCollNameIsNotEntities_IfTheEntityOfTheChangeDoesNotExistInTheCache_IfTheApiCallReturnNoRecords_ItShouldCallSetOnTheProvidedICacheInstanceOnceWithTheExpectedData()
  {
    var source = new ChangeSource
    {
      DbName = "some test db name",
      CollName = "Not Entities",
    };
    var changeRecord = new ChangeRecord
    {
      Id = "",
      ChangeType = ChangeRecordTypes.Insert,
      Document = new Dictionary<string, dynamic?> { },
    };
    var change = new ChangeQueueItem
    {
      ChangeTime = DateTime.Now,
      Source = JsonConvert.SerializeObject(source),
      ChangeRecord = JsonConvert.SerializeObject(changeRecord),
    };
    this._queueDblistenerMock.Setup(s => s.Dequeue(It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.FromResult(("afdoifuu", JsonConvert.SerializeObject(change))));
    this._cacheNotifMock.Setup(s => s.GetString(It.IsAny<string>()))
      .Returns(Task.FromResult<string?>(null));
    var notifConfigs = new NotifConfig[] {
      new NotifConfig { Protocol = "kafka", TargetURL = "some url" },
    };
    var entityGetResData = new FindResult<dynamic>
    {
      Metadata = new FindResultMetadata
      {
        TotalCount = 0,
      },
      Data = new dynamic[] { },
    };
    HttpContent entityGetResContent = new StringContent(JsonConvert.SerializeObject(entityGetResData));
    this._httpClientMock.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
      .Returns(Task.FromResult(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = entityGetResContent }));
    var testHttpClient = new HttpClient(this._httpClientMock.Object);
    testHttpClient.BaseAddress = new Uri("http://localhost");

    await Notify.ProcessMessage(this._queueDblistenerMock.Object, this._cacheNotifMock.Object, this._queueNotifMock.Object, this._dispatchersMock.Object, testHttpClient, this._loggerMock.Object, NotifyMode.MongoChanges, "");
    this._cacheNotifMock.Verify(m => m.Set("entity:Not Entities|notif configs", "\"\"", It.IsAny<TimeSpan?>()), Times.Once());
  }

  [Fact]
  public async Task ProcessMessage_IfTheSourceCollNameIsNotEntities_IfTheEntityOfTheChangeDoesNotExistInTheCache_IfTheEntityOfTheChangeHas1NotifDestinations_ItShouldCallGetDispatcherOnTheProvidedIDispatchersOnceWithTheExpectedArgument()
  {
    var source = new ChangeSource
    {
      DbName = "some test db name",
      CollName = "Not Entities",
    };
    var changeRecord = new ChangeRecord
    {
      Id = "",
      ChangeType = ChangeRecordTypes.Insert,
      Document = new Dictionary<string, dynamic?> { },
    };
    var change = new ChangeQueueItem
    {
      ChangeTime = DateTime.Now,
      Source = JsonConvert.SerializeObject(source),
      ChangeRecord = JsonConvert.SerializeObject(changeRecord),
    };
    this._queueDblistenerMock.Setup(s => s.Dequeue(It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.FromResult(("guihugh", JsonConvert.SerializeObject(change))));
    this._cacheNotifMock.Setup(s => s.GetString(It.IsAny<string>()))
      .Returns(Task.FromResult<string?>(null));
    var notifConfigs = new NotifConfig[] {
      new NotifConfig { Protocol = "webhook", TargetURL = "some url" },
    };
    var entityGetResData = new FindResult<dynamic>
    {
      Metadata = new FindResultMetadata
      {
        TotalCount = 1,
      },
      Data = new dynamic[] {
        new { Name = "Not Entities", notifConfigs = notifConfigs, },
      },
    };
    HttpContent entityGetResContent = new StringContent(JsonConvert.SerializeObject(entityGetResData));
    this._httpClientMock.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
      .Returns(Task.FromResult(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = entityGetResContent }));
    var testHttpClient = new HttpClient(this._httpClientMock.Object);
    testHttpClient.BaseAddress = new Uri("http://localhost");

    await Notify.ProcessMessage(this._queueDblistenerMock.Object, this._cacheNotifMock.Object, this._queueNotifMock.Object, this._dispatchersMock.Object, testHttpClient, this._loggerMock.Object, NotifyMode.MongoChanges, "");
    this._dispatchersMock.Verify(m => m.GetDispatcher("webhook"), Times.Once());
  }

  [Fact]
  public async Task ProcessMessage_IfCallingDequeueOnTheProvidedIQueueInstanceThrowsAnException_ItShouldThrowThatException()
  {
    var testEx = new Exception("ex msg from test");
    this._queueDblistenerMock.Setup(s => s.Dequeue(It.IsAny<string>(), It.IsAny<string>()))
      .Throws(testEx);

    var ex = await Assert.ThrowsAsync<Exception>(async () => await Notify.ProcessMessage(this._queueDblistenerMock.Object, this._cacheNotifMock.Object, this._queueNotifMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object), this._loggerMock.Object, NotifyMode.MongoChanges, ""));
    Assert.Equal(testEx, ex);
  }

  [Fact]
  public async Task ProcessMessage_IfCallingDequeueOnTheProvidedIQueueInstanceThrowsAnException_ItShouldNotCallNackOnTheProvidedIQueueInstance()
  {
    this._queueDblistenerMock.Setup(s => s.Dequeue(It.IsAny<string>(), It.IsAny<string>()))
      .Throws(new Exception("ex msg from test"));

    await Assert.ThrowsAsync<Exception>(async () => await Notify.ProcessMessage(this._queueDblistenerMock.Object, this._cacheNotifMock.Object, this._queueNotifMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object), this._loggerMock.Object, NotifyMode.MongoChanges, ""));
    this._queueDblistenerMock.Verify(m => m.Nack(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never());
  }

  [Fact]
  public async Task ProcessMessage_IfDeserializingTheQueueMessageThrowsAnException_ItShouldCallNackOnTheProvidedIQueueInstanceOnce()
  {
    var change = new ChangeQueueItem
    {
      ChangeTime = DateTime.Now,
      Source = "",
      ChangeRecord = "",
    };
    this._queueDblistenerMock.Setup(s => s.Dequeue(It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.FromResult(("rng id", JsonConvert.SerializeObject(change))));

    await Assert.ThrowsAsync<JsonSerializationException>(async () => await Notify.ProcessMessage(this._queueDblistenerMock.Object, this._cacheNotifMock.Object, this._queueNotifMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object), this._loggerMock.Object, NotifyMode.MongoChanges, ""));
    this._queueDblistenerMock.Verify(m => m.Nack("hello world", "rng id", 5), Times.Once());
  }

  [Fact]
  public async Task ProcessMessage_IfCallingAckOnTheProvidedIQueueInstanceThrowsAnException_ItShouldCallNackOnTheProvidedIQueueInstanceOnce()
  {
    this._queueDblistenerMock.Setup(s => s.Ack(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
      .Throws(new Exception("some error msg from test"));

    await Assert.ThrowsAsync<Exception>(async () => await Notify.ProcessMessage(this._queueDblistenerMock.Object, this._cacheNotifMock.Object, this._queueNotifMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object), this._loggerMock.Object, NotifyMode.MongoChanges, ""));
    this._queueDblistenerMock.Verify(m => m.Nack("hello world", "some id", 5), Times.Once());
  }

  [Fact]
  public async Task ProcessMessage_IfTheSourceCollNameIsEntities_IfCallingSetOnTheProvidedICacheInstanceReturnsFalse_ItShouldLogAnError()
  {
    var notifConfigs = new NotifConfig[] {
      new NotifConfig { Protocol = "kafka", TargetURL = "some url" },
    };
    var notifConfigsStr = JsonConvert.SerializeObject(notifConfigs);
    var source = new ChangeSource
    {
      DbName = "test db name",
      CollName = "Entities",
    };
    var changeRecord = new ChangeRecord
    {
      Id = "",
      ChangeType = ChangeRecordTypes.Insert,
      Document = new Dictionary<string, dynamic?> {
        { "name", "test entity name" },
        { "notifConfigs", notifConfigs },
      },
    };
    var change = new ChangeQueueItem
    {
      ChangeTime = DateTime.Now,
      Source = JsonConvert.SerializeObject(source),
      ChangeRecord = JsonConvert.SerializeObject(changeRecord),
    };
    this._queueDblistenerMock.Setup(s => s.Dequeue(It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.FromResult(("random id", JsonConvert.SerializeObject(change))));

    this._cacheNotifMock.Setup(s => s.Set(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
      .Returns(Task.FromResult(false));

    await Notify.ProcessMessage(this._queueDblistenerMock.Object, this._cacheNotifMock.Object, this._queueNotifMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object), this._loggerMock.Object, NotifyMode.MongoChanges, "");
    this._loggerMock.Verify(m => m.Log(Microsoft.Extensions.Logging.LogLevel.Error, null, "Failed to store in cache the key: 'entity:test entity name|notif configs'"), Times.Once());
  }

  [Fact]
  public async Task ProcessMessage_IfTheSourceCollNameIsNotEntities_IfTheNotifConfigHasADispatcherThatIsNotSupported_ItShouldLogAnErrorAndNotThrowAnyException()
  {
    var notifConfigs = new NotifConfig[] {
      new NotifConfig { Protocol = "something weird", TargetURL = "hello world" },
    };
    var notifConfigsStr = JsonConvert.SerializeObject(notifConfigs);
    var source = new ChangeSource
    {
      DbName = "some test db name",
      CollName = "Not Entities",
    };
    var changeRecord = new ChangeRecord
    {
      Id = "",
      ChangeType = ChangeRecordTypes.Insert,
      Document = new Dictionary<string, dynamic?> { },
    };
    var change = new ChangeQueueItem
    {
      ChangeTime = DateTime.Now,
      Source = JsonConvert.SerializeObject(source),
      ChangeRecord = JsonConvert.SerializeObject(changeRecord),
    };
    this._queueDblistenerMock.Setup(s => s.Dequeue(It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.FromResult(("something", JsonConvert.SerializeObject(change))));
    this._cacheNotifMock.Setup(s => s.GetString(It.IsAny<string>()))
      .Returns(Task.FromResult<string?>(notifConfigsStr));
    var testEx = new ArgumentNullException();
    this._dispatchersMock.SetupSequence(s => s.GetDispatcher(It.IsAny<string>()))
      .Throws(testEx);

    await Notify.ProcessMessage(this._queueDblistenerMock.Object, this._cacheNotifMock.Object, this._queueNotifMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object), this._loggerMock.Object, NotifyMode.MongoChanges, "");
    this._loggerMock.Verify(m => m.Log(
      Microsoft.Extensions.Logging.LogLevel.Warning,
      It.Is<Exception>(e => e.Equals(testEx)),
      It.Is<string>(s => s.Equals(testEx.Message))
    ), Times.Once());
  }

  [Fact]
  public async Task ProcessMessage_IfTheModeIsDispatcherRetry_ItShouldCallDequeueOnTheProvidedIQueueInstanceOnce()
  {
    await Notify.ProcessMessage(this._queueDblistenerMock.Object, this._cacheNotifMock.Object, this._queueNotifMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object), this._loggerMock.Object, NotifyMode.DispatcherRetry, "some id");
    this._queueNotifMock.Verify(m => m.Dequeue("some queue key", "thread-DispatcherRetry-some id"), Times.Once());
  }

  [Fact]
  public async Task ProcessMessage_IfTheModeIsDispatcherRetry_IfTheFeatureFlagForTheDispatchersBeingActiveIsFalse_ItShouldNotCallDequeueOnTheProvidedIQueueInstance()
  {
    this._ldClientMock.Setup(m => m.BoolVariation("another test ff key", this._testLdContext, It.IsAny<bool>()))
      .Returns(false);
    this._testFeatureFlags.GetBoolFlagValue("another test ff key");

    await Notify.ProcessMessage(this._queueDblistenerMock.Object, this._cacheNotifMock.Object, this._queueNotifMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object), this._loggerMock.Object, NotifyMode.DispatcherRetry, "sdiufydiuf");
    this._queueDblistenerMock.Verify(m => m.Dequeue("some queue key", "thread-sdiufydiuf"), Times.Never());
  }

  [Fact]
  public async Task ProcessMessage_IfTheModeIsDispatcherRetry_IfTheFeatureFlagForTheDispatchersBeingActiveIsFalse_ItShouldNotCallGetDispatcherOnTheProvidedIDispatchers()
  {
    this._ldClientMock.Setup(m => m.BoolVariation("another test ff key", this._testLdContext, It.IsAny<bool>()))
      .Returns(false);
    this._testFeatureFlags.GetBoolFlagValue("another test ff key");

    await Notify.ProcessMessage(this._queueDblistenerMock.Object, this._cacheNotifMock.Object, this._queueNotifMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object), this._loggerMock.Object, NotifyMode.DispatcherRetry, "");
    this._dispatchersMock.Verify(m => m.GetDispatcher(It.IsAny<string>()), Times.Never());
  }

  [Fact]
  public async Task ProcessMessage_IfTheModeIsDispatcherRetry_IfCallingDequeueOnTheProvidedIQueueInstanceThrowsAnException_ItShouldNotCallNackOnTheProvidedIQueueInstance()
  {
    this._queueNotifMock.Setup(s => s.Dequeue(It.IsAny<string>(), It.IsAny<string>()))
      .Throws(new Exception("ex msg from test"));

    await Assert.ThrowsAsync<Exception>(async () => await Notify.ProcessMessage(this._queueDblistenerMock.Object, this._cacheNotifMock.Object, this._queueNotifMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object), this._loggerMock.Object, NotifyMode.DispatcherRetry, ""));
    this._queueNotifMock.Verify(m => m.Nack(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never());
  }

  [Fact]
  public async Task ProcessMessage_IfTheModeIsDispatcherRetry_IfDeserializingTheQueueMessageThrowsAnException_ItShouldCallNackOnTheProvidedIQueueInstanceOnce()
  {
    var change = new ChangeQueueItem
    {
      ChangeTime = DateTime.Now,
      Source = "",
      ChangeRecord = "",
    };
    this._queueNotifMock.Setup(s => s.Dequeue(It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.FromResult(("rng id", JsonConvert.SerializeObject(change))));

    await Assert.ThrowsAsync<JsonSerializationException>(async () => await Notify.ProcessMessage(this._queueDblistenerMock.Object, this._cacheNotifMock.Object, this._queueNotifMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object), this._loggerMock.Object, NotifyMode.DispatcherRetry, ""));
    this._queueNotifMock.Verify(m => m.Nack("some queue key", "rng id", 10), Times.Once());
  }

  [Fact]
  public async Task ProcessMessage_IfTheSourceCollNameIsNotEntities_InvokingTheCallbackPassedInAs3rdArgumentToTheDispatcherWithFalse_ItShouldCallEnqueueOnTheProvidedIQueueInstanceOnce()
  {
    var notifConfigs = new NotifConfig[] {
      new NotifConfig { Protocol = "kafka", TargetURL = "some url" },
      new NotifConfig { Protocol = "webhook", TargetURL = "some webhook url" },
    };
    var notifConfigsStr = JsonConvert.SerializeObject(notifConfigs);
    var source = new ChangeSource
    {
      DbName = "some test db name",
      CollName = "Not Entities",
    };
    var changeRecord = new ChangeRecord
    {
      Id = "some doc id",
      ChangeType = ChangeRecordTypes.Insert,
      Document = new Dictionary<string, dynamic?> {
        { "_id", ObjectId.GenerateNewId() },
        { "prop1", true },
        { "hello", "world" },
      },
    };
    var change = new ChangeQueueItem
    {
      ChangeTime = DateTime.Now,
      Source = JsonConvert.SerializeObject(source),
      ChangeRecord = JsonConvert.SerializeObject(changeRecord),
    };
    this._queueDblistenerMock.Setup(s => s.Dequeue(It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.FromResult(("ab", JsonConvert.SerializeObject(change))));
    this._cacheNotifMock.Setup(s => s.GetString(It.IsAny<string>()))
      .Returns(Task.FromResult<string?>(notifConfigsStr));

    await Notify.ProcessMessage(this._queueDblistenerMock.Object, this._cacheNotifMock.Object, this._queueNotifMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object), this._loggerMock.Object, NotifyMode.MongoChanges, "");
    var kafkaCallback = this._webhookDispatcherMock.Invocations[0].Arguments[2] as Action<bool>;
    kafkaCallback(true);
    var webhookCallback = this._webhookDispatcherMock.Invocations[0].Arguments[2] as Action<bool>;
    webhookCallback(false);

    var expectedItem = new ChangeQueueItem
    {
      ChangeRecord = change.ChangeRecord,
      ChangeTime = change.ChangeTime,
      Source = change.Source,
      NotifConfigs = [
        new NotifConfig { Protocol = "webhook", TargetURL = "some webhook url" },
      ],
    };

    this._queueNotifMock.Verify(m => m.Enqueue("some queue key", new string[] { JsonConvert.SerializeObject(expectedItem) }), Times.Once());
  }

  [Fact]
  public async Task ProcessMessage_IfTheModeIsDispatcherRetry_IfTheSourceCollNameIsNotEntities_InvokingTheCallbackPassedInAs3rdArgumentToTheDispatcherWithFalse_ItShouldNotCallEnqueueOnTheProvidedIQueueInstance()
  {
    var notifConfigs = new NotifConfig[] {
      new NotifConfig { Protocol = "webhook", TargetURL = "some webhook url" },
    };
    var source = new ChangeSource
    {
      DbName = "some test db name",
      CollName = "Not Entities",
    };
    var changeRecord = new ChangeRecord
    {
      Id = "some doc id",
      ChangeType = ChangeRecordTypes.Insert,
      Document = new Dictionary<string, dynamic?> {
        { "_id", ObjectId.GenerateNewId() },
        { "prop1", true },
        { "hello", "world" },
      },
    };
    var change = new ChangeQueueItem
    {
      ChangeTime = DateTime.Now,
      Source = JsonConvert.SerializeObject(source),
      ChangeRecord = JsonConvert.SerializeObject(changeRecord),
      NotifConfigs = notifConfigs,
    };
    this._queueNotifMock.Setup(s => s.Dequeue(It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.FromResult(("ab", JsonConvert.SerializeObject(change))));

    await Notify.ProcessMessage(this._queueDblistenerMock.Object, this._cacheNotifMock.Object, this._queueNotifMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object), this._loggerMock.Object, NotifyMode.DispatcherRetry, "");
    var webhookCallback = this._webhookDispatcherMock.Invocations[0].Arguments[2] as Action<bool>;
    webhookCallback(false);

    var expectedItem = new ChangeQueueItem
    {
      ChangeRecord = change.ChangeRecord,
      ChangeTime = change.ChangeTime,
      Source = change.Source,
      NotifConfigs = [
        new NotifConfig { Protocol = "webhook", TargetURL = "some webhook url" },
      ],
    };

    this._queueDblistenerMock.Verify(m => m.Enqueue(It.IsAny<string>(), It.IsAny<string[]>()), Times.Never());
  }

  [Fact]
  public async Task ProcessMessage_IfTheModeIsDispatcherRetry_IfTheSourceCollNameIsNotEntities_InvokingTheCallbackPassedInAs3rdArgumentToTheDispatcherWithFalse_ItShouldNotCallAckOnTheProvidedIQueueInstance()
  {
    var notifConfigs = new NotifConfig[] {
      new NotifConfig { Protocol = "webhook", TargetURL = "some webhook url" },
    };
    var source = new ChangeSource
    {
      DbName = "some test db name",
      CollName = "Not Entities",
    };
    var changeRecord = new ChangeRecord
    {
      Id = "some doc id",
      ChangeType = ChangeRecordTypes.Insert,
      Document = new Dictionary<string, dynamic?> {
        { "_id", ObjectId.GenerateNewId() },
        { "prop1", true },
        { "hello", "world" },
      },
    };
    var change = new ChangeQueueItem
    {
      ChangeTime = DateTime.Now,
      Source = JsonConvert.SerializeObject(source),
      ChangeRecord = JsonConvert.SerializeObject(changeRecord),
      NotifConfigs = notifConfigs,
    };
    this._queueNotifMock.Setup(s => s.Dequeue(It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.FromResult(("test msg id", JsonConvert.SerializeObject(change))));

    await Notify.ProcessMessage(this._queueDblistenerMock.Object, this._cacheNotifMock.Object, this._queueNotifMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object), this._loggerMock.Object, NotifyMode.DispatcherRetry, "");
    var webhookCallback = this._webhookDispatcherMock.Invocations[0].Arguments[2] as Action<bool>;
    webhookCallback(false);

    var expectedItem = new ChangeQueueItem
    {
      ChangeRecord = change.ChangeRecord,
      ChangeTime = change.ChangeTime,
      Source = change.Source,
      NotifConfigs = [
        new NotifConfig { Protocol = "webhook", TargetURL = "some webhook url" },
      ],
    };

    this._queueDblistenerMock.Verify(m => m.Ack(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never());
  }

  [Fact]
  public async Task ProcessMessage_IfTheModeIsDispatcherRetry_IfTheSourceCollNameIsNotEntities_InvokingTheCallbackPassedInAs3rdArgumentToTheDispatcherWithFalse_ItShouldLogAnError()
  {
    var notifConfigs = new NotifConfig[] {
      new NotifConfig { Protocol = "webhook", TargetURL = "some webhook url" },
    };
    var source = new ChangeSource
    {
      DbName = "some test db name",
      CollName = "Not Entities",
    };
    var changeRecord = new ChangeRecord
    {
      Id = "some doc id",
      ChangeType = ChangeRecordTypes.Insert,
      Document = new Dictionary<string, dynamic?> {
        { "_id", ObjectId.GenerateNewId() },
        { "prop1", true },
        { "hello", "world" },
      },
    };
    var change = new ChangeQueueItem
    {
      ChangeTime = DateTime.Now,
      Source = JsonConvert.SerializeObject(source),
      ChangeRecord = JsonConvert.SerializeObject(changeRecord),
      NotifConfigs = notifConfigs,
    };
    this._queueNotifMock.Setup(s => s.Dequeue(It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.FromResult(("dsfiuydfu", JsonConvert.SerializeObject(change))));

    await Notify.ProcessMessage(this._queueDblistenerMock.Object, this._cacheNotifMock.Object, this._queueNotifMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object), this._loggerMock.Object, NotifyMode.DispatcherRetry, "");
    var webhookCallback = this._webhookDispatcherMock.Invocations[0].Arguments[2] as Action<bool>;
    webhookCallback(false);

    var expectedItem = new ChangeQueueItem
    {
      ChangeRecord = change.ChangeRecord,
      ChangeTime = change.ChangeTime,
      Source = change.Source,
      NotifConfigs = [
        new NotifConfig { Protocol = "webhook", TargetURL = "some webhook url" },
      ],
    };

    this._loggerMock.Verify(m => m.Log(Microsoft.Extensions.Logging.LogLevel.Error, null, "Dispatcher failed to send notification for document id: 'some doc id' and dispatcher: 'webhook'. Nacked the message with id: dsfiuydfu"), Times.Once());
  }

  [Fact]
  public async Task ProcessMessage_IfTheModeIsDispatcherRetry_IfTheSourceCollNameIsNotEntities_ItShouldNotCallGetStringOnTheProvidedICacheInstance()
  {
    var notifConfigs = new NotifConfig[] {
      new NotifConfig { Protocol = "kafka", TargetURL = "some kafka topic" },
    };
    var source = new ChangeSource
    {
      DbName = "some test db name",
      CollName = "Not Entities",
    };
    var changeRecord = new ChangeRecord
    {
      Id = "some doc id",
      ChangeType = ChangeRecordTypes.Insert,
      Document = new Dictionary<string, dynamic?> {
        { "_id", ObjectId.GenerateNewId() },
        { "prop1", true },
        { "hello", "world" },
      },
    };
    var change = new ChangeQueueItem
    {
      ChangeTime = DateTime.Now,
      Source = JsonConvert.SerializeObject(source),
      ChangeRecord = JsonConvert.SerializeObject(changeRecord),
      NotifConfigs = notifConfigs,
    };
    this._queueNotifMock.Setup(s => s.Dequeue(It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.FromResult(("dsfiuydfu", JsonConvert.SerializeObject(change))));

    await Notify.ProcessMessage(this._queueDblistenerMock.Object, this._cacheNotifMock.Object, this._queueNotifMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object), this._loggerMock.Object, NotifyMode.DispatcherRetry, "");

    this._cacheNotifMock.Verify(m => m.GetString(It.IsAny<string>()), Times.Never());
  }

  [Fact]
  public async Task ProcessMessage_IfTheModeIsDispatcherRetry_IfTheSourceCollNameIsNotEntities_ItShouldNotCallSendAsyncFromTheProvidedHttpClient()
  {
    var notifConfigs = new NotifConfig[] {
      new NotifConfig { Protocol = "kafka", TargetURL = "some kafka topic" },
    };
    var source = new ChangeSource
    {
      DbName = "some test db name",
      CollName = "Not Entities",
    };
    var changeRecord = new ChangeRecord
    {
      Id = "some doc id",
      ChangeType = ChangeRecordTypes.Insert,
      Document = new Dictionary<string, dynamic?> {
        { "_id", ObjectId.GenerateNewId() },
        { "prop1", true },
        { "hello", "world" },
      },
    };
    var change = new ChangeQueueItem
    {
      ChangeTime = DateTime.Now,
      Source = JsonConvert.SerializeObject(source),
      ChangeRecord = JsonConvert.SerializeObject(changeRecord),
      NotifConfigs = notifConfigs,
    };
    this._queueNotifMock.Setup(s => s.Dequeue(It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.FromResult(("dsfiuydfu", JsonConvert.SerializeObject(change))));

    await Notify.ProcessMessage(this._queueDblistenerMock.Object, this._cacheNotifMock.Object, this._queueNotifMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object), this._loggerMock.Object, NotifyMode.DispatcherRetry, "");

    this._httpClientMock.Protected().Verify("SendAsync", Times.Never(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
  }
}