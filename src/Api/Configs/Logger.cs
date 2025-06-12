using System.Diagnostics.CodeAnalysis;

namespace Api.Configs;

[ExcludeFromCodeCoverage(Justification = "Not unit testable due to being a config static class.")]
public static class Logger
{
  public static string TraceIdReqHeader = Environment.GetEnvironmentVariable("TRACE_ID_REQ_HEADER")
    ?? throw new Exception("Could not get the 'TRACE_ID_REQ_HEADER' environment variable");
}