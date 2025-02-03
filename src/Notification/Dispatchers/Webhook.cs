using System.Net.Http.Headers;
using Newtonsoft.Json;
using Notification.Types;
using SharedLibs.Types.Notification;

namespace Notification.Dispatchers;

public class Webhook : IDispatcher
{
  private readonly HttpClient _client;

  public Webhook(HttpClient client)
  {
    this._client = client;
  }

  public async Task<bool> Dispatch(NotifData data, string destination)
  {
    HttpContent content = new StringContent(JsonConvert.SerializeObject(data));
    content.Headers.ContentType = new MediaTypeHeaderValue("application/json", "utf-8");

    var res = await this._client.PostAsync(destination, content);

    return res.IsSuccessStatusCode;
  }
}