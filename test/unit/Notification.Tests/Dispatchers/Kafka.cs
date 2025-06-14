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
  private readonly Mock<IKafka<NotifDataKafkaKey, NotifDataKafkaValue>> _kafkaMock;
  private readonly Mock<Action<bool>> _callbackMock;
  private readonly Mock<ILogger> _logger;

  public KafkaTests()
  {
    Environment.SetEnvironmentVariable("KAFKA_SCHEMA_REGISTRY_URL", "a");
    Environment.SetEnvironmentVariable("KAFKA_SCHEMA_SUBJECT", "a");
    Environment.SetEnvironmentVariable("KAFKA_SCHEMA_VERSION", "a");
    Environment.SetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS", "a");
    Environment.SetEnvironmentVariable("KAFKA_BROKER_SASL_USERNAME", "a");
    Environment.SetEnvironmentVariable("KAFKA_BROKER_SASL_PW", "a");
    Environment.SetEnvironmentVariable("KAFKA_SCHEMA_REGISTRY_SASL_USERNAME", "a");
    Environment.SetEnvironmentVariable("KAFKA_SCHEMA_REGISTRY_SASL_PW", "a");
    Environment.SetEnvironmentVariable("API_BASE_URL", "a");
    Environment.SetEnvironmentVariable("API_PORT", "a");
    Environment.SetEnvironmentVariable("NUM_PROCESSES", "1");
    Environment.SetEnvironmentVariable("PROJECT_NAME", "test proj name");

    this._kafkaMock = new Mock<IKafka<NotifDataKafkaKey, NotifDataKafkaValue>>(MockBehavior.Strict);
    this._callbackMock = new Mock<Action<bool>>(MockBehavior.Strict);
    this._logger = new Mock<ILogger>(MockBehavior.Strict);

    this._kafkaMock.Setup(s => s.Publish(It.IsAny<string>(), It.IsAny<Message<NotifDataKafkaKey, NotifDataKafkaValue>>(), It.IsAny<Action<DeliveryResult<NotifDataKafkaKey, NotifDataKafkaValue>>>()));

    this._callbackMock.Setup(s => s(It.IsAny<bool>()));

    this._logger.Setup(s => s.Log(It.IsAny<Microsoft.Extensions.Logging.LogLevel>(), It.IsAny<Exception?>(), It.IsAny<string>()));
  }

  public void Dispose()
  {
    Environment.SetEnvironmentVariable("KAFKA_SCHEMA_REGISTRY_URL", null);
    Environment.SetEnvironmentVariable("KAFKA_SCHEMA_SUBJECT", null);
    Environment.SetEnvironmentVariable("KAFKA_SCHEMA_VERSION", null);
    Environment.SetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS", null);
    Environment.SetEnvironmentVariable("KAFKA_BROKER_SASL_USERNAME", null);
    Environment.SetEnvironmentVariable("KAFKA_BROKER_SASL_PW", null);
    Environment.SetEnvironmentVariable("KAFKA_SCHEMA_REGISTRY_SASL_USERNAME", null);
    Environment.SetEnvironmentVariable("KAFKA_SCHEMA_REGISTRY_SASL_PW", null);
    Environment.SetEnvironmentVariable("API_BASE_URL", null);
    Environment.SetEnvironmentVariable("API_PORT", null);
    Environment.SetEnvironmentVariable("NUM_PROCESSES", null);
    Environment.SetEnvironmentVariable("PROJECT_NAME", null);

    this._kafkaMock.Reset();
    this._callbackMock.Reset();
    this._logger.Reset();
  }

  [Fact]
  public async Task Dispatch_ItShouldCallPublishFromTheIEventBusInstanceOnceWithTheExpectedArguments()
  {
    var sut = new Kafka(this._kafkaMock.Object, this._logger.Object);
    NotifData data = new NotifData
    {
      ChangeTime = DateTime.Now,
      EventTime = DateTime.Now,
      ChangeType = "update",
      Entity = "",
      Id = "test data id",
    };

    var expectedMsg = new Message<string, NotifData>
    {
      Key = data.Id,
      Value = data
    };

    await sut.Dispatch(data, "test destination", this._callbackMock.Object);

    this._kafkaMock.Verify(m => m.Publish("test destination", It.IsAny<Message<NotifDataKafkaKey, NotifDataKafkaValue>>(), It.IsAny<Action<DeliveryResult<NotifDataKafkaKey, NotifDataKafkaValue>>>()), Times.Once());
  }

  [Fact]
  public async Task Dispatch_ItShouldCallPublishFromTheIEventBusInstanceOnceWithTheExpectedMessageKey()
  {
    var sut = new Kafka(this._kafkaMock.Object, this._logger.Object);
    NotifData data = new NotifData
    {
      ChangeTime = DateTime.Now,
      EventTime = DateTime.Now,
      ChangeType = "replace",
      Entity = "",
      Id = "test data id",
    };

    await sut.Dispatch(data, "test destination", this._callbackMock.Object);

    Assert.Equal(
      JsonConvert.SerializeObject(new NotifDataKafkaKey { Id = data.Id }),
      JsonConvert.SerializeObject((this._kafkaMock.Invocations[0].Arguments[1] as Message<NotifDataKafkaKey, NotifDataKafkaValue>).Key)
    );
  }

  [Fact]
  public async Task Dispatch_ItShouldCallPublishFromTheIEventBusInstanceOnceWithTheExpectedMessageValueMetadataBlock()
  {
    var sut = new Kafka(this._kafkaMock.Object, this._logger.Object);
    NotifData data = new NotifData
    {
      ChangeTime = DateTime.Now,
      EventTime = DateTime.Now,
      ChangeType = "insert",
      Entity = "test entity",
      Id = "test data id",
      Document = new Dictionary<string, dynamic?> {
        { "some prop", true },
        { "another prop", "hello" },
      },
    };

    await sut.Dispatch(data, "test destination", this._callbackMock.Object);

    NotifDataKafkaValue expectedValue = new NotifDataKafkaValue
    {
      Metadata = new NotifDataKafkaValueMetadata
      {
        Action = "CREATE",
        ActionDatetime = data.ChangeTime,
        EventDatetime = data.EventTime,
        CorrelationId = "",
        Source = "test proj name",
      },
      Data = new NotifDataKafkaValueData
      {
        Id = data.Id,
        Entity = data.Entity,
        Document = data.Document,
      },
    };

    var actualValue = (this._kafkaMock.Invocations[0].Arguments[1] as Message<NotifDataKafkaKey, NotifDataKafkaValue>).Value;

    Assert.IsType<Guid>(Guid.Parse(actualValue.Metadata.CorrelationId));

    actualValue.Metadata.CorrelationId = "";
    Assert.Equal(
      JsonConvert.SerializeObject(expectedValue),
      JsonConvert.SerializeObject(actualValue)
    );
  }

  [Fact]
  public async Task Dispatch_ItShouldCallPublishFromTheIEventBusInstance_ExecutingTheFunctionPassedAsThirdArgument_ItShouldCallTheCallbackOnceWithTrue()
  {
    var sut = new Kafka(this._kafkaMock.Object, this._logger.Object);
    NotifData data = new NotifData
    {
      ChangeTime = DateTime.Now,
      EventTime = DateTime.Now,
      ChangeType = "insert",
      Entity = "",
      Id = "test data id",
    };

    await sut.Dispatch(data, "test destination", this._callbackMock.Object);
    var callback = this._kafkaMock.Invocations[0].Arguments[2] as Action<DeliveryResult<NotifDataKafkaKey, NotifDataKafkaValue>>;

    var dispatchRes = new DeliveryResult<NotifDataKafkaKey, NotifDataKafkaValue>
    {
      Status = PersistenceStatus.Persisted,
      Partition = new Partition(1),
      Offset = new Offset(4),
      Message = new Message<NotifDataKafkaKey, NotifDataKafkaValue>
      {
        Key = new NotifDataKafkaKey { Id = "test data id" },
      },
    };
    callback(dispatchRes);
    this._callbackMock.Verify(m => m(true), Times.Once());
  }

  [Fact]
  public async Task Dispatch_ItShouldCallPublishFromTheIEventBusInstance_ExecutingTheFunctionPassedAsThirdArgument_IfThePublishResultHasAStatusOfNotPersisted_ItShouldCallTheCallbackOnceWithFalse()
  {
    var sut = new Kafka(this._kafkaMock.Object, this._logger.Object);
    NotifData data = new NotifData
    {
      ChangeTime = DateTime.Now,
      EventTime = DateTime.Now,
      ChangeType = "delete",
      Entity = "",
      Id = "test data id",
    };

    await sut.Dispatch(data, "test destination", this._callbackMock.Object);
    var callback = this._kafkaMock.Invocations[0].Arguments[2] as Action<DeliveryResult<NotifDataKafkaKey, NotifDataKafkaValue>>;

    var dispatchRes = new DeliveryResult<NotifDataKafkaKey, NotifDataKafkaValue>
    {
      Status = PersistenceStatus.NotPersisted,
      Partition = new Partition(1),
      Offset = new Offset(4),
      Message = new Message<NotifDataKafkaKey, NotifDataKafkaValue>
      {
        Key = new NotifDataKafkaKey { Id = "test data id" },
      },
    };
    callback(dispatchRes);
    this._callbackMock.Verify(m => m(false), Times.Once());
  }

  [Fact]
  public async Task Dispatch_ItShouldCallPublishFromTheIEventBusInstance_ExecutingTheFunctionPassedAsThirdArgument_IfThePublishResultHasAStatusOfNotPersisted_ItShouldLogAnError()
  {
    var sut = new Kafka(this._kafkaMock.Object, this._logger.Object);
    NotifData data = new NotifData
    {
      ChangeTime = DateTime.Now,
      EventTime = DateTime.Now,
      ChangeType = "delete",
      Entity = "",
      Id = "test data id",
    };

    await sut.Dispatch(data, "test destination", this._callbackMock.Object);
    var callback = this._kafkaMock.Invocations[0].Arguments[2] as Action<DeliveryResult<NotifDataKafkaKey, NotifDataKafkaValue>>;

    var dispatchRes = new DeliveryResult<NotifDataKafkaKey, NotifDataKafkaValue>
    {
      Status = PersistenceStatus.NotPersisted,
      Partition = new Partition(5),
      Offset = new Offset(23),
      Message = new Message<NotifDataKafkaKey, NotifDataKafkaValue>
      {
        Key = new NotifDataKafkaKey { Id = "test data id" },
      },
    };
    callback(dispatchRes);
    this._logger.Verify(m => m.Log(Microsoft.Extensions.Logging.LogLevel.Error, null, "Kafka Dispatcher - publish event callback: Document id = test data id | Status = NotPersisted | Partition = [5] | Offset = 23"), Times.Once());
  }

  [Fact]
  public async Task Dispatch_ItShouldCallPublishFromTheIEventBusInstance_ExecutingTheFunctionPassedAsThirdArgument_IfThePublishResultHasAStatusOfPossiblyPersisted_ItShouldCallTheCallbackOnceWithTrue()
  {
    var sut = new Kafka(this._kafkaMock.Object, this._logger.Object);
    NotifData data = new NotifData
    {
      ChangeTime = DateTime.Now,
      EventTime = DateTime.Now,
      ChangeType = "delete",
      Entity = "",
      Id = "test data id",
    };

    await sut.Dispatch(data, "test destination", this._callbackMock.Object);
    var callback = this._kafkaMock.Invocations[0].Arguments[2] as Action<DeliveryResult<NotifDataKafkaKey, NotifDataKafkaValue>>;

    var dispatchRes = new DeliveryResult<NotifDataKafkaKey, NotifDataKafkaValue>
    {
      Status = PersistenceStatus.PossiblyPersisted,
      Partition = new Partition(1),
      Offset = new Offset(4),
      Message = new Message<NotifDataKafkaKey, NotifDataKafkaValue>
      {
        Key = new NotifDataKafkaKey { Id = "test data id" },
      },
    };
    callback(dispatchRes);
    this._callbackMock.Verify(m => m(true), Times.Once());
  }

  [Fact]
  public async Task Dispatch_IfTheProvidedDataIsADeleteEvent_ItShouldCallPublishFromTheIEventBusInstanceOnceWithTheExpectedMessageValue()
  {
    var sut = new Kafka(this._kafkaMock.Object, this._logger.Object);
    NotifData data = new NotifData
    {
      ChangeTime = DateTime.Now,
      EventTime = DateTime.Now,
      ChangeType = "delete",
      Entity = "",
      Id = "test data id",
    };

    await sut.Dispatch(data, "test destination", this._callbackMock.Object);

    Assert.Null((this._kafkaMock.Invocations[0].Arguments[1] as Message<NotifDataKafkaKey, NotifDataKafkaValue?>).Value);
  }
}