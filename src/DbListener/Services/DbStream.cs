using DbListener.Configs;
using Newtonsoft.Json;
using SharedLibs.Types;
using Toolkit.Types;

namespace DbListener.Services;

public static class DbStream
{
  public static Task Watch(
    ICache cache, IQueue queue, IMongodb db, IFeatureFlags ff, ILogger logger,
    CancellationTokenSource mainCts
  )
  {
    CancellationTokenSource? watchCts = null;
    bool listenerActive = ff.GetBoolFlagValue(FeatureFlags.ListenerKeyActive);
    if (listenerActive)
    {
      logger.Log(
        Microsoft.Extensions.Logging.LogLevel.Information,
        null, "Feature flag is active. Subscribing to Mongo Stream."
      );

      watchCts = new CancellationTokenSource();
      WatchDb(cache, queue, db, logger, mainCts, watchCts.Token);
    }

    ff.SubscribeToValueChanges(
      FeatureFlags.ListenerKeyActive,
      ev =>
      {
        if (ev.NewValue.AsBool)
        {
          watchCts = new CancellationTokenSource();
          WatchDb(cache, queue, db, logger, mainCts, watchCts.Token);

          logger.Log(
            Microsoft.Extensions.Logging.LogLevel.Information,
            null, $"Feature flag value changed to 'TRUE'. Subscribing to Mongo Stream."
          );
        }
        else
        {
          if (watchCts == null) { return; }
          watchCts.Cancel();

          logger.Log(
            Microsoft.Extensions.Logging.LogLevel.Information,
            null, $"Feature flag value changed to 'FALSE'. Cancelling subscription to Mongo Stream."
          );
        }
      }
    );

    return Task.Delay(Timeout.Infinite, mainCts.Token);
  }

  private static async void WatchDb(
    ICache cache, IQueue queue, IMongodb db, ILogger logger,
    CancellationTokenSource mainCts, CancellationToken token
  )
  {
    try
    {
      string? resume = await cache.GetString(Cache.ChangeResumeDataKey);

      ResumeData? resumeData = null;
      if (resume != null)
      {
        resumeData = JsonConvert.DeserializeObject<ResumeData>(resume);
      }

      await foreach (WatchData change in db.WatchDb(Db.DbName, resumeData, token))
      {
        Microsoft.Extensions.Logging.LogLevel logLevel;
        if (Log.WatchKindLogLevels.TryGetValue(change.Kind, out logLevel) == false)
        {
          logLevel = Microsoft.Extensions.Logging.LogLevel.Information;
        }

        logger.Log(
          logLevel,
          change.Exception,
          $"Received Mongo Stream event of type '{change.Kind}' with change time '{change.ChangeTime}', resume data '{JsonConvert.SerializeObject(change.ResumeData)}', source '{JsonConvert.SerializeObject(change.Source)}' and health '{JsonConvert.SerializeObject(change.Health)}'."
        );

        if (change.Kind == WatchKind.Data && change.ChangeRecord != null)
        {
          await queue.Enqueue(
            Cache.ChangesQueueKey,
            new[] {
              JsonConvert.SerializeObject(new ChangeQueueItem{
                ChangeTime = change.ChangeTime ?? DateTime.Now,
                ChangeRecord = JsonConvert.SerializeObject(change.ChangeRecord),
                Source = JsonConvert.SerializeObject(change.Source),
              }),
            },
            Cache.ChangesQueueTtl
          );
        }

        if (change.ResumeData != null)
        {
          await cache.Set(
            Cache.ChangeResumeDataKey,
            JsonConvert.SerializeObject(change.ResumeData)
          );
        }
      }
    }
    catch (Exception ex)
    {
      mainCts.Cancel();
      logger.Log(
        Microsoft.Extensions.Logging.LogLevel.Error,
        ex, ex.Message
      );
    }
  }
}