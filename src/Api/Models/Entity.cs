using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Api.Models;

public class Entity
{
  [BsonId]
  [BsonRepresentation(BsonType.ObjectId)]
  [BsonIgnoreIfDefault]
  public string? Id { get; set; }

  [BsonElement("name")]
  public required string Name { get; set; }

  [JsonPropertyName("description")]
  [BsonElement("description")]
  public string? Desc { get; set; }
}