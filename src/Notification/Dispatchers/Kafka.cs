using Confluent.Kafka;
using KafkaConfigs = Notification.Configs.Kafka;
using Notification.Types;
using SharedLibs.Types;
using Toolkit.Types;
using Notification.Configs;

namespace Notification.Dispatchers;

public class Kafka : IDispatcher
{
  private readonly IKafka<NotifDataKafkaKey, NotifDataKafkaValue> _kafka;
  private readonly ILogger _logger;

  public Kafka(IKafka<NotifDataKafkaKey, NotifDataKafkaValue> eventBus, ILogger logger)
  {
    this._kafka = eventBus;
    this._logger = logger;
  }

  public Task Dispatch(NotifData data, string destination, Action<bool> callback)
  {
    Message<NotifDataKafkaKey, NotifDataKafkaValue> message = new Message<NotifDataKafkaKey, NotifDataKafkaValue>
    {
      Key = new NotifDataKafkaKey { Id = data.Id },
    };

    if (data.ChangeType != ChangeRecordTypes.Delete.Name)
    {
      message.Value = new NotifDataKafkaValue
      {
        Metadata = new NotifDataKafkaValueMetadata
        {
          Action = KafkaConfigs.MetadataActionMap[data.ChangeType],
          ActionDatetime = data.ChangeTime,
          EventDatetime = data.EventTime,
          CorrelationId = Guid.NewGuid().ToString(),
          Source = General.ProjectName,
        },
        Data = new NotifDataKafkaValueData
        {
          Id = data.Id,
          Entity = data.Entity,
          Document = data.Document,
        },
      };
    }

    this._kafka.Publish(
      destination,
      message,
      PublishHandler(callback, this._logger)
    );

    return Task.CompletedTask;
  }

  private static Action<DeliveryResult<NotifDataKafkaKey, NotifDataKafkaValue>?, Exception?> PublishHandler(
    Action<bool> callback, ILogger logger
  )
  {
    return (result, ex) =>
    {
      if (ex != null)
      {
        logger.Log(
          Microsoft.Extensions.Logging.LogLevel.Error,
          ex,
          ex.Message
        );
        callback(false);
        return;
      }

      if (result == null) { return; }

      bool persisted = result.Status != PersistenceStatus.NotPersisted;

      logger.Log(
        persisted ? Microsoft.Extensions.Logging.LogLevel.Information : Microsoft.Extensions.Logging.LogLevel.Error,
        null,
        $"Kafka Dispatcher - publish event callback: Document id = {result.Key.Id} | Status = {result.Status} | Partition = {result.Partition} | Offset = {result.Offset}"
      );
      callback(persisted);
    };
  }
}