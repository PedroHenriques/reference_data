using SharedLibs.Types;
using StackExchange.Redis;

namespace SharedLibs;

public interface ICache
{
  public RedisValue Get(RedisTypes type, RedisKey key);
}

public class Cache : ICache
{
  private readonly IConnectionMultiplexer _client;

  public Cache(IConnectionMultiplexer client)
  {
    this._client = client;
  }

  public RedisValue Get(RedisTypes type, RedisKey key)
  {
    IDatabase db = this._client.GetDatabase(0);

    return db.StringGet(key);
  }
}