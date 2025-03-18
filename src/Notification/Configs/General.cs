namespace Notification.Configs;

public static class General
{
  public static string ApiBaseUrl = Environment.GetEnvironmentVariable("API_BASE_URL")
    ?? throw new Exception("Could not get the 'API_BASE_URL' environment variable");
}