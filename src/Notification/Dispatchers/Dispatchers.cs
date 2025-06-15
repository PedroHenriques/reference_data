using Notification.Types;
using Toolkit.Types;

namespace Notification.Dispatchers;

public class Dispatchers : IDispatchers
{
  private readonly Dictionary<string, IDispatcher> _dispatchers;

  public Dispatchers(
    HttpClient httpClient, IKafka<NotifDataKafkaKey, NotifDataKafkaValue> kafka,
    ILogger logger
  )
  {
    this._dispatchers = new Dictionary<string, IDispatcher>
    {
      { "webhook", new Webhook(httpClient, logger) },
      { "event", new Kafka(kafka, logger) },
    };
  }

  public IDispatcher? GetDispatcher(string protocol)
  {
    return this._dispatchers.GetValueOrDefault(protocol);
  }
}