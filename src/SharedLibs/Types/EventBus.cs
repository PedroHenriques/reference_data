using Confluent.Kafka;
using Confluent.SchemaRegistry;

namespace SharedLibs.Types;

public struct EventBusInputs<TKey, TValue>
where TValue : class
{
  public required ISchemaRegistryClient SchemaRegistry { get; set; }
  public required string SchemaSubject { get; set; }
  public required int SchemaVersion { get; set; }
  public IProducer<TKey, TValue>? Producer { get; set; }
}

public interface IEventBus<TKey, TValue>
{
  public void Publish(
    string topicName, Message<TKey, TValue> message,
    Action<DeliveryResult<TKey, TValue>> handler
  );
}