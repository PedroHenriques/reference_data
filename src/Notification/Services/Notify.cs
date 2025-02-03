using NotifyUtils = Notification.Utils.Notify;
using SharedLibs.Types.Cache;
using Notification.Types;

namespace Notification.Services;

public static class Notify
{
  // Not unit testable due to endless loop
  public static async Task Watch(IQueue queue, ICache cache,
    IDispatchers dispatchers, HttpClient httpClient)
  {
    while (true)
    {
      try
      {
        await NotifyUtils.ProcessMessage(queue, cache, dispatchers, httpClient);
      }
      catch
      {
        // @TODO Log it
      }
    }
  }
}