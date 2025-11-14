using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Toolkit.Types;

namespace DbListener.Configs;

[ExcludeFromCodeCoverage(Justification = "Not unit testable due to being a config static class.")]
public static class Log
{
  public static Dictionary<WatchKind, LogLevel> WatchKindLogLevels = new Dictionary<WatchKind, LogLevel>
  {
    { WatchKind.Data, LogLevel.Information },
    { WatchKind.Error, LogLevel.Error },
    { WatchKind.Heartbeat, LogLevel.Debug },
    { WatchKind.Resumed, LogLevel.Information },
    { WatchKind.Started, LogLevel.Information },
    { WatchKind.Stopped, LogLevel.Information },
  };
}