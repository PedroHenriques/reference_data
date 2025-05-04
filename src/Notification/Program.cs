using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Notification.Dispatchers;
using Notification.Services;
using Notification.Types;
using StackExchange.Redis;
using CacheConfigs = Notification.Configs.Cache;
using KafkaConfigs = Notification.Configs.Kafka;
using FFConfigs = Notification.Configs.FeatureFlags;
using GeneralConfigs = Notification.Configs.General;
using SharedGeneralConfigs = SharedLibs.Configs.General;
using System.Diagnostics.CodeAnalysis;
using Toolkit.Types;
using Toolkit;
using FFUtils = Toolkit.Utils.FeatureFlags;
using SharedFFConfigs = SharedLibs.Configs.FeatureFlags;
using RedisUtils = Toolkit.Utils.Redis;
using KafkaUtils = Toolkit.Utils.Kafka<string, SharedLibs.Types.NotifData>;
using SharedLibs.Types;

[ExcludeFromCodeCoverage(Justification = "Not unit testable due to instantiating classes for service setup.")]
internal class Program
{
  private static Task Main(string[] args)
  {
    ConfigurationOptions redisConOpts = new ConfigurationOptions
    {
      EndPoints = { $"{CacheConfigs.RedisConHost}:{CacheConfigs.RedisConPort}" },
    };

    ConfigurationOptions redisQueueConOpts = new ConfigurationOptions
    {
      EndPoints = { $"{CacheConfigs.RedisConHostQueue}:{CacheConfigs.RedisConPortQueue}" },
    };

    var cacheInputs = RedisUtils.PrepareInputs(redisConOpts);
    var queueInputs = RedisUtils.PrepareInputs(redisQueueConOpts);
    ICache cache = new Redis(cacheInputs);
    IQueue queue = new Redis(queueInputs);

    var schemaRegistryConfig = new SchemaRegistryConfig
    {
      Url = KafkaConfigs.SchemaRegistryUrl,
      BasicAuthCredentialsSource = AuthCredentialsSource.UserInfo,
      BasicAuthUserInfo = $"{KafkaConfigs.SchemaRegistrySaslUsername}:{KafkaConfigs.SchemaRegistrySaslPw}",
    };
    var kafkaProducerConfig = new ProducerConfig
    {
      BootstrapServers = KafkaConfigs.BootstrapServers,
      Acks = Acks.All,
      SecurityProtocol = SecurityProtocol.SaslSsl,
      SaslMechanism = SaslMechanism.ScramSha256,
      SaslUsername = KafkaConfigs.BrokerSaslUsername,
      SaslPassword = KafkaConfigs.BrokerSaslPw,
    };

    if (SharedGeneralConfigs.DeploymentEnv == "local")
    {
      schemaRegistryConfig.BasicAuthCredentialsSource = null;
      kafkaProducerConfig.SecurityProtocol = null;
      kafkaProducerConfig.SaslMechanism = null;
    }

    var kafkaInputs = KafkaUtils.PrepareInputs(
      schemaRegistryConfig, KafkaConfigs.SchemaSubject,
      int.Parse(KafkaConfigs.SchemaVersion), kafkaProducerConfig
    );
    IKafka<string, NotifData> kafka = new Kafka<string, NotifData>(kafkaInputs);

    IDispatchers dispatchers = new Dispatchers(new HttpClient(), kafka);

    EnvNames ffEnvName;
    if (SharedFFConfigs.EnvName.TryGetValue(SharedGeneralConfigs.DeploymentEnv, out ffEnvName) == false)
    {
      throw new Exception("The value provided in the 'DEPLOYMENT_ENV' environment variable does not map to any valid FeatureFlag environment name.");
    }

    var inputs = FFUtils.PrepareInputs(
      SharedFFConfigs.EnvSdkKey, SharedFFConfigs.ContextApiKey, SharedFFConfigs.ContextName,
      ffEnvName
    );
    IFeatureFlags featureFlags = new FeatureFlags(inputs);

    featureFlags.GetBoolFlagValue(FFConfigs.DispatcherKeyActive);
    featureFlags.SubscribeToValueChanges(FFConfigs.DispatcherKeyActive);

    HttpClient httpClient = new HttpClient();
    httpClient.BaseAddress = new Uri(GeneralConfigs.ApiBaseUrl);

    for (int i = 0; i < GeneralConfigs.NumberProcesses; i++)
    {
      _ = Notify.Watch(queue, cache, dispatchers, httpClient);
    }

    Thread.Sleep(Timeout.Infinite);
    return Task.CompletedTask;
  }
}