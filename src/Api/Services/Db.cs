using MongoDB.Bson;
using MongoDB.Driver;

namespace Api.Services;

public interface IDb
{
  public Task InsertOne<T>(string dbName, string collName, T document);
  public Task ReplaceOne<T>(string dbName, string collName, T document,
    string id);
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
      new BsonDocument(new Dictionary<string, dynamic>() {
        {
          "_id",  ObjectId.Parse(id)
        }
      }),
      document
    );

    if (replaceRes.ModifiedCount == 0)
    {
      throw new KeyNotFoundException($"Could not find the document with ID '{id}'");
    }
  }
}