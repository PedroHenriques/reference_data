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
using CacheConfigs = Notification.Configs.Cache;
using KafkaConfigs = Notification.Configs.Kafka;
using GeneralConfigs = Notification.Configs.General;

ConfigurationOptions redisConOpts = new ConfigurationOptions
{
  EndPoints = { CacheConfigs.RedisConStr },
};
IConnectionMultiplexer? redisClient = ConnectionMultiplexer.Connect(redisConOpts);
if (redisClient == null)
{
  throw new Exception("Redis Client returned NULL.");
}

redisConOpts = new ConfigurationOptions
{
  EndPoints = { CacheConfigs.RedisConStrQueue },
};
IConnectionMultiplexer? redisClientQueue = ConnectionMultiplexer.Connect(redisConOpts);
if (redisClient == null)
{
  throw new Exception("Redis Client, for the queue, returned NULL.");
}

var schemaRegistryConfig = new SchemaRegistryConfig { Url = KafkaConfigs.SchemaRegistryUrl };
ISchemaRegistryClient schemaRegistry = new CachedSchemaRegistryClient(schemaRegistryConfig);

var kafkaProducer = new ProducerBuilder<string, NotifData>(
  new ProducerConfig { BootstrapServers = KafkaConfigs.BootstrapServers, Acks = Acks.All }
);
var eventBusInputs = EventBusUtil.PrepareInputs(
  schemaRegistry, KafkaConfigs.SchemaSubject, Int32.Parse(KafkaConfigs.SchemaVersion),
  new JsonSerializer<NotifData>(schemaRegistry), kafkaProducer
);

ICache cache = new Cache(redisClient);
IQueue queue = new Cache(redisClientQueue);
var eventBus = new EventBus<string, NotifData>(eventBusInputs);
IDispatchers dispatchers = new Dispatchers(new HttpClient(), eventBus);

HttpClient httpClient = new HttpClient();
httpClient.BaseAddress = new Uri(GeneralConfigs.ApiBaseUrl);

await Notify.Watch(queue, cache, dispatchers, httpClient);