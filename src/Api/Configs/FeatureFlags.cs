using System.Diagnostics.CodeAnalysis;

namespace Api.Configs;

[ExcludeFromCodeCoverage(Justification = "Not unit testable due to being a config static class.")]
public static class FeatureFlags
{
  public static string MasterKillSwitch = Environment.GetEnvironmentVariable("LD_KILL_SWITCH_KEY")
    ?? throw new Exception("Could not get the 'LD_KILL_SWITCH_KEY' environment variable");
}