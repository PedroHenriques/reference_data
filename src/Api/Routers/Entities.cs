using EntityModel = Api.Models.Entity;
using Api.Services;
using EntityHandler = Api.Handlers.Entity;

namespace Api.Routers;

public class Entities
{
  private readonly WebApplication _app;
  public Entities(WebApplication app)
  {
    this._app = app;

    Post();
  }

  private void Post()
  {
    this._app.MapPost(
      "/entities/",
      (EntityModel entity, IDb db) =>
      {
        EntityHandler.Create(db, entity);
        return TypedResults.Ok<EntityModel>(entity);
      }
    );
  }
}