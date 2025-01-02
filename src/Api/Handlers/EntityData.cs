using EntityModel = Api.Models.Entity;
using Api.Services;
using MongoDB.Bson;

namespace Api.Handlers;

public class EntityData
{
  private readonly static string _dbName = "RefData";

  public static async Task<object> Create(IDb dbClient, string entityId,
    dynamic data)
  {
    BsonDocument match = new BsonDocument {
      {
        "$and",
        new BsonArray {
          new BsonDocument { { "_id", ObjectId.Parse(entityId) } },
          new BsonDocument { { "deleted_at", BsonNull.Value } },
        }
      }
    };
    var findResult = await dbClient.Find<EntityModel>(
      _dbName, "Entities", 1, 1, match);

    if (findResult.Metadata.TotalCount == 0)
    {
      throw new Exception($"No valid entity with the ID '{entityId}' exists.");
    }

    string entityName = findResult.Data[0].Name;
    ObjectId id = ObjectId.GenerateNewId();
    data._id = id;

    await dbClient.InsertOne<dynamic>(_dbName, entityName, data);

    data.id = id.ToString();
    ((IDictionary<String, Object>)data).Remove("_id");
    return data;
  }
}