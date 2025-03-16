using Notification.Dispatchers;
using Notification.Services;
using Notification.Types;
using SharedLibs;
using SharedLibs.Types;
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
IDispatchers dispatchers = new Dispatchers(new HttpClient());

string? apiBaseUrlStr = Environment.GetEnvironmentVariable("API_BASE_URL");
if (apiBaseUrlStr == null)
{
  throw new Exception("Could not get the 'API_BASE_URL' environment variable");
}
HttpClient httpClient = new HttpClient();
httpClient.BaseAddress = new Uri(apiBaseUrlStr);

await Notify.Watch(queue, cache, dispatchers, httpClient);