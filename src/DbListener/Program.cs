using DbConfigs = DbListener.Configs.Db;
using CacheConfigs = DbListener.Configs.Cache;
using DbListener.Services;
using MongoDB.Driver;
using SharedLibs;
using SharedLibs.Types;
using StackExchange.Redis;

IMongoClient? mongoClient = new MongoClient(DbConfigs.MongoConStr);
if (mongoClient == null)
{
  throw new Exception("Mongo Client returned NULL.");
}

ConfigurationOptions redisConOpts = new ConfigurationOptions
{
  EndPoints = { CacheConfigs.RedisConStr },
};
IConnectionMultiplexer? redisClient = ConnectionMultiplexer.Connect(redisConOpts);
if (redisClient == null)
{
  throw new Exception("Redis Client returned NULL.");
}

IDb db = new Db(mongoClient);
ICache cache = new Cache(redisClient);
IQueue queue = (IQueue)cache;

await DbStream.Watch(cache, queue, db);