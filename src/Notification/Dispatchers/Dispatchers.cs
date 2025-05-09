using Notification.Types;
using SharedLibs.Types;
using Toolkit.Types;

namespace Notification.Dispatchers;

public class Dispatchers : IDispatchers
{
  private readonly Dictionary<string, IDispatcher> _dispatchers;

  public Dispatchers(
    HttpClient httpClient, IKafka<NotifDataKafkaKey, NotifDataKafkaValue> kafka
  )
  {
    this._dispatchers = new Dictionary<string, IDispatcher>
    {
      { "webhook", new Webhook(httpClient) },
      { "event", new Kafka(kafka) },
    };
  }

  public IDispatcher? GetDispatcher(string protocol)
  {
    return this._dispatchers.GetValueOrDefault(protocol);
  }
}