using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Moq;
using SharedLibs.Types;

namespace SharedLibs.Tests;

[Trait("Type", "Unit")]
public class EventBusTests : IDisposable
{
  private readonly Mock<ISchemaRegistryClient> _schemaRegistryMock;
  private readonly Mock<IProducer<string, string>> _producerMock;
  private readonly Mock<Action<DeliveryResult<string, string>>> _handlerDelegateMock;
  private EventBusInputs<string, string> _eventBusInputs;

  public EventBusTests()
  {
    this._schemaRegistryMock = new Mock<ISchemaRegistryClient>(MockBehavior.Strict);
    this._producerMock = new Mock<IProducer<string, string>>(MockBehavior.Strict);
    this._handlerDelegateMock = new Mock<Action<DeliveryResult<string, string>>>(MockBehavior.Strict);

    this._producerMock.Setup(s => s.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, string>>(), It.IsAny<CancellationToken>()))
      .Returns(Task.FromResult(new DeliveryResult<string, string> { }));
    this._producerMock.Setup(s => s.Flush(It.IsAny<CancellationToken>()));

    this._eventBusInputs = new EventBusInputs<string, string>
    {
      SchemaRegistry = this._schemaRegistryMock.Object,
      SchemaSubject = "test schema subject",
      SchemaVersion = 1,
    };
  }

  public void Dispose()
  {
    this._schemaRegistryMock.Reset();
    this._producerMock.Reset();
    this._handlerDelegateMock.Reset();
  }

  [Fact]
  public void Publish_ItShouldCallProduceAsyncFromTheProducerInstanceOnceWithTheExpectedArguments()
  {
    this._eventBusInputs.Producer = this._producerMock.Object;
    var sut = new EventBus<string, string>(this._eventBusInputs);

    var testMessage = new Message<string, string>
    {
      Key = "test msg key",
      Value = "test msg value"
    };
    sut.Publish("test topic name", testMessage, this._handlerDelegateMock.Object);

    this._producerMock.Verify(m => m.ProduceAsync("test topic name", testMessage, default), Times.Once());
  }

  [Fact]
  public void Publish_ItShouldCallFlushFromTheProducerInstanceOnceWithTheExpectedArguments()
  {
    this._eventBusInputs.Producer = this._producerMock.Object;
    var sut = new EventBus<string, string>(this._eventBusInputs);

    var testMessage = new Message<string, string>
    {
      Key = "test msg key",
      Value = "test msg value"
    };
    sut.Publish("test topic name", testMessage, this._handlerDelegateMock.Object);

    this._producerMock.Verify(m => m.Flush((CancellationToken)default), Times.Once());
  }

  [Fact]
  public async void Publish_ItShouldCallTheHandlerReceivedAsInputOnceWithTheExpectedArguments()
  {
    var deliveryRes = new DeliveryResult<string, string> { };
    this._producerMock.Setup(s => s.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, string>>(), It.IsAny<CancellationToken>()))
      .Returns(Task.FromResult(deliveryRes));
    this._eventBusInputs.Producer = this._producerMock.Object;
    var sut = new EventBus<string, string>(this._eventBusInputs);

    var testMessage = new Message<string, string>
    {
      Key = "test msg key",
      Value = "test msg value"
    };
    sut.Publish("test topic name", testMessage, this._handlerDelegateMock.Object);
    await Task.Delay(5);

    this._handlerDelegateMock.Verify(m => m(deliveryRes), Times.Once());
  }

  [Fact]
  public void Publish_IfAProducerWasNotProvidedInTheInputs_ItShouldThrowAnException()
  {
    var sut = new EventBus<string, string>(this._eventBusInputs);

    var testMessage = new Message<string, string>
    {
      Key = "test msg key",
      Value = "test msg value"
    };

    var e = Assert.Throws<Exception>(() => sut.Publish("test topic name", testMessage, this._handlerDelegateMock.Object));
    Assert.Equal("An instance of IProducer was not provided in the inputs.", e.Message);
  }
}