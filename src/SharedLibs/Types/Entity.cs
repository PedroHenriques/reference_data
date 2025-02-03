using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;

namespace SharedLibs.Types.Entity;

public struct NotifConfig
{
  [JsonPropertyName("protocol")]
  [JsonProperty("protocol")]
  [BsonElement("protocol")]
  public required string Protocol { get; set; }

  [JsonPropertyName("targetUrl")]
  [JsonProperty("targetUrl")]
  [BsonElement("targetUrl")]
  public required string TargetURL { get; set; }
}