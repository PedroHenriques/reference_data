using NotifyUtils = Notification.Utils.Notify;
using SharedLibs.Types.Cache;
using Notification.Types;

namespace Notification.Services;

public static class Notify
{
  // Not unit testable due to endless loop
  public static async Task Watch(IQueue queue, ICache cache,
    IDispatchers dispatchers)
  {
    while (true)
    {
      try
      {
        await NotifyUtils.ProcessMessage(queue, cache, dispatchers);
      }
      catch
      {
        // @TODO Log it
      }
    }
  }
}