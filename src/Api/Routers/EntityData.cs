using System.Dynamic;
using EntityDataHandler = Api.Handlers.EntityData;
using Api.Services;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Api.Routers;

public class EntityData
{
  private readonly WebApplication _app;

  public EntityData(WebApplication app)
  {
    this._app = app;

    Post();
  }

  private void Post()
  {
    this._app.MapPost(
      "/data/{entityId}/",
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
}