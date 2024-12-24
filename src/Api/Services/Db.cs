using MongoDB.Driver;

namespace Api.Services;

public interface IDb
{
  public void InsertOne<T>(string dbName, string collName, T document);
}

public class Db : IDb
{
  private readonly IMongoClient client;

  public Db(IMongoClient mongoClient)
  {
    this.client = mongoClient;
  }

  public async void InsertOne<T>(string dbName, string collName, T document) {
    IMongoDatabase? db = this.client.GetDatabase(dbName);
    if (db == null) {
      throw new Exception($"Could not find the database '{dbName}'.");
    }

    IMongoCollection<T>? dbColl = db.GetCollection<T>(collName);
    if (dbColl == null) {
      throw new Exception($"Could not find the collection '{collName}'.");
    }

    await dbColl.InsertOneAsync(document);
  }
}