using System.Diagnostics.CodeAnalysis;

namespace Notification.Configs;

[ExcludeFromCodeCoverage(Justification = "Not unit testable due to being a config static class.")]
public static class Queue
{
  public static int DispatcherRetryCount = Int32.Parse(
    Environment.GetEnvironmentVariable("DISPATCHER_RETRY_COUNT")
    ?? throw new Exception("Could not get the 'DISPATCHER_RETRY_COUNT' environment variable")
  );
}