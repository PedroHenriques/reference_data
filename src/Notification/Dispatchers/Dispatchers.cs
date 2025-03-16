using Notification.Types;
using SharedLibs.Types;

namespace Notification.Dispatchers;

public class Dispatchers : IDispatchers
{
  private readonly Dictionary<string, IDispatcher> _dispatchers;

  public Dispatchers(HttpClient httpClient)
  {
    this._dispatchers = new Dictionary<string, IDispatcher>
    {
      { "webhook", new Webhook(httpClient) },
    };
  }

  public IDispatcher? GetDispatcher(string protocol)
  {
    return this._dispatchers.GetValueOrDefault(protocol);
  }
}