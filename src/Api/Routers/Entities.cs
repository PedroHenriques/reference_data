using EntityModel = Api.Models.Entity;
using EntityHandler = Api.Handlers.Entity;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.CodeAnalysis;
using Toolkit.Types;

namespace Api.Routers;

[ExcludeFromCodeCoverage(Justification = "Not unit testable due to WebApplication not exposing an interface and the invoked methods being extension methods.")]
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
      async (IMongodb db, [FromBody] EntityModel[] entities) =>
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
      async (IMongodb db, EntityModel entity) =>
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
      async (IMongodb db, [FromRoute] string id) =>
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
      async (IMongodb db, [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null, [FromQuery] string? filter = null) =>
      {
        var data = await EntityHandler.Select(db, page, pageSize, null, filter);
        return TypedResults.Ok(data);
      }
    );

    this._app.MapGet(
      "/v1/entities/{id}",
      async (IMongodb db, [FromRoute] string id) =>
      {
        var data = await EntityHandler.Select(db, null, null, id);
        return TypedResults.Ok(data);
      }
    );
  }
}