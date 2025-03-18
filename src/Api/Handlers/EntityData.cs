using EntityModel = Api.Models.Entity;
using MongoDB.Bson;
using SharedLibs.Types;
using Api.Configs;

namespace Api.Handlers;

public class EntityData
{
  public static async Task<dynamic> Create(IDb dbClient, string entityId,
    dynamic data)
  {
    if (data.GetType().IsArray == false)
    {
      throw new Exception("The body of the request must be an array of documents.");
    }

    var findResult = await FindEntity(dbClient, entityId, null);
    string entityName = findResult.Data[0].Name;

    for (int i = 0; i < data.Length; i++)
    {
      data[i]._id = ObjectId.GenerateNewId();
    }

    await dbClient.InsertMany<dynamic>(Db.DbName, entityName, data);

    for (int i = 0; i < data.Length; i++)
    {
      data[i].id = data[i]._id.ToString();
      ((IDictionary<String, Object>)data[i]).Remove("_id");
    }
    return data;
  }

  public static async Task<dynamic> Replace(IDb dbClient, string entityId,
    string docId, dynamic data)
  {
    var findResult = await FindEntity(dbClient, entityId, null);

    string entityName = findResult.Data[0].Name;
    await dbClient.ReplaceOne<dynamic>(Db.DbName, entityName, data, docId);

    data.id = docId;
    return data;
  }

  public static async Task Delete(IDb dbClient, string entityId, string docId)
  {
    var findResult = await FindEntity(dbClient, entityId, null);

    string entityName = findResult.Data[0].Name;
    await dbClient.DeleteOne<EntityModel>(Db.DbName, entityName, docId);
  }

  public static async Task<FindResult<dynamic>> Select(IDb dbClient,
    string? entityId = null, string? entityName = null, string? docId = null,
    int? page = null, int? size = null, string? match = null)
  {
    if (entityId == null && entityName == null)
    {
      throw new Exception("Neither an entity Id nor an entity name were provided.");
    }

    var findResult = await FindEntity(dbClient, entityId, entityName);

    BsonDocument? matchDocId = null;
    if (docId != null)
    {
      matchDocId = new BsonDocument{
        { "_id", ObjectId.Parse(docId) }
      };
    }

    BsonDocument? matchFilter = null;
    if (match != null)
    {
      matchFilter = BsonDocument.Parse(match);
    }

    BsonDocument? matchDoc;
    if (matchDocId != null && matchFilter != null)
    {
      matchDoc = new BsonDocument{
        { "$and", new BsonArray{ matchDocId, matchFilter } },
      };
    }
    else
    {
      matchDoc = matchDocId ?? matchFilter;
    }

    var result = await dbClient.Find<dynamic>(Db.DbName, findResult.Data[0].Name,
      page ?? 1, size ?? Db.QueryPageSize, matchDoc, false);

    foreach (var item in result.Data)
    {
      item.id = item._id.ToString();
      ((IDictionary<String, Object>)item).Remove("_id");
    }

    return result;
  }

  private static async Task<FindResult<EntityModel>> FindEntity(IDb dbClient,
    string? entityId, string? entityName)
  {
    BsonDocument? findMatch;
    string noMatchErrorMsg;
    if (entityId != null)
    {
      findMatch = new BsonDocument { { "_id", ObjectId.Parse(entityId) } };
      noMatchErrorMsg = $"No valid entity with the ID '{entityId}' exists.";
    }
    else
    {
      findMatch = new BsonDocument { { "name", entityName } };
      noMatchErrorMsg = $"No valid entity with the NAME '{entityName}' exists.";
    }

    BsonDocument match = new BsonDocument {
      {
        "$and",
        new BsonArray {
          findMatch,
          new BsonDocument { { "deleted_at", BsonNull.Value } },
        }
      }
    };
    var findResult = await dbClient.Find<EntityModel>(Db.DbName, Db.ColName, 1, 1,
      match, false);

    if (findResult.Metadata.TotalCount == 0)
    {
      throw new Exception(noMatchErrorMsg);
    }

    return findResult;
  }
}