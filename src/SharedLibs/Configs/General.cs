using System.Diagnostics.CodeAnalysis;

namespace SharedLibs.Configs;

[ExcludeFromCodeCoverage(Justification = "Not unit testable due to being a config static class.")]
public static class General
{
  public static string DeploymentEnv = Environment.GetEnvironmentVariable("DEPLOYMENT_ENV")
    ?? throw new Exception("Could not get the 'DEPLOYMENT_ENV' environment variable");
}