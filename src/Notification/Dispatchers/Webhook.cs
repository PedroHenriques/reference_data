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

    Task.Run(async () =>
    {
      var result = await this._client.PostAsync(destination, content);
      callback(result.IsSuccessStatusCode);
    });

    return Task.CompletedTask;
  }
}