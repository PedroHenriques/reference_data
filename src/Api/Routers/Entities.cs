using EntityModel = Api.Models.Entity;
using Api.Services;
using EntityHandler = Api.Handlers.Entity;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routers;

public class Entities
{
  private readonly WebApplication _app;
  public Entities(WebApplication app)
  {
    this._app = app;

    Post();
    Put();
  }

  private void Post()
  {
    this._app.MapPost(
      "/entities/",
      async (EntityModel entity, IDb db) =>
      {
        await EntityHandler.Create(db, entity);
        return TypedResults.Ok<EntityModel>(entity);
      }
    );
  }

  private void Put()
  {
    this._app.MapPut(
      "/entities/{id}",
      async (EntityModel entity, IDb db) =>
      {
        await EntityHandler.Replace(db, entity);
        return TypedResults.Ok<EntityModel>(entity);
      }
    );
  }
}