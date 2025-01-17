using System.Dynamic;
using EntityDataHandler = Api.Handlers.EntityData;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using SharedLibs;

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
      async ([FromRoute] string entityId, [FromBody] dynamic body, IDb db) =>
      {
        var bodyObject = JsonConvert.DeserializeObject<ExpandoObject>(
          body.ToString(), new ExpandoObjectConverter());

        var insertedDoc = await EntityDataHandler.Create(db, entityId,
          bodyObject);

        return TypedResults.Ok(insertedDoc);
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
        [FromQuery] int pageSize, IDb db) =>
      {
        var data = await EntityDataHandler.Select(db, entityId, page, pageSize);
        return TypedResults.Ok(data);
      }
    );
  }
}