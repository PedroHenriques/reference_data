using EntityModel = Api.Models.Entity;
using Api.Routers;
using DbConfigs = Api.Configs.Db;
using System.Diagnostics.CodeAnalysis;
using Toolkit;
using Toolkit.Types;
using MongodbUtils = Toolkit.Utils.Mongodb;

[ExcludeFromCodeCoverage(Justification = "Not unit testable due to instantiating classes for service setup.")]
internal class Program
{
  private static void Main(string[] args)
  {
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.Services.AddSingleton<IMongodb>(sp =>
    {
      var inputs = MongodbUtils.PrepareInputs(DbConfigs.MongoConStr);
      return new Mongodb(inputs);
    });
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