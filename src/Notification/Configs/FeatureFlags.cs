using System.Diagnostics.CodeAnalysis;

namespace Notification.Configs;

[ExcludeFromCodeCoverage(Justification = "Not unit testable due to being a config static class.")]
public static class FeatureFlags
{
  public static string DispatcherKeyActive = Environment.GetEnvironmentVariable("LD_DISPATCHER_ACTIVE_KEY")
    ?? throw new Exception("Could not get the 'LD_DISPATCHER_ACTIVE_KEY' environment variable");
}