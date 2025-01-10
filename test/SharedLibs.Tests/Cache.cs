using Moq;
using SharedLibs.Types;
using StackExchange.Redis;

namespace SharedLibs.Tests;

public class CacheTests : IDisposable
{
  private readonly Mock<IConnectionMultiplexer> _redisClient;
  private readonly Mock<IDatabase> _redisDb;

  public CacheTests()
  {
    this._redisClient = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
    this._redisDb = new Mock<IDatabase>(MockBehavior.Strict);

    this._redisClient.Setup(s => s.GetDatabase(It.IsAny<int>(), null)).Returns(this._redisDb.Object);
    this._redisDb.Setup(s => s.StringGet(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).Returns(new RedisValue { });
    this._redisDb.Setup(s => s.ListLeftPushAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>())).Returns(Task.FromResult<long>(0));
  }

  public void Dispose()
  {
    this._redisClient.Reset();
    this._redisDb.Reset();
  }

  [Fact]
  public void Get_ItShouldCallGetDatabaseFromTheProvidedRedisClientOnce()
  {
    ICache sut = new Cache(this._redisClient.Object);

    var result = sut.Get(RedisTypes.String, "");
    this._redisClient.Verify(m => m.GetDatabase(0, null), Times.Once());
  }

  [Fact]
  public void Get_IfTheRequestedGetIsString_ItShouldCallStringGetOnTheRedisDatabaseOnce()
  {
    ICache sut = new Cache(this._redisClient.Object);

    var result = sut.Get(RedisTypes.String, "test key");
    this._redisDb.Verify(m => m.StringGet("test key", CommandFlags.None), Times.Once());
  }

  [Fact]
  public async void Enqueue_ItShouldCallGetDatabaseFromTheProvidedRedisClientOnce()
  {
    ICache sut = new Cache(this._redisClient.Object);

    await sut.Enqueue("", new[] { new RedisValue() });
    this._redisClient.Verify(m => m.GetDatabase(0, null), Times.Once());
  }

  [Fact]
  public async void Enqueue_ItShouldCallListLeftPushAsyncOnTheRedisDatabaseOnce()
  {
    ICache sut = new Cache(this._redisClient.Object);

    var expectedQName = "test queue name";
    var expectedData = new[] { new RedisValue() };
    await sut.Enqueue(expectedQName, expectedData);
    this._redisDb.Verify(m => m.ListLeftPushAsync(expectedQName, expectedData, CommandFlags.None), Times.Once());
  }

  [Fact]
  public async void Enqueue_ItShouldReturnTheResultOfCallingListLeftPushAsyncOnTheRedisDatabase()
  {
    this._redisDb.Setup(s => s.ListLeftPushAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult<long>(123456789));

    ICache sut = new Cache(this._redisClient.Object);

    Assert.Equal(123456789, await sut.Enqueue("", new[] { new RedisValue() }));
  }
}