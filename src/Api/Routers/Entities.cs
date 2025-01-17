using EntityModel = Api.Models.Entity;
using EntityHandler = Api.Handlers.Entity;
using Microsoft.AspNetCore.Mvc;
using SharedLibs;

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
    Get();
  }

  private void Post()
  {
    this._app.MapPost(
      "/v1/entities/",
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
      "/v1/entities/{id}",
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
      "/v1/entities/{id}",
      async (IDb db, [FromRoute] string id) =>
      {
        await EntityHandler.Delete(db, id);
        return Results.Ok();
      }
    );
  }

  private void Get()
  {
    this._app.MapGet(
      "/v1/entities/",
      async (IDb db, [FromQuery] int page, [FromQuery] int pageSize) =>
      {
        var data = await EntityHandler.Select(db, page, pageSize);
        return TypedResults.Ok(data);
      }
    );
  }
}