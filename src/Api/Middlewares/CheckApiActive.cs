using System.Net;
using Api.Services;
using FFApiConfigs = Api.Configs.FeatureFlags;

namespace Api.Middleware;

public class CheckApiActiveMiddleware
{
  private readonly RequestDelegate _next;

  public CheckApiActiveMiddleware(RequestDelegate next)
  {
    this._next = next;
  }

  public async Task InvokeAsync(HttpContext context)
  {
    if (FeatureFlags.FlagValues[FFApiConfigs.ApiKeyActive])
    {
      await this._next.Invoke(context);
    }
    else
    {
      context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
    }
  }
}