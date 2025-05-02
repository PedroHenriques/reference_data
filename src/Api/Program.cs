using EntityModel = Api.Models.Entity;
using Api.Routers;
using DbConfigs = Api.Configs.Db;
using System.Diagnostics.CodeAnalysis;
using Toolkit;
using Toolkit.Types;
using MongodbUtils = Toolkit.Utils.Mongodb;
using FFUtils = Toolkit.Utils.FeatureFlags;
using FFConfigs = SharedLibs.Configs.FeatureFlags;
using FFApiConfigs = Api.Configs.FeatureFlags;
using GeneralConfigs = SharedLibs.Configs.General;
using Api.Middleware;

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
      IMongodb mongo = new Mongodb(inputs);
      Api.Configs.MongoIndexes.Create(mongo);
      return mongo;
    });
    builder.Services.AddSingleton<IFeatureFlags>(sp =>
    {
      EnvNames ffEnvName;
      if (FFConfigs.EnvName.TryGetValue(GeneralConfigs.DeploymentEnv, out ffEnvName) == false)
      {
        throw new Exception("The value provided in the 'DEPLOYMENT_ENV' environment variable does not map to any valid FeatureFlag environment name.");
      }

      var inputs = FFUtils.PrepareInputs(
        FFConfigs.EnvSdkKey, FFConfigs.ContextApiKey, FFConfigs.ContextName,
        ffEnvName
      );
      return new FeatureFlags(inputs);
    });
    builder.Services.AddScoped<EntityModel>();

    WebApplication app = builder.Build();

    app.UseMiddleware<CheckApiActiveMiddleware>();

    if (app.Environment.IsDevelopment())
    {
      app.UseSwagger();
      app.UseSwaggerUI();
    }

    IFeatureFlags featureFlags = app.Services.GetService<IFeatureFlags>() ??
      throw new Exception("Failed to get the IFeatureFlags instance from DI.");
    featureFlags.GetBoolFlagValue(FFApiConfigs.ApiKeyActive);
    featureFlags.SubscribeToValueChanges(FFApiConfigs.ApiKeyActive);

    Entities entitiesRouter = new Entities(app);
    EntityData entityDataRouter = new EntityData(app);

    app.Run();
  }
}