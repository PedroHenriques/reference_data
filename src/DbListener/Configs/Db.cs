using System.Diagnostics.CodeAnalysis;

namespace DbListener.Configs;

[ExcludeFromCodeCoverage(Justification = "Not unit testable due to being a config static class.")]
public static class Db
{
  public static string MongoConStr = Environment.GetEnvironmentVariable("MONGO_CON_STR")
    ?? throw new Exception("Could not get the 'MONGO_CON_STR' environment variable");

  public static string DbName = Environment.GetEnvironmentVariable("MONGO_DB_NAME")
    ?? throw new Exception("Could not get the 'MONGO_DB_NAME' environment variable");
}