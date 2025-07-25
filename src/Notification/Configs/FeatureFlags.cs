using System.Diagnostics.CodeAnalysis;

namespace Notification.Configs;

[ExcludeFromCodeCoverage(Justification = "Not unit testable due to being a config static class.")]
public static class FeatureFlags
{
  public static string NotificationKeyActive = Environment.GetEnvironmentVariable("LD_NOTIFICATION_ACTIVE_KEY")
    ?? throw new Exception("Could not get the 'LD_NOTIFICATION_ACTIVE_KEY' environment variable");

  public static string RetryQueueKeyActive = Environment.GetEnvironmentVariable("LD_NOTIFICATION_RETRY_ACTIVE_KEY")
    ?? throw new Exception("Could not get the 'LD_NOTIFICATION_RETRY_ACTIVE_KEY' environment variable");
}