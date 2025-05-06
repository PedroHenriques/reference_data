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