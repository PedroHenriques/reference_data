using Confluent.Kafka;
using Notification.Types;
using SharedLibs.Types;

namespace Notification.Dispatchers;

public class Kafka : IDispatcher
{
  private readonly IEventBus<string, NotifData> _eventBus;

  public Kafka(IEventBus<string, NotifData> eventBus)
  {
    this._eventBus = eventBus;
  }

  public Task Dispatch(NotifData data, string destination, Action<bool> callback)
  {
    this._eventBus.Publish(
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