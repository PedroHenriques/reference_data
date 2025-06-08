using Confluent.Kafka;
using KafkaConfigs = Notification.Configs.Kafka;
using Notification.Types;
using SharedLibs.Types;
using Toolkit.Types;
using Notification.Configs;

namespace Notification.Dispatchers;

public class Kafka : IDispatcher
{
  private readonly IKafka<NotifDataKafkaKey, NotifDataKafkaValue> _kafka;

  public Kafka(IKafka<NotifDataKafkaKey, NotifDataKafkaValue> eventBus)
  {
    this._kafka = eventBus;
  }

  public Task Dispatch(NotifData data, string destination, Action<bool> callback)
  {
    Message<NotifDataKafkaKey, NotifDataKafkaValue> message = new Message<NotifDataKafkaKey, NotifDataKafkaValue>
    {
      Key = new NotifDataKafkaKey { Id = data.Id },
    };

    if (data.ChangeType != ChangeRecordTypes.Delete.Name)
    {
      message.Value = new NotifDataKafkaValue
      {
        Metadata = new NotifDataKafkaValueMetadata
        {
          Action = KafkaConfigs.MetadataActionMap[data.ChangeType],
          ActionDatetime = data.ChangeTime,
          EventDatetime = data.EventTime,
          CorrelationId = Guid.NewGuid().ToString(),
          Source = General.ProjectName,
        },
        Data = new NotifDataKafkaValueData
        {
          Id = data.Id,
          Entity = data.Entity,
          Document = data.Document,
        },
      };
    }

    this._kafka.Publish(
      destination,
      message,
      PublishHandler(callback)
    );

    return Task.CompletedTask;
  }

  private static Action<DeliveryResult<NotifDataKafkaKey, NotifDataKafkaValue>> PublishHandler(Action<bool> callback)
  {
    return result =>
    {
      Console.WriteLine($"Status: {result.Status}");
      Console.WriteLine($"Partition: {result.Partition}");
      Console.WriteLine($"Offset: {result.Offset}");
      callback(result.Status != PersistenceStatus.NotPersisted);
    };
  }
}