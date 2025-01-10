using SharedLibs.Types.Db;
using MongoDB.Bson;
using MongoDB.Driver;

namespace SharedLibs;

public interface IDb
{
  public Task InsertOne<T>(string dbName, string collName, T document);
  public Task ReplaceOne<T>(string dbName, string collName, T document,
    string id);
  public Task DeleteOne<T>(string dbName, string collName, string id);
  public Task<FindResult<T>> Find<T>(string dbName, string collName, int page,
    int size, BsonDocument? match, bool showDeleted);
  public Task<IChangeStreamCursor<ChangeStreamDocument<BsonDocument>>> WatchDb(
    string dbName, ChangeStreamOptions? opts);
}

public class Db : IDb
{
  private readonly IMongoClient _client;

  public Db(IMongoClient mongoClient)
  {
    this._client = mongoClient;
  }

  public async Task InsertOne<T>(string dbName, string collName, T document)
  {
    IMongoDatabase db = this._client.GetDatabase(dbName);
    IMongoCollection<T> dbColl = db.GetCollection<T>(collName);

    await dbColl.InsertOneAsync(document);
  }

  public async Task ReplaceOne<T>(string dbName, string collName, T document,
    string id)
  {
    IMongoDatabase db = this._client.GetDatabase(dbName);
    IMongoCollection<T> dbColl = db.GetCollection<T>(collName);

    ReplaceOneResult replaceRes = await dbColl.ReplaceOneAsync(
      new BsonDocument {
        {
          "_id",  ObjectId.Parse(id)
        }
      },
      document
    );

    if (replaceRes.MatchedCount == 0)
    {
      throw new KeyNotFoundException($"Could not find the document with ID '{id}'");
    }

    if (replaceRes.ModifiedCount == 0)
    {
      throw new Exception($"Could not replace the document with ID '{id}'");
    }
  }

  public async Task DeleteOne<T>(string dbName, string collName, string id)
  {
    IMongoDatabase db = this._client.GetDatabase(dbName);
    IMongoCollection<T> dbColl = db.GetCollection<T>(collName);

    UpdateResult updateRes = await dbColl.UpdateOneAsync(
      new BsonDocument {
        {
          "_id",  ObjectId.Parse(id)
        }
      },
      new BsonDocument {
        {
          "$currentDate", new BsonDocument {
            { "deleted_at", true }
          }
        }
      }
    );

    if (updateRes.MatchedCount == 0)
    {
      throw new KeyNotFoundException($"Could not find the document with ID '{id}'");
    }

    if (updateRes.ModifiedCount == 0)
    {
      throw new Exception($"Could not update the document with ID '{id}'");
    }
  }

  public async Task<FindResult<T>> Find<T>(string dbName, string collName,
    int page, int size, BsonDocument? match, bool showDeleted)
  {
    IMongoDatabase db = this._client.GetDatabase(dbName);
    IMongoCollection<T> dbColl = db.GetCollection<T>(collName);

    List<BsonDocument> stages = new List<BsonDocument>();
    BsonDocument showActiveMatch = new BsonDocument {
      { "deleted_at", BsonNull.Value }
    };

    BsonDocument? matchContent = null;
    if (match != null && showDeleted == false)
    {
      matchContent = new BsonDocument {
        {
          "$and",
          new BsonArray { match, showActiveMatch }
        }
      };
    }
    else if (match != null)
    {
      matchContent = match;
    }
    else if (showDeleted == false)
    {
      matchContent = showActiveMatch;
    }

    if (matchContent != null)
    {
      stages.Add(new BsonDocument {
        { "$match", matchContent }
      });
    }

    stages.Add(new BsonDocument
    {
      {
        "$sort", new BsonDocument
        {
          { "_id", 1 }
        }
      }
    });

    stages.Add(new BsonDocument
    {
      {
        "$facet", new BsonDocument {
          { "metadata", new BsonArray {
            new BsonDocument { { "$count", "totalCount" } }
          } },
          { "data", new BsonArray {
            new BsonDocument { { "$skip", (page - 1) * size } },
            new BsonDocument { { "$limit", size } }
          } }
        }
      }
    });

    IAsyncCursor<AggregateResult<T>> resultCursor = await dbColl.AggregateAsync(
      PipelineDefinition<T, AggregateResult<T>>.Create(stages)
    );

    AggregateResult<T> results = await resultCursor.FirstAsync();
    int totalCount = results.Metadata.Length == 0 ? 0 : results.Metadata.First()
      .TotalCount;

    return new FindResult<T>
    {
      Metadata = new FindResultMetadata
      {
        Page = page,
        PageSize = size,
        TotalCount = totalCount,
        TotalPages = (int)Math.Ceiling((double)totalCount / size)
      },
      Data = results.Data
    };
  }

  public Task<IChangeStreamCursor<ChangeStreamDocument<BsonDocument>>> WatchDb(
    string dbName, ChangeStreamOptions? opts = null)
  {
    IMongoDatabase db = this._client.GetDatabase(dbName);

    return db.WatchAsync(opts);
  }
}