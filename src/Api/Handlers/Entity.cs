using MongoDB.Bson;
using EntityModel = Api.Models.Entity;
using Api.Configs;
using Toolkit.Types;

namespace Api.Handlers;

public class Entity
{
  public static async Task Create(IMongodb dbClient, EntityModel[] entities)
  {
    await dbClient.InsertMany<EntityModel>(Db.DbName, Db.ColName, entities);
  }

  public static async Task Replace(IMongodb dbClient, EntityModel entity)
  {
    if (entity.Id == null)
    {
      throw new Exception("Couldn't determine the Entity's ID.");
    }

    await dbClient.ReplaceOne<EntityModel>(Db.DbName, Db.ColName, entity,
      entity.Id);
  }

  public static async Task Delete(IMongodb dbClient, string id)
  {
    await dbClient.DeleteOne<EntityModel>(Db.DbName, Db.ColName, id);
  }

  public static async Task<FindResult<EntityModel>> Select(IMongodb dbClient,
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

    return await dbClient.Find<EntityModel>(Db.DbName, Db.ColName, page ?? 1,
      size ?? 50, matchDoc, false);
  }
}

