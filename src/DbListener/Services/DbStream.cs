using DbListener.Configs;
using Newtonsoft.Json;
using SharedLibs.Types;
using Toolkit.Types;

namespace DbListener.Services;

public static class DbStream
{
  public static Task Watch(
    ICache cache, IQueue queue, IMongodb db, IFeatureFlags ff
  )
  {
    CancellationTokenSource? cts = null;
    bool listenerActive = ff.GetBoolFlagValue(FeatureFlags.ListenerKeyActive);
    if (listenerActive)
    {
      cts = new CancellationTokenSource();
      WatchDb(cache, queue, db, cts.Token);
    }

    ff.SubscribeToValueChanges(
      FeatureFlags.ListenerKeyActive,
      ev =>
      {
        if (ev.NewValue.AsBool)
        {
          cts = new CancellationTokenSource();
          WatchDb(cache, queue, db, cts.Token);
        }
        else
        {
          if (cts == null) { return; }
          cts.Cancel();
        }
      }
    );

    return Task.CompletedTask;
  }

  private static async void WatchDb(
    ICache cache, IQueue queue, IMongodb db, CancellationToken token
  )
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
}