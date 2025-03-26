using Confluent.Kafka;
using Notification.Types;
using SharedLibs.Types;
using Toolkit.Types;

namespace Notification.Dispatchers;

public class Kafka : IDispatcher
{
  private readonly IKafka<string, NotifData> _kafka;

  public Kafka(IKafka<string, NotifData> eventBus)
  {
    this._kafka = eventBus;
  }

  public Task Dispatch(NotifData data, string destination, Action<bool> callback)
  {
    this._kafka.Publish(
      destination,
      new Message<string, NotifData>
      {
        Key = data.Id,
        Value = data
      },
      PublishHandler(callback)
    );

    return Task.CompletedTask;
  }

  private static Action<DeliveryResult<string, NotifData>> PublishHandler(Action<bool> callback)
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