using System.Diagnostics.CodeAnalysis;

namespace Notification.Configs;

[ExcludeFromCodeCoverage(Justification = "Not unit testable due to being a config static class.")]
public static class Cache
{
  public static string RedisConHost = Environment.GetEnvironmentVariable("REDIS_CON_HOST")
    ?? throw new Exception("Could not get the 'REDIS_CON_HOST' environment variable");

  public static string RedisConPort = Environment.GetEnvironmentVariable("REDIS_CON_PORT")
    ?? throw new Exception("Could not get the 'REDIS_CON_PORT' environment variable");

  public static string RedisConHostQueue = Environment.GetEnvironmentVariable("REDIS_CON_HOST_QUEUE")
    ?? throw new Exception("Could not get the 'REDIS_CON_HOST_QUEUE' environment variable");

  public static string RedisConPortQueue = Environment.GetEnvironmentVariable("REDIS_CON_PORT_QUEUE")
    ?? throw new Exception("Could not get the 'REDIS_CON_PORT_QUEUE' environment variable");

  public static string ChangesQueueKey = Environment.GetEnvironmentVariable("DBLISTENER_CACHE_CHANGES_QUEUE_KEY")
    ?? throw new Exception("Could not get the 'DBLISTENER_CACHE_CHANGES_QUEUE_KEY' environment variable");
}