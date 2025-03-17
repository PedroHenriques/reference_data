using System.Net.Http.Headers;
using Newtonsoft.Json;
using Notification.Types;
using SharedLibs.Types;

namespace Notification.Dispatchers;

public class Webhook : IDispatcher
{
  private readonly HttpClient _client;

  public Webhook(HttpClient client)
  {
    this._client = client;
  }

  public Task Dispatch(NotifData data, string destination, Action<bool> callback)
  {
    HttpContent content = new StringContent(JsonConvert.SerializeObject(data));
    content.Headers.ContentType = new MediaTypeHeaderValue("application/json", "utf-8");

    this._client.PostAsync(destination, content)
      .ContinueWith((result) =>
      {
        callback(result.Result.IsSuccessStatusCode);
      });

    return Task.CompletedTask;
  }
}