using MongoDB.Bson;
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
    var res = await this._client.PostAsync(destination,
      new StringContent(data.ToJson()));

    return res.IsSuccessStatusCode;
  }
}