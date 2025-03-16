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
  private readonly Mock<Action<DeliveryReport<string, string>>> _handlerDelegateMock;
  private EventBusInputs<string, string> _eventBusInputs;

  public EventBusTests()
  {
    this._schemaRegistryMock = new Mock<ISchemaRegistryClient>(MockBehavior.Strict);
    this._producerMock = new Mock<IProducer<string, string>>(MockBehavior.Strict);
    this._handlerDelegateMock = new Mock<Action<DeliveryReport<string, string>>>(MockBehavior.Strict);

    this._producerMock.Setup(s => s.Produce(It.IsAny<string>(), It.IsAny<Message<string, string>>(), It.IsAny<Action<DeliveryReport<string, string>>>()));

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
  public void Publish_ItShouldCallProduceFromTheProducerInstanceOnceWithTheExpectedArguments()
  {
    this._eventBusInputs.Producer = this._producerMock.Object;
    var sut = new EventBus<string, string>(this._eventBusInputs);

    var testMessage = new Message<string, string>
    {
      Key = "test msg key",
      Value = "test msg value"
    };
    sut.Publish("test topic name", testMessage, this._handlerDelegateMock.Object);

    this._producerMock.Verify(m => m.Produce("test topic name", testMessage, this._handlerDelegateMock.Object), Times.Once());
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