using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace SharedLibs.Types;

public class NotifData
{
  [JsonPropertyName("eventTime")]
  [JsonProperty("eventTime")]
  public required DateTime EventTime { get; set; }

  [JsonPropertyName("changeTime")]
  [JsonProperty("changeTime")]
  public required DateTime ChangeTime { get; set; }

  [JsonPropertyName("id")]
  [JsonProperty("id")]
  public required string Id { get; set; }

  [JsonPropertyName("changeType")]
  [JsonProperty("changeType")]
  public required string ChangeType { get; set; }

  [JsonPropertyName("entity")]
  [JsonProperty("entity")]
  public required string Entity { get; set; }

  [JsonPropertyName("document")]
  [JsonProperty("document")]
  public Dictionary<string, dynamic?>? Document { get; set; }
}