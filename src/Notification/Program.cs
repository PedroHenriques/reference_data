using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Notification.Dispatchers;
using Notification.Services;
using Notification.Types;
using StackExchange.Redis;
using CacheConfigs = Notification.Configs.Cache;
using KafkaConfigs = Notification.Configs.Kafka;
using GeneralConfigs = Notification.Configs.General;
using System.Diagnostics.CodeAnalysis;
using Toolkit.Types;
using Toolkit;
using RedisUtils = Toolkit.Utils.Redis;
using KafkaUtils = Toolkit.Utils.Kafka<string, SharedLibs.Types.NotifData>;
using SharedLibs.Types;

[ExcludeFromCodeCoverage(Justification = "Not unit testable due to instantiating classes for service setup.")]
internal class Program
{
  private static async Task Main(string[] args)
  {
    ConfigurationOptions redisConOpts = new ConfigurationOptions
    {
      EndPoints = { CacheConfigs.RedisConStr },
    };

    ConfigurationOptions redisQueueConOpts = new ConfigurationOptions
    {
      EndPoints = { CacheConfigs.RedisConStrQueue },
    };

    var schemaRegistryConfig = new SchemaRegistryConfig { Url = KafkaConfigs.SchemaRegistryUrl };
    var kafkaProducerConfig = new ProducerConfig
    {
      BootstrapServers = KafkaConfigs.BootstrapServers,
      Acks = Acks.All
    };

    var cacheInputs = RedisUtils.PrepareInputs(redisConOpts);
    var queueInputs = RedisUtils.PrepareInputs(redisQueueConOpts);
    ICache cache = new Redis(cacheInputs);
    IQueue queue = new Redis(queueInputs);

    var kafkaInputs = KafkaUtils.PrepareInputs(
      schemaRegistryConfig, KafkaConfigs.SchemaSubject,
      int.Parse(KafkaConfigs.SchemaVersion), kafkaProducerConfig
    );
    IKafka<string, NotifData> kafka = new Kafka<string, NotifData>(kafkaInputs);

    IDispatchers dispatchers = new Dispatchers(new HttpClient(), kafka);

    HttpClient httpClient = new HttpClient();
    httpClient.BaseAddress = new Uri(GeneralConfigs.ApiBaseUrl);

    await Notify.Watch(queue, cache, dispatchers, httpClient);
  }
}