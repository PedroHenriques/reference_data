using System.Dynamic;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using SharedLibs.Types.Db;

namespace SharedLibs.Utils;

public class Db
{
  public static ChangeStreamOptions? BuildStreamOpts(ResumeData resumeData)
  {
    if (resumeData.ResumeToken != null)
    {
      ExpandoObject? token = JsonConvert.DeserializeObject<ExpandoObject>(
        resumeData.ResumeToken, new ExpandoObjectConverter());

      if (token != null)
      {
        return new ChangeStreamOptions
        {
          ResumeAfter = new BsonDocument(token),
        };
      }
    }

    if (resumeData.ClusterTime != null)
    {
      return new ChangeStreamOptions
      {
        StartAtOperationTime = new BsonTimestamp(long.Parse(
          resumeData.ClusterTime)),
      };
    }

    return null;
  }
}