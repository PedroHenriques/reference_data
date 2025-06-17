using System.Diagnostics.CodeAnalysis;

namespace Notification.Configs;

[ExcludeFromCodeCoverage(Justification = "Not unit testable due to being a config static class.")]
public static class General
{
  public static string ApiBaseUrl = Environment.GetEnvironmentVariable("API_BASE_URL")
    ?? throw new Exception("Could not get the 'API_BASE_URL' environment variable");

  public static string ApiPort = Environment.GetEnvironmentVariable("API_PORT")
    ?? throw new Exception("Could not get the 'API_PORT' environment variable");

  public static int NumberProcesses = Int32.Parse(
    Environment.GetEnvironmentVariable("NUM_PROCESSES") ??
    throw new Exception("Could not get the 'NUM_PROCESSES' environment variable")
  );

  public static int NumberProcessesRetry = Int32.Parse(
    Environment.GetEnvironmentVariable("NUM_PROCESSES_RETRY") ??
    throw new Exception("Could not get the 'NUM_PROCESSES_RETRY' environment variable")
  );

  public static string ProjectName = Environment.GetEnvironmentVariable("PROJECT_NAME")
    ?? throw new Exception("Could not get the 'PROJECT_NAME' environment variable");
}