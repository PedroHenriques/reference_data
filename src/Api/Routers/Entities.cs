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
    Delete();
  }

  private void Post()
  {
    this._app.MapPost(
      "/entities/",
      async (EntityModel entity, IDb db) =>
      {
        await EntityHandler.Create(db, entity);
        return TypedResults.Ok(entity);
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
        return TypedResults.Ok(entity);
      }
    );
  }

  private void Delete()
  {
    this._app.MapDelete(
      "/entities/{id}",
      async (IDb db, [FromRoute] string id) =>
      {
        await EntityHandler.Delete(db, id);
        return Results.Ok();
      }
    );
  }
}