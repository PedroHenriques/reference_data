using DbListener.Configs;
using Newtonsoft.Json;
using SharedLibs.Types;

namespace DbListener.Services;

public static class DbStream
{
  public static async Task Watch(ICache cache, IQueue queue, IDb db)
  {
    string? resume = await cache.Get(Cache.ChangeResumeDataKey);

    ResumeData? resumeData = null;
    if (resume != null)
    {
      resumeData = JsonConvert.DeserializeObject<ResumeData>(resume);
    }

    await foreach (WatchData change in db.WatchDb(Db.DbName, resumeData))
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