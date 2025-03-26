using NotifyUtils = Notification.Utils.Notify;
using Notification.Types;
using System.Diagnostics.CodeAnalysis;
using Toolkit.Types;

namespace Notification.Services;

public static class Notify
{
  [ExcludeFromCodeCoverage(Justification = "Not unit testable due to having an endless loop.")]
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