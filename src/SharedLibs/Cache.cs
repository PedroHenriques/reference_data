using StackExchange.Redis;

namespace SharedLibs;

public interface ICache
{
  public Task<string?> Get(string key);
  public Task<bool> Set(string key, string value);
}

public interface IQueue
{
  public Task<long> Enqueue(string queueName, string[] messages);
}

public class Cache : ICache, IQueue
{
  private readonly IConnectionMultiplexer _client;

  public Cache(IConnectionMultiplexer client)
  {
    this._client = client;
  }

  public async Task<string?> Get(string key)
  {
    IDatabase db = this._client.GetDatabase(0);

    RedisValue result = await db.StringGetAsync(key);

    if (result.HasValue == false || result.IsNullOrEmpty)
    {
      return null;
    }

    return result.ToString();
  }

  public Task<bool> Set(string key, string value)
  {
    IDatabase db = this._client.GetDatabase(0);

    return db.StringSetAsync(key, value);
  }

  public Task<long> Enqueue(string queueName, string[] messages)
  {
    RedisValue[] values = messages.Select(message => (RedisValue)message)
      .ToArray();

    IDatabase db = this._client.GetDatabase(0);

    return db.ListLeftPushAsync(queueName, values, CommandFlags.None);
  }
}