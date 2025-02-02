using Newtonsoft.Json;
using Notification.Types;
using SharedLibs.Types.Cache;
using SharedLibs.Types.Db;

namespace Notification.Utils;

public static class Notify
{
  private static readonly string _queueName = "mongo_changes";

  public static async Task ProcessMessage(IQueue queue, ICache cache,
    IDispatchers dispatchers)
  {
    string messageStr = await queue.Dequeue(_queueName);
    if (String.IsNullOrEmpty(messageStr))
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

      if (changeSource.CollName == "Entities")
      {
        await HandleEntitiesMessage(cache, changeRecord);
      }

      await queue.Ack(_queueName, messageStr);
    }
    catch
    {
      // @TODO: call nack()
    }
  }

  private static async Task HandleEntitiesMessage(ICache cache,
    ChangeRecord changeRecord)
  {
    if (changeRecord.Document == null)
    {
      return;
    }
    if (
      changeRecord.Document.ContainsKey("notif_configs") == false ||
      changeRecord.Document["notif_configs"] == "null"
    )
    {
      changeRecord.Document["notif_configs"] = "";
    }

    var result = await cache.Set(
      $"entity:{changeRecord.Document["name"]}|notif configs",
      changeRecord.Document["notif_configs"]
    );

    if (result == false)
    {
      // @TODO: Log it
    }
  }
}