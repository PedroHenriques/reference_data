using Newtonsoft.Json;
using ffConfigs = Notification.Configs.FeatureFlags;
using cacheConfigs = Notification.Configs.Cache;
using dbConfigs = Notification.Configs.Db;
using Notification.Types;
using SharedLibs.Types;
using Toolkit.Types;
using Toolkit;

namespace Notification.Utils;

public static class Notify
{
  public static async Task ProcessMessage(IQueue queue, ICache cache,
    IDispatchers dispatchers, HttpClient httpClient, string processId)
  {
    if (FeatureFlags.GetCachedBoolFlagValue(ffConfigs.NotificationKeyActive) == false)
    {
      return;
    }

    var (messageId, messageStr) = await queue.Dequeue(cacheConfigs.ChangesQueueKey, $"thread-{processId}");
    if (String.IsNullOrEmpty(messageId) || String.IsNullOrEmpty(messageStr))
    {
      return;
    }

    try
    {
      var message = JsonConvert.DeserializeObject<ChangeQueueItem>(messageStr);
      if (message.Source == null || message.ChangeRecord == null)
      {
        return;
      }
      var changeSource = JsonConvert.DeserializeObject<ChangeSource>(
        message.Source);
      var changeRecord = JsonConvert.DeserializeObject<ChangeRecord>(
        message.ChangeRecord);

      if (changeSource.CollName == dbConfigs.ColName)
      {
        await HandleEntitiesMessage(cache, changeRecord);
      }
      else
      {
        await HandleDataMessage(
          cache, changeSource.CollName, message.ChangeTime,
          changeRecord, dispatchers, httpClient,
          DispatcherHandler(queue, messageStr)
        );
      }

      await queue.Ack(cacheConfigs.ChangesQueueKey, messageId);
    }
    catch (Exception e)
    {
      await queue.Nack(cacheConfigs.ChangesQueueKey, messageId, 2);
      Console.WriteLine(e);
      Console.WriteLine(e.Message);
    }
  }

  private static Action<bool> DispatcherHandler(IQueue queue, string messageStr)
  {
    return (bool success) =>
    {
      if (success == false)
      {
        // @TODO: call nack()
        Console.WriteLine($"Dispatcher signaled a failure in dispatching the message: '{messageStr}'");
      }
    };
  }

  private static async Task HandleEntitiesMessage(ICache cache,
    ChangeRecord changeRecord)
  {
    if (changeRecord.Document == null)
    {
      return;
    }
    if (
      changeRecord.Document.ContainsKey("notifConfigs") == false ||
      changeRecord.Document["notifConfigs"] == null
    )
    {
      changeRecord.Document["notifConfigs"] = "";
    }

    var result = await cache.Set(
      $"entity:{changeRecord.Document["name"]}|notif configs",
      JsonConvert.SerializeObject(changeRecord.Document["notifConfigs"])
    );

    if (result == false)
    {
      // @TODO: Log it
    }
  }

  private static async Task HandleDataMessage(ICache cache, string entityName,
    DateTime changeTime, ChangeRecord changeRecord, IDispatchers dispatchers,
    HttpClient httpClient, Action<bool> callback)
  {
    var configsStr = await cache.GetString($"entity:{entityName}|notif configs");
    if (configsStr == null)
    {
      configsStr = await GetEntityInformation(httpClient, cache, entityName);
    }

    var notifConfigs = JsonConvert.DeserializeObject<NotifConfig[]>(configsStr);
    if (notifConfigs == null)
    {
      return;
    }

    if (changeRecord.Document != null)
    {
      changeRecord.Document.Add("id", changeRecord.Id);
      changeRecord.Document.Remove("_id");
    }

    foreach (var notif in notifConfigs)
    {
      try
      {
        var dispatcher = dispatchers.GetDispatcher(notif.Protocol);
        if (dispatcher == null)
        {
          continue;
        }

        var _ = dispatcher.Dispatch(
          new NotifData
          {
            EventTime = DateTime.Now,
            ChangeTime = changeTime,
            ChangeType = changeRecord.ChangeType.Name,
            Entity = entityName,
            Id = changeRecord.Id,
            Document = changeRecord.Document,
          },
          notif.TargetURL,
          callback
        );
      }
      catch
      {
        // @TODO: Log it
      }
    }
  }

  private static async Task<string> GetEntityInformation(
    HttpClient httpClient, ICache cache, string entityName)
  {
    var response = await httpClient.GetAsync(
      "/v1/entities/?filter={\"name\":\"" + entityName + "\"}");
    var resDataStr = await response.Content.ReadAsStringAsync();
    var resData = JsonConvert.DeserializeObject<FindResult<dynamic>>(resDataStr);

    string notifConfigsStr = "";
    var document = new Dictionary<string, dynamic?> {
      { "name", $"{entityName}" },
    };
    if (resData.Metadata.TotalCount > 0)
    {
      notifConfigsStr = JsonConvert.SerializeObject(resData.Data[0].notifConfigs);
      document.Add("notifConfigs", resData.Data[0].notifConfigs);
    }

    var _ = HandleEntitiesMessage(
      cache,
      new ChangeRecord
      {
        Id = "",
        ChangeType = ChangeRecordTypes.Insert,
        Document = document,
      }
    );

    return notifConfigsStr;
  }
}