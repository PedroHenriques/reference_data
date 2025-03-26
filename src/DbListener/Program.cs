using DbConfigs = DbListener.Configs.Db;
using CacheConfigs = DbListener.Configs.Cache;
using DbListener.Services;
using StackExchange.Redis;
using System.Diagnostics.CodeAnalysis;
using Toolkit.Types;
using Toolkit;
using MongodbUtils = Toolkit.Utils.Mongodb;
using RedisUtils = Toolkit.Utils.Redis;

[ExcludeFromCodeCoverage(Justification = "Not unit testable due to instantiating classes for service setup.")]
internal class Program
{
  private static async Task Main(string[] args)
  {
    var mongodbInputs = MongodbUtils.PrepareInputs(DbConfigs.MongoConStr);
    IMongodb db = new Mongodb(mongodbInputs);

    ConfigurationOptions redisConOpts = new ConfigurationOptions
    {
      EndPoints = { CacheConfigs.RedisConStr },
    };
    var redisInputs = RedisUtils.PrepareInputs(redisConOpts);
    ICache cache = new Redis(redisInputs);
    IQueue queue = (IQueue)cache;

    await DbStream.Watch(cache, queue, db);
  }
}