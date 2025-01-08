using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;

namespace SharedLibs.Types.Db;

public struct AggregateResult<T>
{
  [BsonElement("metadata")]
  public AggregateResultMetadata[] Metadata { get; set; }

  [BsonElement("data")]
  public T[] Data { get; set; }
}

public struct AggregateResultMetadata
{
  [BsonElement("totalCount")]
  public int TotalCount { get; set; }
}

public struct FindResult<T>
{
  [JsonPropertyName("metadata")]
  [JsonProperty("metadata")]
  public FindResultMetadata Metadata { get; set; }

  [JsonPropertyName("data")]
  [JsonProperty("data")]
  public T[] Data { get; set; }
}

public struct FindResultMetadata
{
  [JsonPropertyName("totalCount")]
  [JsonProperty("totalCount")]
  public int TotalCount { get; set; }

  [JsonPropertyName("page")]
  [JsonProperty("page")]
  public int Page { get; set; }

  [JsonPropertyName("pageSize")]
  [JsonProperty("pageSize")]
  public int PageSize { get; set; }

  [JsonPropertyName("totalPages")]
  [JsonProperty("totalPages")]
  public int TotalPages { get; set; }
}