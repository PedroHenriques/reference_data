using EntityModel = Api.Models.Entity;
using EntityHandler = Api.Handlers.Entity;
using Microsoft.AspNetCore.Mvc;
using SharedLibs.Types.Db;

namespace Api.Routers;

// Not unit testable due to WebApplication not exposing an interface and
// the invoked methods being extension methods.
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
      async (IDb db, [FromBody] EntityModel[] entities) =>
      {
        await EntityHandler.Create(db, entities);
        return TypedResults.Ok(entities);
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
      async (IDb db, [FromQuery] int page, [FromQuery] int pageSize,
        [FromQuery] string? filter) =>
      {
        var data = await EntityHandler.Select(db, page, pageSize, null, filter);
        return TypedResults.Ok(data);
      }
    );

    this._app.MapGet(
      "/v1/entities/{id}",
      async (IDb db, [FromRoute] string id) =>
      {
        var data = await EntityHandler.Select(db, null, null, id);
        return TypedResults.Ok(data);
      }
    );
  }
}