using MongoDB.Bson;
using Moq;
using Newtonsoft.Json;
using Notification.Utils;
using SharedLibs.Types.Cache;
using SharedLibs.Types.Db;
using SharedLibs.Types.Entity;

namespace Notification.Tests.Utils;

[Trait("Type", "Unit")]
public class NotifyTests : IDisposable
{
  private readonly Mock<ICache> _cacheMock;
  private readonly Mock<IQueue> _queueMock;

  public NotifyTests()
  {
    this._cacheMock = new Mock<ICache>(MockBehavior.Strict);
    this._queueMock = new Mock<IQueue>(MockBehavior.Strict);

    this._cacheMock.Setup(s => s.Set(It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.FromResult(true));
    this._queueMock.Setup(s => s.Dequeue(It.IsAny<string>()))
      .Returns(Task.FromResult(JsonConvert.SerializeObject(new ChangeQueueItem { ChangeRecord = "", ChangeTime = DateTime.Now, Source = "" })));
    this._queueMock.Setup(s => s.Ack(It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.FromResult(true));
  }

  public void Dispose()
  {
    this._cacheMock.Reset();
    this._queueMock.Reset();
  }

  [Fact]
  public async void ProcessMessage_ItShouldCallDequeueOnTheProvidedIQueueInstanceOnce()
  {
    await Notify.ProcessMessage(this._queueMock.Object, this._cacheMock.Object);
    this._queueMock.Verify(m => m.Dequeue("mongo_changes"), Times.Once());
  }

  [Fact]
  public async void ProcessMessage_IfThereAreNoMessagesInTheQueue_ItShouldNotCallTheICacheInstance()
  {
    this._queueMock.Setup(s => s.Dequeue(It.IsAny<string>()))
      .Returns(Task.FromResult(""));

    await Notify.ProcessMessage(this._queueMock.Object, this._cacheMock.Object);
    this._cacheMock.Verify(m => m.Set(It.IsAny<string>(), It.IsAny<string>()), Times.Never());
    this._cacheMock.Verify(m => m.Get(It.IsAny<string>()), Times.Never());
  }

  [Fact]
  public async void ProcessMessage_IfTheSourceCollNameIsEntities_ItShouldCallSetOnTheProvidedICacheInstanceOnceWithTheExpectedData()
  {
    var notifConfigs = new NotifConfig[] {
      new NotifConfig { Protocol = "kafka", TargetURL = "some url" },
    };
    var notifConfigsStr = notifConfigs.ToJson();
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
    this._queueMock.Setup(s => s.Dequeue(It.IsAny<string>()))
      .Returns(Task.FromResult(JsonConvert.SerializeObject(change)));

    await Notify.ProcessMessage(this._queueMock.Object, this._cacheMock.Object);
    this._cacheMock.Verify(m => m.Set("entity:test entity name|notif configs", notifConfigsStr), Times.Once());
  }

  [Fact]
  public async void ProcessMessage_IfTheSourceCollNameIsEntities_ItShouldCallAckOnTheProvidedIQueueInstanceOnceWithTheExpectedData()
  {
    var notifConfigs = new NotifConfig[] {
      new NotifConfig { Protocol = "kafka", TargetURL = "some url" },
    };
    var notifConfigsStr = notifConfigs.ToJson();
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

    await Notify.ProcessMessage(this._queueMock.Object, this._cacheMock.Object);
    this._queueMock.Verify(m => m.Ack("mongo_changes", changeStr), Times.Once());
  }

  [Fact]
  public async void ProcessMessage_IfTheSourceCollNameIsEntities_IfTheChangeDocumentDoesNotHaveNotifConfigs_ItShouldCallSetOnTheProvidedICacheInstanceWithTheExpectedData()
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

    await Notify.ProcessMessage(this._queueMock.Object, this._cacheMock.Object);
    this._cacheMock.Verify(m => m.Set("entity:test entity name|notif configs", ""), Times.Once());
  }

  [Fact]
  public async void ProcessMessage_IfTheSourceCollNameIsEntities_IfTheChangeDocumentHasANotifConfigsThatIsTheStringNull_ItShouldCallSetOnTheProvidedICacheInstanceWithTheExpectedData()
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

    await Notify.ProcessMessage(this._queueMock.Object, this._cacheMock.Object);
    this._cacheMock.Verify(m => m.Set("entity:test entity name|notif configs", ""), Times.Once());
  }
}