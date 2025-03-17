using Notification.Types;
using SharedLibs.Types;

namespace Notification.Dispatchers;

public class Dispatchers : IDispatchers
{
  private readonly Dictionary<string, IDispatcher> _dispatchers;

  public Dispatchers(
    HttpClient httpClient, IEventBus<string, NotifData> eventBus
  )
  {
    this._dispatchers = new Dictionary<string, IDispatcher>
    {
      { "webhook", new Webhook(httpClient) },
      { "event", new Kafka(eventBus) },
    };
  }

  public IDispatcher? GetDispatcher(string protocol)
  {
    return this._dispatchers.GetValueOrDefault(protocol);
  }
}