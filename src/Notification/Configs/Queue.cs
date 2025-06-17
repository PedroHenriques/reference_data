using System.Diagnostics.CodeAnalysis;

namespace Notification.Configs;

[ExcludeFromCodeCoverage(Justification = "Not unit testable due to being a config static class.")]
public static class Queue
{
  public static string ChangesQueueKey = Environment.GetEnvironmentVariable("DBLISTENER_CHANGES_QUEUE_KEY")
    ?? throw new Exception("Could not get the 'DBLISTENER_CHANGES_QUEUE_KEY' environment variable");

  public static string DispatcherRetryQueueKey = Environment.GetEnvironmentVariable("DISPATCHER_RETRY_QUEUE_KEY")
    ?? throw new Exception("Could not get the 'DISPATCHER_RETRY_QUEUE_KEY' environment variable");

  public static int ChangesQueueRetryCount = Int32.Parse(
    Environment.GetEnvironmentVariable("CHANGES_QUEUE_RETRY_COUNT")
    ?? throw new Exception("Could not get the 'CHANGES_QUEUE_RETRY_COUNT' environment variable")
  );

  public static int DispatcherRetryQueueRetryCount = Int32.Parse(
    Environment.GetEnvironmentVariable("DISPATCHER_RETRY_COUNT")
    ?? throw new Exception("Could not get the 'DISPATCHER_RETRY_COUNT' environment variable")
  );
}