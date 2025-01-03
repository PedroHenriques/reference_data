using EntityModel = Api.Models.Entity;
using Api.Services;
using MongoDB.Bson;
using System.Dynamic;
using Api.Services.Types.Db;

namespace Api.Handlers;

public class EntityData
{
  private readonly static string _dbName = "RefData";

  public static async Task<object> Create(IDb dbClient, string entityId,
    dynamic data)
  {
    var findResult = await FindEntity(dbClient, entityId);

    string entityName = findResult.Data[0].Name;
    ObjectId id = ObjectId.GenerateNewId();
    data._id = id;

    await dbClient.InsertOne<dynamic>(_dbName, entityName, data);

    data.id = id.ToString();
    ((IDictionary<String, Object>)data).Remove("_id");
    return data;
  }

  public static async Task<object> Replace(IDb dbClient, string entityId,
    string docId, dynamic data)
  {
    var findResult = await FindEntity(dbClient, entityId);

    string entityName = findResult.Data[0].Name;
    await dbClient.ReplaceOne<dynamic>(_dbName, entityName, data, docId);

    data.id = docId;
    return data;
  }

  private static async Task<FindResult<EntityModel>> FindEntity(IDb dbClient,
    string entityId)
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
    
    return findResult;
  }
}