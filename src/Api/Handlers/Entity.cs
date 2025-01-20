using MongoDB.Bson;
using SharedLibs;
using SharedLibs.Types.Db;
using EntityModel = Api.Models.Entity;

namespace Api.Handlers;

public class Entity
{
  private readonly static string _dbName = "RefData";
  private readonly static string _dbCollName = "Entities";

  public static async Task Create(IDb dbClient, EntityModel entity)
  {
    await dbClient.InsertOne<EntityModel>(_dbName, _dbCollName, entity);
  }

  public static async Task Replace(IDb dbClient, EntityModel entity)
  {
    if (entity.Id == null)
    {
      throw new Exception("Couldn't determine the Entity's ID.");
    }

    await dbClient.ReplaceOne<EntityModel>(_dbName, _dbCollName, entity,
      entity.Id);
  }

  public static async Task Delete(IDb dbClient, string id)
  {
    await dbClient.DeleteOne<EntityModel>(_dbName, _dbCollName, id);
  }

  public static async Task<FindResult<EntityModel>> Select(IDb dbClient,
    int? page = null, int? size = null, string? id = null, string? match = null)
  {
    BsonDocument? matchId = null;
    if (id != null)
    {
      matchId = new BsonDocument{
        { "_id", ObjectId.Parse(id) }
      };
    }

    BsonDocument? matchFilter = null;
    if (match != null)
    {
      matchFilter = BsonDocument.Parse(match);
    }

    BsonDocument? matchDoc;
    if (matchId != null && matchFilter != null)
    {
      matchDoc = new BsonDocument{
        { "$and", new BsonArray{ matchId, matchFilter } },
      };
    }
    else
    {
      matchDoc = matchId ?? matchFilter;
    }

    return await dbClient.Find<EntityModel>(_dbName, _dbCollName, page ?? 1,
      size ?? 50, matchDoc, false);
  }
}

