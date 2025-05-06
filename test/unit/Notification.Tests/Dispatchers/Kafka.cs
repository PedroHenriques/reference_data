using Confluent.Kafka;
using Moq;
using Newtonsoft.Json;
using Notification.Dispatchers;
using Notification.Types;
using SharedLibs.Types;
using Toolkit.Types;

namespace Notification.Tests.Dispatchers;

[Trait("Type", "Unit")]
public class KafkaTests : IDisposable
{
  private readonly Mock<IKafka<NotifDataKafkaKey, NotifData>> _kafkaMock;
  private readonly Mock<Action<bool>> _callbackMock;

  public KafkaTests()
  {
    this._kafkaMock = new Mock<IKafka<NotifDataKafkaKey, NotifData>>(MockBehavior.Strict);
    this._callbackMock = new Mock<Action<bool>>(MockBehavior.Strict);

    this._kafkaMock.Setup(s => s.Publish(It.IsAny<string>(), It.IsAny<Message<NotifDataKafkaKey, NotifData>>(), It.IsAny<Action<DeliveryResult<NotifDataKafkaKey, NotifData>>>()));
    this._callbackMock.Setup(s => s(It.IsAny<bool>()));
  }

  public void Dispose()
  {
    this._kafkaMock.Reset();
    this._callbackMock.Reset();
  }

  [Fact]
  public async Task Dispatch_ItShouldCallPublishFromTheIEventBusInstanceOnceWithTheExpectedArguments()
  {
    var sut = new Kafka(this._kafkaMock.Object);
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

    this._kafkaMock.Verify(m => m.Publish("test destination", It.IsAny<Message<NotifDataKafkaKey, NotifData>>(), It.IsAny<Action<DeliveryResult<NotifDataKafkaKey, NotifData>>>()), Times.Once());
  }

  [Fact]
  public async Task Dispatch_ItShouldCallPublishFromTheIEventBusInstanceOnceWithTheExpectedMessageKey()
  {
    var sut = new Kafka(this._kafkaMock.Object);
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
      JsonConvert.SerializeObject(new NotifDataKafkaKey { Id = data.Id }),
      JsonConvert.SerializeObject((this._kafkaMock.Invocations[0].Arguments[1] as Message<NotifDataKafkaKey, NotifData>).Key)
    );
  }

  [Fact]
  public async Task Dispatch_ItShouldCallPublishFromTheIEventBusInstanceOnceWithTheExpectedMessageValue()
  {
    var sut = new Kafka(this._kafkaMock.Object);
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
      (this._kafkaMock.Invocations[0].Arguments[1] as Message<NotifDataKafkaKey, NotifData>).Value
    );
  }

  [Fact]
  public async Task Dispatch_ItShouldCallPublishFromTheIEventBusInstance_ExecutingTheFunctionPassedAsThirdArgument_ItShouldCallTheCallbackOnceWithTrue()
  {
    var sut = new Kafka(this._kafkaMock.Object);
    NotifData data = new NotifData
    {
      ChangeTime = DateTime.Now,
      EventTime = DateTime.Now,
      ChangeType = "",
      Entity = "",
      Id = "test data id",
    };

    await sut.Dispatch(data, "test destination", this._callbackMock.Object);
    var callback = this._kafkaMock.Invocations[0].Arguments[2] as Action<DeliveryResult<NotifDataKafkaKey, NotifData>>;

    DeliveryResult<NotifDataKafkaKey, NotifData> dispatchRes = new DeliveryResult<NotifDataKafkaKey, NotifData> { Status = PersistenceStatus.Persisted };
    callback(dispatchRes);
    this._callbackMock.Verify(m => m(true), Times.Once());
  }

  [Fact]
  public async Task Dispatch_ItShouldCallPublishFromTheIEventBusInstance_ExecutingTheFunctionPassedAsThirdArgument_IfThePublishResultHasAStatusOfNotPersisted_ItShouldCallTheCallbackOnceWithFalse()
  {
    var sut = new Kafka(this._kafkaMock.Object);
    NotifData data = new NotifData
    {
      ChangeTime = DateTime.Now,
      EventTime = DateTime.Now,
      ChangeType = "",
      Entity = "",
      Id = "test data id",
    };

    await sut.Dispatch(data, "test destination", this._callbackMock.Object);
    var callback = this._kafkaMock.Invocations[0].Arguments[2] as Action<DeliveryResult<NotifDataKafkaKey, NotifData>>;

    DeliveryResult<NotifDataKafkaKey, NotifData> dispatchRes = new DeliveryResult<NotifDataKafkaKey, NotifData> { Status = PersistenceStatus.NotPersisted };
    callback(dispatchRes);
    this._callbackMock.Verify(m => m(false), Times.Once());
  }

  [Fact]
  public async Task Dispatch_ItShouldCallPublishFromTheIEventBusInstance_ExecutingTheFunctionPassedAsThirdArgument_IfThePublishResultHasAStatusOfPossiblyPersisted_ItShouldCallTheCallbackOnceWithTrue()
  {
    var sut = new Kafka(this._kafkaMock.Object);
    NotifData data = new NotifData
    {
      ChangeTime = DateTime.Now,
      EventTime = DateTime.Now,
      ChangeType = "",
      Entity = "",
      Id = "test data id",
    };

    await sut.Dispatch(data, "test destination", this._callbackMock.Object);
    var callback = this._kafkaMock.Invocations[0].Arguments[2] as Action<DeliveryResult<NotifDataKafkaKey, NotifData>>;

    DeliveryResult<NotifDataKafkaKey, NotifData> dispatchRes = new DeliveryResult<NotifDataKafkaKey, NotifData> { Status = PersistenceStatus.PossiblyPersisted };
    callback(dispatchRes);
    this._callbackMock.Verify(m => m(true), Times.Once());
  }

  [Fact]
  public async Task Dispatch_IfTheProvidedDataIsADeleteEvent_ItShouldCallPublishFromTheIEventBusInstanceOnceWithTheExpectedMessageValue()
  {
    var sut = new Kafka(this._kafkaMock.Object);
    NotifData data = new NotifData
    {
      ChangeTime = DateTime.Now,
      EventTime = DateTime.Now,
      ChangeType = "delete",
      Entity = "",
      Id = "test data id",
    };

    await sut.Dispatch(data, "test destination", this._callbackMock.Object);

    Assert.Null((this._kafkaMock.Invocations[0].Arguments[1] as Message<NotifDataKafkaKey, NotifData?>).Value);
  }
}