using System.Net;
using LaunchDarkly.Sdk.Server.Interfaces;
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
  private readonly Mock<ICache> _cacheMock;
  private readonly Mock<IQueue> _queueMock;
  private readonly Mock<HttpMessageHandler> _httpClientMock;
  private readonly Mock<IDispatchers> _dispatchersMock;
  private readonly Mock<IDispatcher> _webhookDispatcherMock;
  private readonly Mock<IDispatcher> _kafkaDispatcherMock;
  private readonly Mock<ILdClient> _ldClientMock;
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
    Environment.SetEnvironmentVariable("DBLISTENER_CACHE_CHANGES_QUEUE_KEY", "mongo_changes");
    Environment.SetEnvironmentVariable("MONGO_COL_NAME", "Entities");
    Environment.SetEnvironmentVariable("LD_NOTIFICATION_ACTIVE_KEY", "test ff key");

    this._cacheMock = new Mock<ICache>(MockBehavior.Strict);
    this._queueMock = new Mock<IQueue>(MockBehavior.Strict);
    this._httpClientMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
    this._dispatchersMock = new Mock<IDispatchers>(MockBehavior.Strict);
    this._webhookDispatcherMock = new Mock<IDispatcher>(MockBehavior.Strict);
    this._kafkaDispatcherMock = new Mock<IDispatcher>(MockBehavior.Strict);
    this._ldClientMock = new Mock<ILdClient>(MockBehavior.Strict);
    this._testLdContext = new LaunchDarkly.Sdk.Context { };

    this._cacheMock.Setup(s => s.Set(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
      .Returns(Task.FromResult(true));
    this._cacheMock.Setup(s => s.GetString(It.IsAny<string>()))
      .Returns(Task.FromResult<string?>(""));

    this._queueMock.Setup(s => s.Dequeue(It.IsAny<string>()))
      .Returns(Task.FromResult(JsonConvert.SerializeObject(new ChangeQueueItem { ChangeRecord = "", ChangeTime = DateTime.Now, Source = "" })));
    this._queueMock.Setup(s => s.Ack(It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.FromResult(true));

    this._httpClientMock.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
      .Returns(Task.FromResult(new HttpResponseMessage { StatusCode = HttpStatusCode.OK }));

    this._dispatchersMock.SetupSequence(s => s.GetDispatcher(It.IsAny<string>()))
      .Returns(this._kafkaDispatcherMock.Object)
      .Returns(this._webhookDispatcherMock.Object);

    this._webhookDispatcherMock.Setup(s => s.Dispatch(It.IsAny<NotifData>(), It.IsAny<string>(), It.IsAny<Action<bool>>()))
      .Returns(Task.FromResult(true));

    this._kafkaDispatcherMock.Setup(s => s.Dispatch(It.IsAny<NotifData>(), It.IsAny<string>(), It.IsAny<Action<bool>>()))
      .Returns(Task.FromResult(true));

    this._ldClientMock.Setup(m => m.BoolVariation("test ff key", this._testLdContext, It.IsAny<bool>()))
      .Returns(true);

    this._testFeatureFlags = new FeatureFlags(new Toolkit.Types.FeatureFlagsInputs
    {
      Client = this._ldClientMock.Object,
      Context = this._testLdContext,
    });
    this._testFeatureFlags.GetBoolFlagValue("test ff key");
  }

  public void Dispose()
  {
    Environment.SetEnvironmentVariable("REDIS_CON_HOST", null);
    Environment.SetEnvironmentVariable("REDIS_CON_PORT", null);
    Environment.SetEnvironmentVariable("REDIS_PW", null);
    Environment.SetEnvironmentVariable("REDIS_CON_HOST_QUEUE", null);
    Environment.SetEnvironmentVariable("REDIS_CON_PORT_QUEUE", null);
    Environment.SetEnvironmentVariable("REDIS_PW_QUEUE", null);
    Environment.SetEnvironmentVariable("DBLISTENER_CACHE_CHANGES_QUEUE_KEY", null);
    Environment.SetEnvironmentVariable("MONGO_COL_NAME", null);
    Environment.SetEnvironmentVariable("LD_NOTIFICATION_ACTIVE_KEY", null);

    this._cacheMock.Reset();
    this._queueMock.Reset();
    this._httpClientMock.Reset();
    this._dispatchersMock.Reset();
    this._webhookDispatcherMock.Reset();
    this._kafkaDispatcherMock.Reset();
    this._ldClientMock.Reset();
  }

  [Fact]
  public async Task ProcessMessage_ItShouldCallDequeueOnTheProvidedIQueueInstanceOnce()
  {
    await Notify.ProcessMessage(this._queueMock.Object, this._cacheMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object));
    this._queueMock.Verify(m => m.Dequeue("mongo_changes"), Times.Once());
  }

  [Fact]
  public async Task ProcessMessage_IfTheFeatureFlagForTheDispatchersBeingActiveIsFalse_ItShouldNotCallDequeueOnTheProvidedIQueueInstance()
  {
    this._ldClientMock.Setup(m => m.BoolVariation("test ff key", this._testLdContext, It.IsAny<bool>()))
      .Returns(false);
    this._testFeatureFlags.GetBoolFlagValue("test ff key");

    await Notify.ProcessMessage(this._queueMock.Object, this._cacheMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object));
    this._queueMock.Verify(m => m.Dequeue("mongo_changes"), Times.Never());
  }

  [Fact]
  public async Task ProcessMessage_IfTheFeatureFlagForTheDispatchersBeingActiveIsFalse_ItShouldNotCallGetDispatcherOnTheProvidedIDispatchers()
  {
    this._ldClientMock.Setup(m => m.BoolVariation("test ff key", this._testLdContext, It.IsAny<bool>()))
      .Returns(false);
    this._testFeatureFlags.GetBoolFlagValue("test ff key");

    await Notify.ProcessMessage(this._queueMock.Object, this._cacheMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object));
    this._dispatchersMock.Verify(m => m.GetDispatcher(It.IsAny<string>()), Times.Never());
  }

  [Fact]
  public async Task ProcessMessage_IfThereAreNoMessagesInTheQueue_ItShouldNotCallTheICacheInstance()
  {
    this._queueMock.Setup(s => s.Dequeue(It.IsAny<string>()))
      .Returns(Task.FromResult(""));

    await Notify.ProcessMessage(this._queueMock.Object, this._cacheMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object));
    this._cacheMock.Verify(m => m.Set(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()), Times.Never());
    this._cacheMock.Verify(m => m.GetString(It.IsAny<string>()), Times.Never());
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
        { "notifConfigs", notifConfigsStr },
      },
    };
    var change = new ChangeQueueItem
    {
      ChangeTime = DateTime.Now,
      Source = JsonConvert.SerializeObject(source),
      ChangeRecord = JsonConvert.SerializeObject(changeRecord),
    };
    this._queueMock.Setup(s => s.Dequeue(It.IsAny<string>()))
      .Returns(Task.FromResult(JsonConvert.SerializeObject(change)));

    await Notify.ProcessMessage(this._queueMock.Object, this._cacheMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object));
    this._cacheMock.Verify(m => m.Set("entity:test entity name|notif configs", notifConfigsStr, It.IsAny<TimeSpan?>()), Times.Once());
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
    this._queueMock.Setup(s => s.Dequeue(It.IsAny<string>()))
      .Returns(Task.FromResult(changeStr));

    await Notify.ProcessMessage(this._queueMock.Object, this._cacheMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object));
    this._queueMock.Verify(m => m.Ack("mongo_changes", changeStr), Times.Once());
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
    this._queueMock.Setup(s => s.Dequeue(It.IsAny<string>()))
      .Returns(Task.FromResult(JsonConvert.SerializeObject(change)));

    await Notify.ProcessMessage(this._queueMock.Object, this._cacheMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object));
    this._cacheMock.Verify(m => m.Set("entity:test entity name|notif configs", "", It.IsAny<TimeSpan?>()), Times.Once());
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
        { "notif_configs", "null" },
      },
    };
    var change = new ChangeQueueItem
    {
      ChangeTime = DateTime.Now,
      Source = JsonConvert.SerializeObject(source),
      ChangeRecord = JsonConvert.SerializeObject(changeRecord),
    };
    this._queueMock.Setup(s => s.Dequeue(It.IsAny<string>()))
      .Returns(Task.FromResult(JsonConvert.SerializeObject(change)));

    await Notify.ProcessMessage(this._queueMock.Object, this._cacheMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object));
    this._cacheMock.Verify(m => m.Set("entity:test entity name|notif configs", "", It.IsAny<TimeSpan?>()), Times.Once());
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
    this._queueMock.Setup(s => s.Dequeue(It.IsAny<string>()))
      .Returns(Task.FromResult(JsonConvert.SerializeObject(change)));

    await Notify.ProcessMessage(this._queueMock.Object, this._cacheMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object));
    this._cacheMock.Verify(m => m.GetString("entity:\"Not Entities\"|notif configs"), Times.Once());
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
    this._queueMock.Setup(s => s.Dequeue(It.IsAny<string>()))
      .Returns(Task.FromResult(JsonConvert.SerializeObject(change)));
    this._cacheMock.Setup(s => s.GetString(It.IsAny<string>()))
      .Returns(Task.FromResult<string?>(notifConfigsStr));

    await Notify.ProcessMessage(this._queueMock.Object, this._cacheMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object));
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
    this._queueMock.Setup(s => s.Dequeue(It.IsAny<string>()))
      .Returns(Task.FromResult(JsonConvert.SerializeObject(change)));
    this._cacheMock.Setup(s => s.GetString(It.IsAny<string>()))
      .Returns(Task.FromResult<string?>(notifConfigsStr));

    await Notify.ProcessMessage(this._queueMock.Object, this._cacheMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object));
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
    this._queueMock.Setup(s => s.Dequeue(It.IsAny<string>()))
      .Returns(Task.FromResult(JsonConvert.SerializeObject(change)));
    this._cacheMock.Setup(s => s.GetString(It.IsAny<string>()))
      .Returns(Task.FromResult<string?>(notifConfigsStr));

    await Notify.ProcessMessage(this._queueMock.Object, this._cacheMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object));
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
    this._queueMock.Setup(s => s.Dequeue(It.IsAny<string>()))
      .Returns(Task.FromResult(JsonConvert.SerializeObject(change)));
    this._cacheMock.Setup(s => s.GetString(It.IsAny<string>()))
      .Returns(Task.FromResult<string?>(notifConfigsStr));

    NotifData expectedData = new NotifData
    {
      ChangeTime = change.ChangeTime,
      ChangeType = changeRecord.ChangeType.Name,
      Entity = source.CollName,
      EventTime = DateTime.Now,
      Id = changeRecord.Id,
    };
    await Notify.ProcessMessage(this._queueMock.Object, this._cacheMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object));
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
    this._queueMock.Setup(s => s.Dequeue(It.IsAny<string>()))
      .Returns(Task.FromResult(JsonConvert.SerializeObject(change)));
    this._cacheMock.Setup(s => s.GetString(It.IsAny<string>()))
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
    await Notify.ProcessMessage(this._queueMock.Object, this._cacheMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object));
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
    this._queueMock.Setup(s => s.Dequeue(It.IsAny<string>()))
      .Returns(Task.FromResult(JsonConvert.SerializeObject(change)));
    this._cacheMock.Setup(s => s.GetString(It.IsAny<string>()))
      .Returns(Task.FromResult<string?>(notifConfigsStr));

    NotifData expectedData = new NotifData
    {
      ChangeTime = change.ChangeTime,
      ChangeType = changeRecord.ChangeType.Name,
      Entity = source.CollName,
      EventTime = DateTime.Now,
      Id = changeRecord.Id,
    };
    await Notify.ProcessMessage(this._queueMock.Object, this._cacheMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object));
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
        { "some", "data"},
      },
    };
    var change = new ChangeQueueItem
    {
      ChangeTime = DateTime.Now,
      Source = JsonConvert.SerializeObject(source),
      ChangeRecord = JsonConvert.SerializeObject(changeRecord),
    };
    this._queueMock.Setup(s => s.Dequeue(It.IsAny<string>()))
      .Returns(Task.FromResult(JsonConvert.SerializeObject(change)));
    this._cacheMock.Setup(s => s.GetString(It.IsAny<string>()))
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
    await Notify.ProcessMessage(this._queueMock.Object, this._cacheMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object));
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
    this._queueMock.Setup(s => s.Dequeue(It.IsAny<string>()))
      .Returns(Task.FromResult(JsonConvert.SerializeObject(change)));
    this._cacheMock.Setup(s => s.GetString(It.IsAny<string>()))
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
    await Notify.ProcessMessage(this._queueMock.Object, this._cacheMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object));
    this._queueMock.Setup(s => s.Ack("mongo_changes", JsonConvert.SerializeObject(change)));
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
    this._queueMock.Setup(s => s.Dequeue(It.IsAny<string>()))
      .Returns(Task.FromResult(JsonConvert.SerializeObject(change)));
    this._cacheMock.Setup(s => s.GetString(It.IsAny<string>()))
      .Returns(Task.FromResult<string?>(""));

    await Notify.ProcessMessage(this._queueMock.Object, this._cacheMock.Object, this._dispatchersMock.Object, new HttpClient(this._httpClientMock.Object));
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
    this._queueMock.Setup(s => s.Dequeue(It.IsAny<string>()))
      .Returns(Task.FromResult(JsonConvert.SerializeObject(change)));
    this._cacheMock.Setup(s => s.GetString(It.IsAny<string>()))
      .Returns(Task.FromResult<string?>(null));
    var testHttpClient = new HttpClient(this._httpClientMock.Object);
    testHttpClient.BaseAddress = new Uri("http://localhost");

    await Notify.ProcessMessage(this._queueMock.Object, this._cacheMock.Object, this._dispatchersMock.Object, testHttpClient);
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
    this._queueMock.Setup(s => s.Dequeue(It.IsAny<string>()))
      .Returns(Task.FromResult(JsonConvert.SerializeObject(change)));
    this._cacheMock.Setup(s => s.GetString(It.IsAny<string>()))
      .Returns(Task.FromResult<string?>(null));
    var testHttpClient = new HttpClient(this._httpClientMock.Object);
    testHttpClient.BaseAddress = new Uri("http://localhost");

    await Notify.ProcessMessage(this._queueMock.Object, this._cacheMock.Object, this._dispatchersMock.Object, testHttpClient);
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
    this._queueMock.Setup(s => s.Dequeue(It.IsAny<string>()))
      .Returns(Task.FromResult(JsonConvert.SerializeObject(change)));
    this._cacheMock.Setup(s => s.GetString(It.IsAny<string>()))
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

    await Notify.ProcessMessage(this._queueMock.Object, this._cacheMock.Object, this._dispatchersMock.Object, testHttpClient);
    this._cacheMock.Verify(m => m.Set("entity:\"Not Entities\"|notif configs", JsonConvert.SerializeObject(notifConfigs), It.IsAny<TimeSpan?>()), Times.Once());
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
    this._queueMock.Setup(s => s.Dequeue(It.IsAny<string>()))
      .Returns(Task.FromResult(JsonConvert.SerializeObject(change)));
    this._cacheMock.Setup(s => s.GetString(It.IsAny<string>()))
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

    await Notify.ProcessMessage(this._queueMock.Object, this._cacheMock.Object, this._dispatchersMock.Object, testHttpClient);
    this._cacheMock.Verify(m => m.Set("entity:\"Not Entities\"|notif configs", "", It.IsAny<TimeSpan?>()), Times.Once());
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
    this._queueMock.Setup(s => s.Dequeue(It.IsAny<string>()))
      .Returns(Task.FromResult(JsonConvert.SerializeObject(change)));
    this._cacheMock.Setup(s => s.GetString(It.IsAny<string>()))
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

    await Notify.ProcessMessage(this._queueMock.Object, this._cacheMock.Object, this._dispatchersMock.Object, testHttpClient);
    this._dispatchersMock.Verify(m => m.GetDispatcher("webhook"), Times.Once());
  }
}