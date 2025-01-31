using NotifyUtils = Notification.Utils.Notify;
using SharedLibs.Types.Cache;

namespace Notification.Services;

public static class Notify
{
  // Not unit testable due to endless loop
  public static async Task Watch(IQueue queue, ICache cache)
  {
    while (true)
    {
      try
      {
        await NotifyUtils.ProcessMessage(queue, cache);
      }
      catch
      {
        // @TODO Log it
      }
    }
  }
}