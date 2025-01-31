namespace SharedLibs.Types.Cache;

public interface ICache
{
  public Task<string?> Get(string key);

  public Task<bool> Set(string key, string value);

  public Task Remove(string key);
}

public interface IQueue
{
  public Task<long> Enqueue(string queueName, string[] messages);

  public Task<string> Dequeue(string queueName);

  public Task<bool> Ack(string queueName, string message);

  public Task<bool> Nack(string queueName, string message);
}

public struct ChangeQueueItem
{
  public required DateTime ChangeTime { get; set; }

  public required string ChangeRecord { get; set; }

  public required string Source { get; set; }
}