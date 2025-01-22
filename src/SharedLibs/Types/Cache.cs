namespace SharedLibs.Types.Cache;

public interface ICache
{
  public Task<string?> Get(string key);
  public Task<bool> Set(string key, string value);
}

public interface IQueue
{
  public Task<long> Enqueue(string queueName, string[] messages);
  public Task<string> Dequeue(string queueName);
}

public struct ChangeQueueItem
{
  public string ChangeRecord { get; set; }

  public string Source { get; set; }
}