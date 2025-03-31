using System.Diagnostics.CodeAnalysis;

namespace DbListener.Configs;

[ExcludeFromCodeCoverage(Justification = "Not unit testable due to being a config static class.")]
public static class FeatureFlags
{
  public static string ListenerKeyActive = Environment.GetEnvironmentVariable("LD_DBLISTENER_ACTIVE_KEY")
    ?? throw new Exception("Could not get the 'LD_DBLISTENER_ACTIVE_KEY' environment variable");
}