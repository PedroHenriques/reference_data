using SharedLibs.Types;
using StackExchange.Redis;

namespace SharedLibs;

public interface ICache
{
  public Task<RedisValue> Get(RedisTypes type, string key);
  public Task<long> Enqueue(string queueName, RedisValue[] messages);
}

public class Cache : ICache
{
  private readonly IConnectionMultiplexer _client;

  public Cache(IConnectionMultiplexer client)
  {
    this._client = client;
  }

  public Task<RedisValue> Get(RedisTypes type, string key)
  {
    IDatabase db = this._client.GetDatabase(0);

    return db.StringGetAsync(key);
  }

  public Task<long> Enqueue(string queueName, RedisValue[] messages)
  {
    IDatabase db = this._client.GetDatabase(0);

    return db.ListLeftPushAsync(queueName, messages, CommandFlags.None);
  }
}