using EntityModel = Api.Models.Entity;
using Api.Services;
using MongoDB.Driver;
using Api.Routers;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IMongoClient>(sp => {
  MongoClient? mongoClient = new MongoClient("mongodb://admin:pw@localhost:27017/admin?authMechanism=SCRAM-SHA-256");
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

app.Run("http://localhost:10000");
