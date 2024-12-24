using Api.Services;
using EntityModel = Api.Models.Entity;

namespace Api.Handlers;

public class Entity
{
  private readonly static string dbName = "RefData";
  private readonly static string dbCollName = "Entities";

  public static void Create(IDb dbClient, EntityModel entity)
  {
    dbClient.InsertOne<EntityModel>(dbName, dbCollName, entity);
  }
}

