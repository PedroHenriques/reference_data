using DbListener.Configs;
using Newtonsoft.Json;
using SharedLibs.Types;
using Toolkit.Types;

namespace DbListener.Services;

public static class DbStream
{
  public static Task Watch(
    ICache cache, IQueue queue, IMongodb db, IFeatureFlags ff, ILogger logger
  )
  {
    CancellationTokenSource? cts = null;
    bool listenerActive = ff.GetBoolFlagValue(FeatureFlags.ListenerKeyActive);
    if (listenerActive)
    {
      logger.Log(
        Microsoft.Extensions.Logging.LogLevel.Information,
        null, "Feature flag is active. Subscribing to Mongo Stream."
      );

      cts = new CancellationTokenSource();
      WatchDb(cache, queue, db, logger, cts.Token);
    }

    ff.SubscribeToValueChanges(
      FeatureFlags.ListenerKeyActive,
      ev =>
      {
        if (ev.NewValue.AsBool)
        {
          cts = new CancellationTokenSource();
          WatchDb(cache, queue, db, logger, cts.Token);

          logger.Log(
            Microsoft.Extensions.Logging.LogLevel.Information,
            null, $"Feature flag value changed to 'TRUE'. Subscribing to Mongo Stream."
          );
        }
        else
        {
          if (cts == null) { return; }
          cts.Cancel();

          logger.Log(
            Microsoft.Extensions.Logging.LogLevel.Information,
            null, $"Feature flag value changed to 'FALSE'. Cancelling subscription to Mongo Stream."
          );
        }
      }
    );

    return Task.CompletedTask;
  }

  private static async void WatchDb(
    ICache cache, IQueue queue, IMongodb db, ILogger logger,
    CancellationToken token
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
        if (change.ChangeRecord != null)
        {
          await queue.Enqueue(Cache.ChangesQueueKey, new[] {
            JsonConvert.SerializeObject(new ChangeQueueItem{
              ChangeTime = change.ChangeTime,
              ChangeRecord = JsonConvert.SerializeObject(change.ChangeRecord),
              Source = JsonConvert.SerializeObject(change.Source),
            }),
          });
        }

        await cache.Set(Cache.ChangeResumeDataKey,
          JsonConvert.SerializeObject(change.ResumeData));
      }
    }
    catch (Exception ex)
    {
      logger.Log(
        Microsoft.Extensions.Logging.LogLevel.Error,
        ex, ex.Message
      );
    }
  }
}