using System.Diagnostics.CodeAnalysis;

namespace Notification.Configs;

[ExcludeFromCodeCoverage(Justification = "Not unit testable due to being a config static class.")]
public static class Kafka
{
  public static string SchemaRegistryUrl = Environment.GetEnvironmentVariable("KAFKA_SCHEMA_REGISTRY_URL")
    ?? throw new Exception("Could not get the 'KAFKA_SCHEMA_REGISTRY_URL' environment variable");

  public static string SchemaSubject = Environment.GetEnvironmentVariable("KAFKA_SCHEMA_SUBJECT")
    ?? throw new Exception("Could not get the 'KAFKA_SCHEMA_SUBJECT' environment variable");

  public static string SchemaVersion = Environment.GetEnvironmentVariable("KAFKA_SCHEMA_VERSION")
    ?? throw new Exception("Could not get the 'KAFKA_SCHEMA_VERSION' environment variable");

  public static string BootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS")
    ?? throw new Exception("Could not get the 'KAFKA_BOOTSTRAP_SERVERS' environment variable");

  public static string BrokerSaslUsername = Environment.GetEnvironmentVariable("KAFKA_BROKER_SASL_USERNAME")
    ?? throw new Exception("Could not get the 'KAFKA_BROKER_SASL_USERNAME' environment variable");

  public static string BrokerSaslPw = Environment.GetEnvironmentVariable("KAFKA_BROKER_SASL_PW")
    ?? throw new Exception("Could not get the 'KAFKA_BROKER_SASL_PW' environment variable");

  public static string SchemaRegistrySaslUsername = Environment.GetEnvironmentVariable("KAFKA_SCHEMA_REGISTRY_SASL_USERNAME")
    ?? throw new Exception("Could not get the 'KAFKA_SCHEMA_REGISTRY_SASL_USERNAME' environment variable");

  public static string SchemaRegistrySaslPw = Environment.GetEnvironmentVariable("KAFKA_SCHEMA_REGISTRY_SASL_PW")
    ?? throw new Exception("Could not get the 'KAFKA_SCHEMA_REGISTRY_SASL_PW' environment variable");
}