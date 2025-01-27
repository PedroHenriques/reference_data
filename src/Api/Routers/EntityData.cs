using System.Dynamic;
using EntityDataHandler = Api.Handlers.EntityData;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using SharedLibs.Types.Db;

namespace Api.Routers;

// Not unit testable due to WebApplication not exposing an interface and
// the invoked methods being extension methods.
public class EntityData
{
  private readonly WebApplication _app;

  public EntityData(WebApplication app)
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
      "/v1/data/{entityId}/",
      async (IDb db, [FromRoute] string entityId, [FromBody] dynamic body) =>
      {
        var bodyObject = JsonConvert.DeserializeObject<ExpandoObject[]>(
          body.ToString(), new ExpandoObjectConverter());

        var result = await EntityDataHandler.Create(db, entityId, bodyObject);

        return TypedResults.Ok(result);
      }
    );
  }

  private void Put()
  {
    this._app.MapPut(
      "/v1/data/{entityId}/{docId}",
      async ([FromRoute] string entityId, [FromRoute] string docId,
        [FromBody] dynamic body, IDb db) =>
      {
        var bodyObject = JsonConvert.DeserializeObject<ExpandoObject>(
          body.ToString(), new ExpandoObjectConverter());

        var insertedDoc = await EntityDataHandler.Replace(db, entityId, docId,
          bodyObject);

        return TypedResults.Ok(insertedDoc);
      }
    );
  }

  private void Delete()
  {
    this._app.MapDelete(
      "/v1/data/{entityId}/{docId}",
      async ([FromRoute] string entityId, [FromRoute] string docId, IDb db) =>
      {
        await EntityDataHandler.Delete(db, entityId, docId);
        return Results.Ok();
      }
    );
  }

  private void Get()
  {
    this._app.MapGet(
      "/v1/data/{entityId}",
      async ([FromRoute] string entityId, [FromQuery] int page,
        [FromQuery] int pageSize, [FromQuery] string? filter, IDb db) =>
      {
        var data = await EntityDataHandler.Select(db, entityId, null, null,
          page, pageSize, filter);
        return TypedResults.Ok(data);
      }
    );

    this._app.MapGet(
      "/v1/data/{entityId}/{docId}",
      async ([FromRoute] string entityId, [FromRoute] string docId, IDb db) =>
      {
        var data = await EntityDataHandler.Select(db, entityId, null, docId);
        return TypedResults.Ok(data);
      }
    );

    this._app.MapGet(
      "/v1/data/name/{entityName}",
      async ([FromRoute] string entityName, [FromQuery] int page,
        [FromQuery] int pageSize, [FromQuery] string? filter, IDb db) =>
      {
        var data = await EntityDataHandler.Select(db, null, entityName, null,
          page, pageSize, filter);
        return TypedResults.Ok(data);
      }
    );

    this._app.MapGet(
      "/v1/data/name/{entityName}/{docId}",
      async ([FromRoute] string entityName, [FromRoute] string docId, IDb db) =>
      {
        var data = await EntityDataHandler.Select(db, null, entityName, docId);
        return TypedResults.Ok(data);
      }
    );
  }
}