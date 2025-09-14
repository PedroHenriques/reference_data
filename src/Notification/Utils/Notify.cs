using Newtonsoft.Json;
using ffConfigs = Notification.Configs.FeatureFlags;
using queueConfigs = Notification.Configs.Queue;
using dbConfigs = Notification.Configs.Db;
using Notification.Types;
using SharedLibs.Types;
using Toolkit.Types;
using Toolkit;

namespace Notification.Utils;

public enum NotifyMode
{
  MongoChanges,
  DispatcherRetry,
}

public static class Notify
{
  public static async Task ProcessMessage(IQueue queueDblistener, ICache cacheNotif,
    IQueue queueNotif, IDispatchers dispatchers, HttpClient httpClient,
    ILogger logger, NotifyMode mode, string processId
  )
  {
    string queueName = queueConfigs.ChangesQueueKey;
    string ffKey = ffConfigs.NotificationKeyActive;
    int retryThreashold = queueConfigs.ChangesQueueRetryCount;
    IQueue queue = queueDblistener;

    if (mode == NotifyMode.DispatcherRetry)
    {
      queueName = queueConfigs.DispatcherRetryQueueKey;
      ffKey = ffConfigs.RetryQueueKeyActive;
      retryThreashold = queueConfigs.DispatcherRetryQueueRetryCount;
      queue = queueNotif;
    }

    if (FeatureFlags.GetCachedBoolFlagValue(ffKey) == false)
    {
      return;
    }

    string? messageId = null;
    try
    {
      var (id, messageStr) = await queue.Dequeue(
        queueName, $"thread-{mode}-{processId}"
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
        await HandleEntitiesMessage(cacheNotif, logger, changeRecord);
      }
      else
      {
        await HandleDataMessage(
          cacheNotif, changeSource.CollName, message.ChangeTime,
          changeRecord, dispatchers, httpClient, logger,
          DispatcherHandler(queueNotif, logger, mode, messageId, message, changeRecord.Id),
          message.NotifConfigs
        );
      }

      if (mode == NotifyMode.MongoChanges)
      {
        await queue.Ack(queueName, messageId, false);
      }
    }
    catch (Exception)
    {
      if (messageId != null)
      {
        await queue.Nack(queueName, messageId, retryThreashold);
      }
      throw;
    }
  }

  private static Func<NotifConfig, Action<bool>> DispatcherHandler(
    IQueue queue, ILogger logger, NotifyMode mode, string messageId,
    ChangeQueueItem message, string documentId
  )
  {
    return (NotifConfig notifConfig) => async (bool success) =>
    {
      if (success)
      {
        if (mode == NotifyMode.DispatcherRetry)
        {
          await queue.Ack(queueConfigs.DispatcherRetryQueueKey, messageId);
        }

        logger.Log(
          Microsoft.Extensions.Logging.LogLevel.Information,
          null,
          $"Dispatcher sent notification successfully for document id: {documentId}"
        );
      }
      else
      {
        if (mode == NotifyMode.MongoChanges)
        {
          var retryMsg = new ChangeQueueItem
          {
            ChangeRecord = message.ChangeRecord,
            ChangeTime = message.ChangeTime,
            Source = message.Source,
            NotifConfigs = [notifConfig],
          };
          var insertedIds = await queue.Enqueue(
            queueConfigs.DispatcherRetryQueueKey,
            [JsonConvert.SerializeObject(retryMsg)]
          );

          logger.Log(
            Microsoft.Extensions.Logging.LogLevel.Error,
            null,
            $"Dispatcher failed to send notification for document id: '{documentId}' and dispatcher: '{notifConfig.Protocol}'. Sent to the retry queue with message id: {String.Join(" ", insertedIds)}"
          );
        }
        else
        {
          await queue.Nack(
            queueConfigs.DispatcherRetryQueueKey,
            messageId, queueConfigs.DispatcherRetryQueueRetryCount
          );

          logger.Log(
            Microsoft.Extensions.Logging.LogLevel.Error,
            null,
            $"Dispatcher failed to send notification for document id: '{documentId}' and dispatcher: '{notifConfig.Protocol}'. Nacked the message with id: {messageId}"
          );
        }
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
    HttpClient httpClient, ILogger logger,
    Func<NotifConfig, Action<bool>> callbackFactory, NotifConfig[]? notifConfigs
  )
  {
    if (notifConfigs == null)
    {
      var configsStr = await cache.GetString($"entity:{entityName}|notif configs");
      if (configsStr == null)
      {
        configsStr = await GetEntityInformation(httpClient, cache, logger, entityName);
      }

      var configs = JsonConvert.DeserializeObject<NotifConfig[]>(configsStr);
      if (configs == null)
      {
        return;
      }
      notifConfigs = configs;
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
          callbackFactory(notif)
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