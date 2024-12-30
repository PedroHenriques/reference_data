using MongoDB.Bson;
using MongoDB.Driver;

namespace Api.Services;

public interface IDb
{
  public Task InsertOne<T>(string dbName, string collName, T document);
  public Task ReplaceOne<T>(string dbName, string collName, T document,
    string id);
  public Task DeleteOne<T>(string dbName, string collName, string id);
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
}