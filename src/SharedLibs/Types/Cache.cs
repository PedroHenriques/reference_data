namespace SharedLibs.Types;

public enum CacheTypes
{
  String
}

public struct ChangeQueueItem
{
  public string ChangeRecord { get; set; }

  public string Source { get; set; }
}