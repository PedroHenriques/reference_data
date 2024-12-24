using EntityModel = Api.Models.Entity;
using EntityHandler = Api.Handlers.Entity;
using Microsoft.AspNetCore.Mvc;
using Api.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IDb, Db>();
builder.Services.AddScoped<EntityModel>();

WebApplication app = builder.Build();

app.MapPost(
  "/entities/",
  ([FromBody] EntityModel entity, IDb db) =>
  {
    EntityHandler.Create(db , entity);
    return TypedResults.Ok<EntityModel>(entity);
  }
);

app.Run("http://localhost:10000");
