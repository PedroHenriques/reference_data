namespace Notification.Configs;

public static class Cache
{
  public static string RedisConStr = Environment.GetEnvironmentVariable("REDIS_CON_STR")
    ?? throw new Exception("Could not get the 'REDIS_CON_STR' environment variable");

  public static string RedisConStrQueue = Environment.GetEnvironmentVariable("REDIS_CON_STR_QUEUE")
    ?? throw new Exception("Could not get the 'REDIS_CON_STR_QUEUE' environment variable");

  public static string ChangesQueueKey = Environment.GetEnvironmentVariable("DBLISTENER_CACHE_CHANGES_QUEUE_KEY")
    ?? throw new Exception("Could not get the 'DBLISTENER_CACHE_CHANGES_QUEUE_KEY' environment variable");
}