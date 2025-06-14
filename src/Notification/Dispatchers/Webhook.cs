using System.Net.Http.Headers;
using Newtonsoft.Json;
using Notification.Types;
using SharedLibs.Types;
using Toolkit.Types;

namespace Notification.Dispatchers;

public class Webhook : IDispatcher
{
  private readonly HttpClient _client;
  private readonly ILogger _logger;

  public Webhook(HttpClient client, ILogger logger)
  {
    this._client = client;
    this._logger = logger;
  }

  public Task Dispatch(NotifData data, string destination, Action<bool> callback)
  {
    return Task.Run(async () =>
    {
      try
      {
        HttpContent content = new StringContent(JsonConvert.SerializeObject(data));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json", "utf-8");

        var result = await this._client.PostAsync(destination, content);
        callback(result.IsSuccessStatusCode);

        this._logger.Log(
          result.IsSuccessStatusCode ? Microsoft.Extensions.Logging.LogLevel.Information : Microsoft.Extensions.Logging.LogLevel.Error,
          null,
          $"Webhook Dispatcher - HTTP(S) request: Document id = {data.Id} | Status Code = {result.StatusCode} | Reason = {result.ReasonPhrase}"
        );
      }
      catch (Exception ex)
      {
        callback(false);

        this._logger.Log(
          Microsoft.Extensions.Logging.LogLevel.Error,
          ex,
          ex.Message
        );
      }
    });
  }
}