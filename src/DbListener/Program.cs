using DbConfigs = DbListener.Configs.Db;
using CacheConfigs = DbListener.Configs.Cache;
using DbListener.Services;
using StackExchange.Redis;
using System.Diagnostics.CodeAnalysis;
using Toolkit.Types;
using Toolkit;
using FFUtils = Toolkit.Utils.FeatureFlags;
using MongodbUtils = Toolkit.Utils.Mongodb;
using RedisUtils = Toolkit.Utils.Redis;
using FFConfigs = SharedLibs.Configs.FeatureFlags;
using GeneralConfigs = SharedLibs.Configs.General;

[ExcludeFromCodeCoverage(Justification = "Not unit testable due to instantiating classes for service setup.")]
internal class Program
{
  private static async Task Main(string[] args)
  {
    var mongodbInputs = MongodbUtils.PrepareInputs(DbConfigs.MongoConStr);
    IMongodb db = new Mongodb(mongodbInputs);

    ConfigurationOptions redisConOpts = new ConfigurationOptions
    {
      EndPoints = { $"{CacheConfigs.RedisConHost}:{CacheConfigs.RedisConPort}" },
      Password = CacheConfigs.RedisPw,
      Ssl = false,
    };
    if (GeneralConfigs.DeploymentEnv == "local")
    {
      redisConOpts.Password = null;
    }
    var redisInputs = RedisUtils.PrepareInputs(redisConOpts);
    ICache cache = new Redis(redisInputs);
    IQueue queue = (IQueue)cache;

    EnvNames ffEnvName;
    if (FFConfigs.EnvName.TryGetValue(GeneralConfigs.DeploymentEnv, out ffEnvName) == false)
    {
      throw new Exception("The value provided in the 'DEPLOYMENT_ENV' environment variable does not map to any valid FeatureFlag environment name.");
    }

    var inputs = FFUtils.PrepareInputs(
      FFConfigs.EnvSdkKey, FFConfigs.ContextApiKey, FFConfigs.ContextName,
      ffEnvName
    );
    IFeatureFlags ff = new FeatureFlags(inputs);

    await DbStream.Watch(cache, queue, db, ff);

    Thread.Sleep(Timeout.Infinite);
  }
}