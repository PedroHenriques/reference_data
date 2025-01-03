using EntityModel = Api.Models.Entity;
using Api.Services;
using MongoDB.Driver;
using Api.Routers;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IMongoClient>(sp =>
{
  string? mongoConStr = Environment.GetEnvironmentVariable("MONGO_CON_STR");
  if (mongoConStr == null)
  {
    throw new Exception("Could not get the 'MONGO_CON_STR' environment variable");
  }

  MongoClient? mongoClient = new MongoClient(mongoConStr);
  if (mongoClient == null)
  {
    throw new Exception("Mongo Client returned NULL.");
  }
  return mongoClient;
});
builder.Services.AddSingleton<IDb, Db>();
builder.Services.AddScoped<EntityModel>();

WebApplication app = builder.Build();

Entities entitiesRouter = new Entities(app);
EntityData entityDataRouter = new EntityData(app);

app.Run();
