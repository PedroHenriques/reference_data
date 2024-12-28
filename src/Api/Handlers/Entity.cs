using Api.Services;
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
}

