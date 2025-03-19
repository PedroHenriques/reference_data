using System.Diagnostics.CodeAnalysis;

namespace Notification.Configs;

[ExcludeFromCodeCoverage(Justification = "Not unit testable due to being a config static class.")]
public static class Db
{
  public static string ColName = Environment.GetEnvironmentVariable("MONGO_COL_NAME")
    ?? throw new Exception("Could not get the 'MONGO_COL_NAME' environment variable");
}