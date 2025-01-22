using Newtonsoft.Json;
using SharedLibs;
using SharedLibs.Types;
using SharedLibs.Types.Cache;
using SharedLibs.Types.Db;

namespace DbListener.Services;

public static class DbStream
{
  public static async Task Watch(ICache cache, IQueue queue, IDb db)
  {
    string? resume = await cache.Get("change_resume_data");

    ResumeData? resumeData = null;
    if (resume != null)
    {
      resumeData = JsonConvert.DeserializeObject<ResumeData>(resume);
    }

    await foreach (WatchData change in db.WatchDb("RefData", resumeData))
    {
      await queue.Enqueue("mongo_changes", new[] {
        JsonConvert.SerializeObject(new ChangeQueueItem{
          ChangeRecord = JsonConvert.SerializeObject(change.ChangeRecord),
          Source = JsonConvert.SerializeObject(change.Source),
        }),
      });

      await cache.Set("change_resume_data",
        JsonConvert.SerializeObject(change.ResumeData));
    }
  }
}