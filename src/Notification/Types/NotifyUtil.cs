namespace Notification.Types;

public struct GetEntityInfoRes
{
  public required string NotifConfigsStr { get; set; }

  public required Task CacheNotifConfigs { get; set; }
}