using Newtonsoft.Json;
using ffConfigs = Notification.Configs.FeatureFlags;
using queueConfigs = Notification.Configs.Queue;
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
    IDispatchers dispatchers, HttpClient httpClient, ILogger logger,
    string processId
  )
  {
    if (FeatureFlags.GetCachedBoolFlagValue(ffConfigs.NotificationKeyActive) == false)
    {
      return;
    }

    string? messageId = null;
    try
    {
      var (id, messageStr) = await queue.Dequeue(
        cacheConfigs.ChangesQueueKey, $"thread-{processId}"
      );
      if (String.IsNullOrEmpty(id) || String.IsNullOrEmpty(messageStr))
      {
        return;
      }
      messageId = id;

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
        await HandleEntitiesMessage(cache, logger, changeRecord);
      }
      else
      {
        await HandleDataMessage(
          cache, changeSource.CollName, message.ChangeTime,
          changeRecord, dispatchers, httpClient, logger,
          DispatcherHandler(queue, logger, changeRecord.Id)
        );
      }

      await queue.Ack(cacheConfigs.ChangesQueueKey, messageId);
    }
    catch (Exception)
    {
      if (messageId != null)
      {
        await queue.Nack(
          cacheConfigs.ChangesQueueKey, messageId, queueConfigs.DispatcherRetryCount
        );
      }
      throw;
    }
  }

  private static Action<bool> DispatcherHandler(
    IQueue queue, ILogger logger, string documentId
  )
  {
    return (bool success) =>
    {
      if (success)
      {
        logger.Log(
          Microsoft.Extensions.Logging.LogLevel.Information,
          null,
          $"Dispatcher sent notification successfully for document id: {documentId}"
        );
      }
      else
      {
        // @TODO: enqueue into dispatcher retry queue
        logger.Log(
          Microsoft.Extensions.Logging.LogLevel.Error,
          null,
          $"Dispatcher failed send notification for document id: {documentId}"
        );
      }
    };
  }

  private static async Task HandleEntitiesMessage(ICache cache, ILogger logger,
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
      logger.Log(
        Microsoft.Extensions.Logging.LogLevel.Error,
        null,
        $"Failed to store in cache the key: 'entity:{changeRecord.Document["name"]}|notif configs'"
      );
    }
  }

  private static async Task HandleDataMessage(ICache cache, string entityName,
    DateTime changeTime, ChangeRecord changeRecord, IDispatchers dispatchers,
    HttpClient httpClient, ILogger logger, Action<bool> callback)
  {
    var configsStr = await cache.GetString($"entity:{entityName}|notif configs");
    if (configsStr == null)
    {
      configsStr = await GetEntityInformation(httpClient, cache, logger, entityName);
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
      catch (Exception ex)
      {
        logger.Log(
          Microsoft.Extensions.Logging.LogLevel.Warning,
          ex,
          ex.Message
        );
      }
    }
  }

  private static async Task<string> GetEntityInformation(
    HttpClient httpClient, ICache cache, ILogger logger, string entityName)
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
      logger,
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