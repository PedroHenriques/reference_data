using System.Dynamic;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using SharedLibs;
using SharedLibs.Types;
using SharedLibs.Types.Db;
using StackExchange.Redis;

namespace DbListener.Services;

public static class DbStream
{
  public static async Task Watch(ICache cache, IDb db)
  {
    RedisValue changeResume = await cache.Get(CacheTypes.String,
      "change_resume_data");

    ChangeStreamOptions? watchOpts = BuildStreamOpts(changeResume);

    await foreach (WatchData change in db.WatchDb("RefData", watchOpts))
    {
      await cache.Enqueue("mongo_changes", new[] {
        new RedisValue(JsonConvert.SerializeObject(new ChangeQueueItem{
          ChangeRecord = change.ChangeRecord,
          Source = JsonConvert.SerializeObject(change.Source),
        })),
      });

      await cache.Set(CacheTypes.String, "change_resume_data",
        new RedisValue(JsonConvert.SerializeObject(change.ResumeData)));
    }
  }

  private static ChangeStreamOptions? BuildStreamOpts(RedisValue data)
  {
    if (data.IsNullOrEmpty || data.HasValue == false)
    {
      return null;
    }

    ResumeData? resumeData = JsonConvert.DeserializeObject<ResumeData>(data);
    if (resumeData != null)
    {
      if (resumeData.Value.ResumeToken != null)
      {
        ExpandoObject? token = JsonConvert.DeserializeObject<ExpandoObject>(
          resumeData.Value.ResumeToken, new ExpandoObjectConverter());

        if (token != null)
        {
          return new ChangeStreamOptions
          {
            ResumeAfter = new BsonDocument(token),
          };
        }
      }

      if (resumeData.Value.ClusterTime != null)
      {
        return new ChangeStreamOptions
        {
          StartAtOperationTime = new BsonTimestamp(long.Parse(
            resumeData.Value.ClusterTime)),
        };
      }
    }

    return null;
  }
}