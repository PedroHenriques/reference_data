using SharedLibs;
using SharedLibs.Types.Cache;
using StackExchange.Redis;

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

redisConStr = Environment.GetEnvironmentVariable("REDIS_CON_STR_QUEUE");
if (redisConStr == null)
{
  throw new Exception("Could not get the 'REDIS_CON_STR_QUEUE' environment variable");
}
redisConOpts = new ConfigurationOptions
{
  EndPoints = { redisConStr },
};
IConnectionMultiplexer? redisClientQueue = ConnectionMultiplexer.Connect(redisConOpts);
if (redisClient == null)
{
  throw new Exception("Redis Client, for the queue, returned NULL.");
}

ICache cache = new Cache(redisClient);
IQueue queue = new Cache(redisClientQueue);