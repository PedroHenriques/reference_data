using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using SharedLibs.Types;

namespace SharedLibs.Utils;

// Not unit testable due to the use of ProducerBuilder is on non-overwritable
// methods
public static class EventBus<TKey, TValue>
where TValue : class
{
  public static EventBusInputs<TKey, TValue> PrepareInputs(
    ISchemaRegistryClient schemaRegistry, string schemaSubject,
    int schemaVersion, JsonSerializer<TValue> jsonSerializer,
    ProducerBuilder<TKey, TValue>? producerBuilder = null
  )
  {
    IProducer<TKey, TValue>? producer = null;
    if (producerBuilder != null)
    {
      producer = producerBuilder.SetValueSerializer(jsonSerializer).Build();
    }

    return new EventBusInputs<TKey, TValue>
    {
      SchemaRegistry = schemaRegistry,
      SchemaSubject = schemaSubject,
      SchemaVersion = schemaVersion,
      Producer = producer
    };
  }
}