using Api.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using Toolkit.Types;

namespace Api.Configs;

public static class MongoIndexes
{
  public static void Create(IMongodb mongo)
  {
    mongo.CreateOneIndex<Entity>(
      Db.DbName, Db.ColName, new BsonDocument { { "name", 1 } },
      new CreateIndexOptions { Name = "name_unique", Unique = true }
    );
  }
}