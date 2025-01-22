using DbListener.Services;
using MongoDB.Driver;
using SharedLibs;
using SharedLibs.Types.Cache;
using SharedLibs.Types.Db;
using StackExchange.Redis;

string? mongoConStr = Environment.GetEnvironmentVariable("MONGO_CON_STR");
if (mongoConStr == null)
{
  throw new Exception("Could not get the 'MONGO_CON_STR' environment variable");
}

IMongoClient? mongoClient = new MongoClient(mongoConStr);
if (mongoClient == null)
{
  throw new Exception("Mongo Client returned NULL.");
}

string? redisConStr = Environment.GetEnvironmentVariable("REDIS_CON_STR");
if (redisConStr == null)
{
  throw new Exception("Could not get the 'REDIS_CON_STR' environment variable");
}
ConfigurationOptions redisConOpts = new ConfigurationOptions
{
  EndPoints = { redisConStr },
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