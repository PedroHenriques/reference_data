using EntityModel = Api.Models.Entity;
using MongoDB.Driver;
using Api.Routers;
using SharedLibs;
using SharedLibs.Types;
using DbConfigs = Api.Configs.Db;
using System.Diagnostics.CodeAnalysis;

[ExcludeFromCodeCoverage(Justification = "Not unit testable due to instantiating classes for service setup.")]
internal class Program
{
  private static void Main(string[] args)
  {
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.Services.AddSingleton<IMongoClient>(sp =>
    {
      MongoClient? mongoClient = new MongoClient(DbConfigs.MongoConStr);
      if (mongoClient == null)
      {
        throw new Exception("Mongo Client returned NULL.");
      }
      return mongoClient;
    });
    builder.Services.AddSingleton<IDb, Db>();
    builder.Services.AddScoped<EntityModel>();

    WebApplication app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
      app.UseSwagger();
      app.UseSwaggerUI();
    }

    Entities entitiesRouter = new Entities(app);
    EntityData entityDataRouter = new EntityData(app);

    app.Run();
  }
}