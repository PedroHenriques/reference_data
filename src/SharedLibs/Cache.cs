using SharedLibs.Types.Cache;
using StackExchange.Redis;

namespace SharedLibs;

public class Cache : ICache, IQueue
{
  private readonly IConnectionMultiplexer _client;
  private readonly IDatabase _db;

  public Cache(IConnectionMultiplexer client)
  {
    this._client = client;
    this._db = this._client.GetDatabase(0);
  }

  public async Task<string?> Get(string key)
  {
    RedisValue result = await this._db.StringGetAsync(key);

    if (result.HasValue == false || result.IsNullOrEmpty)
    {
      return null;
    }

    return result.ToString();
  }

  public Task<bool> Set(string key, string value)
  {
    return this._db.StringSetAsync(key, value);
  }

  public Task<long> Enqueue(string queueName, string[] messages)
  {
    RedisValue[] values = messages.Select(message => (RedisValue)message)
      .ToArray();

    return this._db.ListLeftPushAsync(queueName, values, CommandFlags.None);
  }

  public async Task<string> Dequeue(string queueName)
  {
    var item = await this._db.ListMoveAsync(queueName, $"{queueName}_temp",
      ListSide.Right, ListSide.Left);

    return item.ToString();
  }

  public async Task<bool> Ack(string queueName, string message)
  {
    return await this._db.ListRemoveAsync($"{queueName}_temp", message, 0) > 0;
  }

  public Task<bool> Nack(string queueName, string message)
  {
    // @TODO: Log it

    return Ack(queueName, message);
  }
}