namespace Notification.Configs;

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
}