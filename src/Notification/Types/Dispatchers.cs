using System.Text.Json.Serialization;
using Newtonsoft.Json;
using SharedLibs.Types;

namespace Notification.Types;

public interface IDispatchers
{
  public IDispatcher? GetDispatcher(string protocol);
}

public interface IDispatcher
{
  public Task Dispatch(NotifData data, string destination, Action<bool> callback);
}

public class NotifDataKafkaKey
{
  [JsonPropertyName("id")]
  [JsonProperty("id")]
  public required string Id { get; set; }
}

public class NotifDataKafkaValue
{
  [JsonPropertyName("metadata")]
  [JsonProperty("metadata")]
  public required NotifDataKafkaValueMetadata Metadata { get; set; }

  [JsonPropertyName("data")]
  [JsonProperty("data")]
  public required NotifDataKafkaValueData Data { get; set; }
}

public class NotifDataKafkaValueMetadata
{
  [JsonPropertyName("action")]
  [JsonProperty("action")]
  public required string Action { get; set; }

  [JsonPropertyName("actionDatetime")]
  [JsonProperty("actionDatetime")]
  public required DateTime ActionDatetime { get; set; }

  [JsonPropertyName("correlationId")]
  [JsonProperty("correlationId")]
  public required string CorrelationId { get; set; }

  [JsonPropertyName("eventDatetime")]
  [JsonProperty("eventDatetime")]
  public required DateTime EventDatetime { get; set; }

  [JsonPropertyName("source")]
  [JsonProperty("source")]
  public required string Source { get; set; }
}

public class NotifDataKafkaValueData
{
  [JsonPropertyName("document")]
  [JsonProperty("document")]
  public required Dictionary<string, dynamic?>? Document { get; set; }

  [JsonPropertyName("entity")]
  [JsonProperty("entity")]
  public required string Entity { get; set; }

  [JsonPropertyName("id")]
  [JsonProperty("id")]
  public required string Id { get; set; }
}