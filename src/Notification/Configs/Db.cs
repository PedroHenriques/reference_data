namespace Notification.Configs;

public static class Db
{
  public static string ColName = Environment.GetEnvironmentVariable("MONGO_COL_NAME")
    ?? throw new Exception("Could not get the 'MONGO_COL_NAME' environment variable");
}