namespace SharedLibs.Types;

public struct ChangeQueueItem
{
  public required DateTime ChangeTime { get; set; }

  public required string ChangeRecord { get; set; }

  public required string Source { get; set; }
}