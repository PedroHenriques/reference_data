using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Notification.Dispatchers;
using Notification.Services;
using Notification.Types;
using SharedLibs;
using SharedLibs.Types;
using EventBusUtil = SharedLibs.Utils.EventBus<string, SharedLibs.Types.NotifData>;
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

var kafkaSchemaRegistryUrl = Environment.GetEnvironmentVariable("KAFKA_SCHEMA_REGISTRY_URL");
if (kafkaSchemaRegistryUrl == null)
{
  throw new Exception("Could not get the 'KAFKA_SCHEMA_REGISTRY_URL' environment variable");
}
var kafkaSchemaSubject = Environment.GetEnvironmentVariable("KAFKA_SCHEMA_SUBJECT");
if (kafkaSchemaSubject == null)
{
  throw new Exception("Could not get the 'KAFKA_SCHEMA_SUBJECT' environment variable");
}
var kafkaSchemaVersion = Environment.GetEnvironmentVariable("KAFKA_SCHEMA_VERSION");
if (kafkaSchemaVersion == null)
{
  throw new Exception("Could not get the 'KAFKA_SCHEMA_VERSION' environment variable");
}
var schemaRegistryConfig = new SchemaRegistryConfig { Url = kafkaSchemaRegistryUrl };
ISchemaRegistryClient schemaRegistry = new CachedSchemaRegistryClient(schemaRegistryConfig);

var kafkaBootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS");
if (kafkaBootstrapServers == null)
{
  throw new Exception("Could not get the 'KAFKA_BOOTSTRAP_SERVERS' environment variable");
}
var kafkaProducer = new ProducerBuilder<string, NotifData>(
  new ProducerConfig { BootstrapServers = kafkaBootstrapServers, Acks = Acks.All }
);
var eventBusInputs = EventBusUtil.PrepareInputs(
  schemaRegistry, kafkaSchemaSubject, Int32.Parse(kafkaSchemaVersion),
  new JsonSerializer<NotifData>(schemaRegistry), kafkaProducer
);

ICache cache = new Cache(redisClient);
IQueue queue = new Cache(redisClientQueue);
var eventBus = new EventBus<string, NotifData>(eventBusInputs);
IDispatchers dispatchers = new Dispatchers(new HttpClient(), eventBus);

string? apiBaseUrlStr = Environment.GetEnvironmentVariable("API_BASE_URL");
if (apiBaseUrlStr == null)
{
  throw new Exception("Could not get the 'API_BASE_URL' environment variable");
}
HttpClient httpClient = new HttpClient();
httpClient.BaseAddress = new Uri(apiBaseUrlStr);

await Notify.Watch(queue, cache, dispatchers, httpClient);