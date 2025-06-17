using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Notification.Dispatchers;
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
using LoggerUtils = Toolkit.Utils.Logger;
using SharedFFConfigs = SharedLibs.Configs.FeatureFlags;
using RedisUtils = Toolkit.Utils.Redis;
using KafkaUtils = Toolkit.Utils.Kafka<Notification.Types.NotifDataKafkaKey, Notification.Types.NotifDataKafkaValue>;
using Notification.Utils;

[ExcludeFromCodeCoverage(Justification = "Not unit testable due to instantiating classes for service setup.")]
internal class Program
{
  private static async Task Main(string[] args)
  {
    var loggerInputs = LoggerUtils.PrepareInputs("Notification", "Program.cs", "Main thread");
    ILogger logger = new Logger(loggerInputs);

    ConfigurationOptions redisConOpts = new ConfigurationOptions
    {
      EndPoints = { $"{CacheConfigs.RedisConHost}:{CacheConfigs.RedisConPort}" },
      Password = CacheConfigs.RedisPw,
      Ssl = false,
      AbortOnConnectFail = false,
    };

    ConfigurationOptions redisQueueChangesConOpts = new ConfigurationOptions
    {
      EndPoints = { $"{CacheConfigs.RedisConHostQueue}:{CacheConfigs.RedisConPortQueue}" },
      Password = CacheConfigs.RedisPwQueue,
      Ssl = false,
      AbortOnConnectFail = false,
    };

    var notificationRedisInputs = RedisUtils.PrepareInputs(redisConOpts, "notification-service");
    var dblistenerRedisInputs = RedisUtils.PrepareInputs(redisQueueChangesConOpts, "notification-service");
    ICache cacheNotif = new Redis(notificationRedisInputs);
    IQueue queueNotif = new Redis(notificationRedisInputs);
    IQueue queueDblistener = new Redis(dblistenerRedisInputs);

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
      SaslMechanism = SaslMechanism.Plain,
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
    IKafka<NotifDataKafkaKey, NotifDataKafkaValue> kafka = new Kafka<NotifDataKafkaKey, NotifDataKafkaValue>(kafkaInputs);

    IDispatchers dispatchers = new Dispatchers(new HttpClient(), kafka, logger);

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

    featureFlags.GetBoolFlagValue(FFConfigs.NotificationKeyActive);
    featureFlags.SubscribeToValueChanges(FFConfigs.NotificationKeyActive);
    featureFlags.GetBoolFlagValue(FFConfigs.RetryQueueKeyActive);
    featureFlags.SubscribeToValueChanges(FFConfigs.RetryQueueKeyActive);

    HttpClient httpClient = new HttpClient();
    httpClient.BaseAddress = new Uri($"{GeneralConfigs.ApiBaseUrl}:{GeneralConfigs.ApiPort}");

    List<Task> tasks = new List<Task>();
    for (int i = 0; i < GeneralConfigs.NumberProcesses; i++)
    {
      string threadId = $"{System.Environment.MachineName}_{i}";
      logger.Log(
        Microsoft.Extensions.Logging.LogLevel.Information,
        null,
        $"Starting Mongo changes process with id: {threadId}"
      );

      tasks.Add(
        Task.Run(async () =>
        {
          while (true)
          {
            try
            {
              await Notify.ProcessMessage(
                queueDblistener, cacheNotif, queueNotif, dispatchers, httpClient,
                logger, NotifyMode.MongoChanges, threadId
              );
            }
            catch (Exception ex)
            {
              logger.Log(
                Microsoft.Extensions.Logging.LogLevel.Error,
                ex, ex.Message
              );
            }
          }
        })
      );
    }

    for (int i = 0; i < GeneralConfigs.NumberProcessesRetry; i++)
    {
      string threadId = $"{System.Environment.MachineName}_{i}";
      logger.Log(
        Microsoft.Extensions.Logging.LogLevel.Information,
        null,
        $"Starting retry queue process with id: {threadId}"
      );

      tasks.Add(
        Task.Run(async () =>
        {
          while (true)
          {
            try
            {
              await Notify.ProcessMessage(
                queueDblistener, cacheNotif, queueNotif, dispatchers, httpClient,
                logger, NotifyMode.DispatcherRetry, threadId
              );
            }
            catch (Exception ex)
            {
              logger.Log(
                Microsoft.Extensions.Logging.LogLevel.Error,
                ex, ex.Message
              );
            }
          }
        })
      );
    }

    await Task.WhenAll(tasks);
  }
}