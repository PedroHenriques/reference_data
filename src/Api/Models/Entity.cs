using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using SharedLibs.Types;

namespace Api.Models;

public class Entity
{
  [JsonPropertyName("id")]
  [JsonProperty("id")]
  [BsonId]
  [BsonRepresentation(BsonType.ObjectId)]
  [BsonIgnoreIfDefault]
  public string? Id { get; set; }

  [JsonPropertyName("name")]
  [JsonProperty("name")]
  [BsonElement("name")]
  public required string Name { get; set; }

  [JsonPropertyName("description")]
  [JsonProperty("description")]
  [BsonElement("description")]
  public string? Desc { get; set; }

  [JsonPropertyName("deletedAt")]
  [JsonProperty("deletedAt")]
  [BsonElement("deletedAt")]
  public DateTime? DeletedAt { get; set; }

  [JsonPropertyName("notifConfigs")]
  [JsonProperty("notifConfigs")]
  [BsonElement("notifConfigs")]
  public NotifConfig[]? NotifConfigs { get; set; }

  public static async ValueTask<Entity?> BindAsync(HttpContext context)
  {
    Entity? entity;

    string bodyAsText = await new StreamReader(context.Request.Body)
      .ReadToEndAsync();

    entity = JsonConvert.DeserializeObject<Entity>(bodyAsText);
    if (entity == null)
    {
      throw new Exception("Deserializing Entity produced NULL.");
    }

    if (entity.Name == null)
    {
      throw new Exception("No name provided.");
    }

    if (context.Request.Method == System.Net.WebRequestMethods.Http.Put)
    {
      Object? id = context.Request.RouteValues["id"];
      if (id == null)
      {
        throw new KeyNotFoundException("No ID provided in the route.");
      }

      entity.Id = id.ToString();
    }

    return entity;
  }
}