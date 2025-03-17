using Confluent.Kafka;
using Moq;
using Notification.Dispatchers;
using SharedLibs.Types;

namespace Notification.Tests.Dispatchers;

[Trait("Type", "Unit")]
public class KafkaTests : IDisposable
{
  private readonly Mock<IEventBus<string, NotifData>> _eventBusMock;
  private readonly Mock<Action<bool>> _callbackMock;

  public KafkaTests()
  {
    this._eventBusMock = new Mock<IEventBus<string, NotifData>>(MockBehavior.Strict);
    this._callbackMock = new Mock<Action<bool>>(MockBehavior.Strict);

    this._eventBusMock.Setup(s => s.Publish(It.IsAny<string>(), It.IsAny<Message<string, NotifData>>(), It.IsAny<Action<DeliveryResult<string, NotifData>>>()));
    this._callbackMock.Setup(s => s(It.IsAny<bool>()));
  }

  public void Dispose()
  {
    this._eventBusMock.Reset();
    this._callbackMock.Reset();
  }

  [Fact]
  public async void Dispatch_ItShouldCallPublishFromTheIEventBusInstanceOnceWithTheExpectedArguments()
  {
    var sut = new Kafka(this._eventBusMock.Object);
    NotifData data = new NotifData
    {
      ChangeTime = DateTime.Now,
      EventTime = DateTime.Now,
      ChangeType = "",
      Entity = "",
      Id = "test data id",
    };

    var expectedMsg = new Message<string, NotifData>
    {
      Key = data.Id,
      Value = data
    };

    await sut.Dispatch(data, "test destination", this._callbackMock.Object);

    this._eventBusMock.Verify(m => m.Publish("test destination", It.IsAny<Message<string, NotifData>>(), It.IsAny<Action<DeliveryResult<string, NotifData>>>()), Times.Once());
  }

  [Fact]
  public async void Dispatch_ItShouldCallPublishFromTheIEventBusInstanceOnceWithTheExpectedMessageKey()
  {
    var sut = new Kafka(this._eventBusMock.Object);
    NotifData data = new NotifData
    {
      ChangeTime = DateTime.Now,
      EventTime = DateTime.Now,
      ChangeType = "",
      Entity = "",
      Id = "test data id",
    };

    await sut.Dispatch(data, "test destination", this._callbackMock.Object);

    Assert.Equal(
      data.Id,
      (this._eventBusMock.Invocations[0].Arguments[1] as Message<string, NotifData>).Key
    );
  }

  [Fact]
  public async void Dispatch_ItShouldCallPublishFromTheIEventBusInstanceOnceWithTheExpectedMessageValue()
  {
    var sut = new Kafka(this._eventBusMock.Object);
    NotifData data = new NotifData
    {
      ChangeTime = DateTime.Now,
      EventTime = DateTime.Now,
      ChangeType = "",
      Entity = "",
      Id = "test data id",
    };

    await sut.Dispatch(data, "test destination", this._callbackMock.Object);

    Assert.Equal(
      data,
      (this._eventBusMock.Invocations[0].Arguments[1] as Message<string, NotifData>).Value
    );
  }

  [Fact]
  public async void Dispatch_ItShouldCallPublishFromTheIEventBusInstance_ExecutingTheFunctionPassedAsThirdArgument_ItShouldCallTheCallbackOnceWithTrue()
  {
    var sut = new Kafka(this._eventBusMock.Object);
    NotifData data = new NotifData
    {
      ChangeTime = DateTime.Now,
      EventTime = DateTime.Now,
      ChangeType = "",
      Entity = "",
      Id = "test data id",
    };

    await sut.Dispatch(data, "test destination", this._callbackMock.Object);
    var callback = this._eventBusMock.Invocations[0].Arguments[2] as Action<DeliveryResult<string, NotifData>>;

    DeliveryResult<string, NotifData> dispatchRes = new DeliveryResult<string, NotifData> { Status = PersistenceStatus.Persisted };
    callback(dispatchRes);
    this._callbackMock.Verify(m => m(true), Times.Once());
  }

  [Fact]
  public async void Dispatch_ItShouldCallPublishFromTheIEventBusInstance_ExecutingTheFunctionPassedAsThirdArgument_IfThePublishResultHasAStatusOfNotPersisted_ItShouldCallTheCallbackOnceWithFalse()
  {
    var sut = new Kafka(this._eventBusMock.Object);
    NotifData data = new NotifData
    {
      ChangeTime = DateTime.Now,
      EventTime = DateTime.Now,
      ChangeType = "",
      Entity = "",
      Id = "test data id",
    };

    await sut.Dispatch(data, "test destination", this._callbackMock.Object);
    var callback = this._eventBusMock.Invocations[0].Arguments[2] as Action<DeliveryResult<string, NotifData>>;

    DeliveryResult<string, NotifData> dispatchRes = new DeliveryResult<string, NotifData> { Status = PersistenceStatus.NotPersisted };
    callback(dispatchRes);
    this._callbackMock.Verify(m => m(false), Times.Once());
  }

  [Fact]
  public async void Dispatch_ItShouldCallPublishFromTheIEventBusInstance_ExecutingTheFunctionPassedAsThirdArgument_IfThePublishResultHasAStatusOfPossiblyPersisted_ItShouldCallTheCallbackOnceWithTrue()
  {
    var sut = new Kafka(this._eventBusMock.Object);
    NotifData data = new NotifData
    {
      ChangeTime = DateTime.Now,
      EventTime = DateTime.Now,
      ChangeType = "",
      Entity = "",
      Id = "test data id",
    };

    await sut.Dispatch(data, "test destination", this._callbackMock.Object);
    var callback = this._eventBusMock.Invocations[0].Arguments[2] as Action<DeliveryResult<string, NotifData>>;

    DeliveryResult<string, NotifData> dispatchRes = new DeliveryResult<string, NotifData> { Status = PersistenceStatus.PossiblyPersisted };
    callback(dispatchRes);
    this._callbackMock.Verify(m => m(true), Times.Once());
  }
}