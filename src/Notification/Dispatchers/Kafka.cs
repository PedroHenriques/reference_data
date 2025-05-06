using Confluent.Kafka;
using Notification.Types;
using SharedLibs.Types;
using Toolkit.Types;

namespace Notification.Dispatchers;

public class Kafka : IDispatcher
{
  private readonly IKafka<NotifDataKafkaKey, NotifData> _kafka;

  public Kafka(IKafka<NotifDataKafkaKey, NotifData> eventBus)
  {
    this._kafka = eventBus;
  }

  public Task Dispatch(NotifData data, string destination, Action<bool> callback)
  {
    Message<NotifDataKafkaKey, NotifData> message = new Message<NotifDataKafkaKey, NotifData>
    {
      Key = new NotifDataKafkaKey { Id = data.Id },
    };

    if (data.ChangeType != ChangeRecordTypes.Delete.Name)
    {
      message.Value = data;
    }

    this._kafka.Publish(
      destination,
      message,
      PublishHandler(callback)
    );

    return Task.CompletedTask;
  }

  private static Action<DeliveryResult<NotifDataKafkaKey, NotifData>> PublishHandler(Action<bool> callback)
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