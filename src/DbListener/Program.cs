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
using LoggerUtils = Toolkit.Utils.Logger;
using FFConfigs = SharedLibs.Configs.FeatureFlags;
using GeneralConfigs = SharedLibs.Configs.General;

[ExcludeFromCodeCoverage(Justification = "Not unit testable due to instantiating classes for service setup.")]
internal class Program
{
  private static async Task Main(string[] args)
  {
    var loggerInputs = LoggerUtils.PrepareInputs("DbListener", "Program.cs", "Main thread");
    ILogger logger = new Logger(loggerInputs);

    var mongodbInputs = MongodbUtils.PrepareInputs(
      DbConfigs.MongoConStr, GeneralConfigs.DeletedAtPropName
    );
    IMongodb db = new Mongodb(mongodbInputs);

    ConfigurationOptions redisConOpts = new ConfigurationOptions
    {
      EndPoints = { $"{CacheConfigs.RedisConHost}:{CacheConfigs.RedisConPort}" },
      Password = CacheConfigs.RedisPw,
      Ssl = false,
      AbortOnConnectFail = false,
    };
    var redisInputs = RedisUtils.PrepareInputs(redisConOpts, "dblistener-service");
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

    await DbStream.Watch(cache, queue, db, ff, logger);

    Thread.Sleep(Timeout.Infinite);
  }
}