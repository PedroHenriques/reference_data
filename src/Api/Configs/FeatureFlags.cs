using System.Diagnostics.CodeAnalysis;

namespace Api.Configs;

[ExcludeFromCodeCoverage(Justification = "Not unit testable due to being a config static class.")]
public static class FeatureFlags
{
  public static string ApiKeyActive = Environment.GetEnvironmentVariable("LD_API_ACTIVE_KEY")
    ?? throw new Exception("Could not get the 'LD_API_ACTIVE_KEY' environment variable");
}