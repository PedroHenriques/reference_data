using System.Diagnostics.CodeAnalysis;
using Toolkit.Types;

namespace SharedLibs.Configs;

[ExcludeFromCodeCoverage(Justification = "Not unit testable due to being a config static class.")]
public static class FeatureFlags
{
  public static string EnvSdkKey = Environment.GetEnvironmentVariable("LD_ENV_SDK_KEY")
    ?? throw new Exception("Could not get the 'LD_ENV_SDK_KEY' environment variable");

  public static string ContextApiKey = Environment.GetEnvironmentVariable("LD_CONTEXT_API_KEY")
    ?? throw new Exception("Could not get the 'LD_CONTEXT_API_KEY' environment variable");

  public static string ContextName = Environment.GetEnvironmentVariable("LD_CONTEXT_NAME")
    ?? throw new Exception("Could not get the 'LD_CONTEXT_NAME' environment variable");

  public static Dictionary<string, EnvNames> EnvName = new Dictionary<string, EnvNames>
  {
    { "local", EnvNames.local },
    { "dev", EnvNames.dev },
    { "qua", EnvNames.qua },
    { "prd", EnvNames.prd },
  };
}