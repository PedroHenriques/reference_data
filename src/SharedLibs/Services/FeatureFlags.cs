using Toolkit.Types;

namespace SharedLibs.Services;

public class FeatureFlags
{
  public static Dictionary<string, bool> FlagValues;
  private readonly IFeatureFlags _ff;

  public FeatureFlags(IFeatureFlags ff, string[] flagKeys)
  {
    this._ff = ff;

    FlagValues = new Dictionary<string, bool>();

    WatchKeys(flagKeys);
  }

  private void WatchKeys(string[] flagKeys)
  {
    foreach (var key in flagKeys)
    {
      FlagValues[key] = this._ff.GetBoolFlagValue(key);
      this._ff.SubscribeToValueChanges(
        key,
        ev => { FlagValues[ev.Key] = ev.NewValue.AsBool; }
      );
    }
  }
}