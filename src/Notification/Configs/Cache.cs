using System.Diagnostics.CodeAnalysis;

namespace Notification.Configs;

[ExcludeFromCodeCoverage(Justification = "Not unit testable due to being a config static class.")]
public static class Cache
{
  public static string RedisConHost = Environment.GetEnvironmentVariable("REDIS_CON_HOST")
    ?? throw new Exception("Could not get the 'REDIS_CON_HOST' environment variable");

  public static string RedisConPort = Environment.GetEnvironmentVariable("REDIS_CON_PORT")
    ?? throw new Exception("Could not get the 'REDIS_CON_PORT' environment variable");

  public static string RedisPw = Environment.GetEnvironmentVariable("REDIS_PW")
    ?? throw new Exception("Could not get the 'REDIS_PW' environment variable");

  public static string RedisConHostQueue = Environment.GetEnvironmentVariable("REDIS_CON_HOST_QUEUE")
    ?? throw new Exception("Could not get the 'REDIS_CON_HOST_QUEUE' environment variable");

  public static string RedisConPortQueue = Environment.GetEnvironmentVariable("REDIS_CON_PORT_QUEUE")
    ?? throw new Exception("Could not get the 'REDIS_CON_PORT_QUEUE' environment variable");

  public static string RedisPwQueue = Environment.GetEnvironmentVariable("REDIS_PW_QUEUE")
    ?? throw new Exception("Could not get the 'REDIS_PW_QUEUE' environment variable");
}