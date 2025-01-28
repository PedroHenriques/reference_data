using Moq;
using SharedLibs.Types.Cache;
using StackExchange.Redis;

namespace SharedLibs.Tests;

[Trait("Type", "Unit")]
public class CacheTests : IDisposable
{
  private readonly Mock<IConnectionMultiplexer> _redisClient;
  private readonly Mock<IDatabase> _redisDb;

  public CacheTests()
  {
    this._redisClient = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
    this._redisDb = new Mock<IDatabase>(MockBehavior.Strict);

    this._redisClient.Setup(s => s.GetDatabase(It.IsAny<int>(), null))
      .Returns(this._redisDb.Object);
    this._redisDb.Setup(s => s.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult(new RedisValue { }));
    this._redisDb.Setup(s => s.ListLeftPushAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult<long>(0));
    this._redisDb.Setup(s => s.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), null, It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult<bool>(true));
    this._redisDb.Setup(s => s.ListMoveAsync(It.IsAny<RedisKey>(), It.IsAny<RedisKey>(), It.IsAny<ListSide>(), It.IsAny<ListSide>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult<RedisValue>(new RedisValue("")));
    this._redisDb.Setup(s => s.ListRemoveAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult<long>(1));
  }

  public void Dispose()
  {
    this._redisClient.Reset();
    this._redisDb.Reset();
  }

  [Fact]
  public async void Get_ItShouldCallGetDatabaseFromTheProvidedRedisClientOnce()
  {
    ICache sut = new Cache(this._redisClient.Object);

    await sut.Get("");
    this._redisClient.Verify(m => m.GetDatabase(0, null), Times.Once());
  }

  [Fact]
  public async void Get_ItShouldCallStringGetAsyncOnTheRedisDatabaseOnce()
  {
    ICache sut = new Cache(this._redisClient.Object);

    await sut.Get("test key");
    this._redisDb.Verify(m => m.StringGetAsync("test key", CommandFlags.None), Times.Once());
  }

  [Fact]
  public async void Get_ItShouldReturnTheStringCastOfTheResultOfCallingStringGetAsyncOnTheRedisDatabase()
  {
    string expectedResult = "test string from Redis";
    this._redisDb.Setup(s => s.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult(new RedisValue(expectedResult)));

    ICache sut = new Cache(this._redisClient.Object);

    Assert.Equal(expectedResult, await sut.Get("test key"));
  }

  [Fact]
  public async void Get_IfTheResultOfCallingStringGetAsyncOnTheRedisDatabaseIsEmpty_ItShouldReturnNull()
  {
    this._redisDb.Setup(s => s.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult(new RedisValue()));

    ICache sut = new Cache(this._redisClient.Object);

    Assert.Null(await sut.Get("test key"));
  }

  [Fact]
  public async void Set_ItShouldCallGetDatabaseFromTheProvidedRedisClientOnce()
  {
    ICache sut = new Cache(this._redisClient.Object);

    await sut.Set("", "");
    this._redisClient.Verify(m => m.GetDatabase(0, null), Times.Once());
  }

  [Fact]
  public async void Set_IfTheRequestedGetIsString_ItShouldCallStringSetAsyncOnTheRedisDatabaseOnce()
  {
    ICache sut = new Cache(this._redisClient.Object);

    await sut.Set("test key", "test value");
    this._redisDb.Verify(m => m.StringSetAsync("test key", "test value", null, false, When.Always, CommandFlags.None), Times.Once());
  }

  [Fact]
  public async void Enqueue_ItShouldCallGetDatabaseFromTheProvidedRedisClientOnce()
  {
    IQueue sut = new Cache(this._redisClient.Object);

    await sut.Enqueue("", new[] { "" });
    this._redisClient.Verify(m => m.GetDatabase(0, null), Times.Once());
  }

  [Fact]
  public async void Enqueue_ItShouldCallListLeftPushAsyncOnTheRedisDatabaseOnce()
  {
    IQueue sut = new Cache(this._redisClient.Object);

    var expectedQName = "test queue name";
    var expectedData = new[] { "test data" };
    await sut.Enqueue(expectedQName, expectedData);
    this._redisDb.Verify(m => m.ListLeftPushAsync(expectedQName, new[] { new RedisValue("test data") }, CommandFlags.None), Times.Once());
  }

  [Fact]
  public async void Enqueue_ItShouldReturnTheResultOfCallingListLeftPushAsyncOnTheRedisDatabase()
  {
    this._redisDb.Setup(s => s.ListLeftPushAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult<long>(123456789));

    IQueue sut = new Cache(this._redisClient.Object);

    Assert.Equal(123456789, await sut.Enqueue("", new[] { "" }));
  }

  [Fact]
  public async void Dequeue_ItShouldCallGetDatabaseFromTheProvidedRedisClientOnce()
  {
    IQueue sut = new Cache(this._redisClient.Object);

    await sut.Dequeue("some queue name");
    this._redisClient.Verify(m => m.GetDatabase(0, null), Times.Once());
  }

  [Fact]
  public async void Dequeue_ItShouldCallListMoveAsyncOnTheRedisDatabaseOnce()
  {
    IQueue sut = new Cache(this._redisClient.Object);

    var expectedSourceQName = "another test queue name";
    var expectedTargetQName = "another test queue name_temp";
    await sut.Dequeue(expectedSourceQName);
    this._redisDb.Verify(m => m.ListMoveAsync(expectedSourceQName, expectedTargetQName, ListSide.Right, ListSide.Left, CommandFlags.None), Times.Once());
  }

  [Fact]
  public async void Dequeue_ItShouldReturnTheStringValueReceivedFromCallingListMoveAsyncOnTheRedisDatabase()
  {
    string expectedResult = "some test json string";
    this._redisDb.Setup(s => s.ListMoveAsync(It.IsAny<RedisKey>(), It.IsAny<RedisKey>(), It.IsAny<ListSide>(), It.IsAny<ListSide>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult<RedisValue>(new RedisValue(expectedResult)));

    IQueue sut = new Cache(this._redisClient.Object);

    var result = await sut.Dequeue("");
    Assert.Equal(expectedResult, result);
  }

  [Fact]
  public async void Ack_ItShouldReturnTrue()
  {
    IQueue sut = new Cache(this._redisClient.Object);

    Assert.True(await sut.Ack("", ""));
  }

  [Fact]
  public async void Ack_ItShouldCallGetDatabaseFromTheProvidedRedisClientOnce()
  {
    IQueue sut = new Cache(this._redisClient.Object);

    await sut.Ack("", "");
    this._redisClient.Verify(m => m.GetDatabase(0, null), Times.Once());
  }

  [Fact]
  public async void Ack_ItShouldCallListRemoveAsyncOnTheRedisDatabaseOnce()
  {
    IQueue sut = new Cache(this._redisClient.Object);

    await sut.Ack("test q", "some value");
    this._redisDb.Verify(m => m.ListRemoveAsync("test q_temp", "some value", 0, CommandFlags.None), Times.Once());
  }

  [Fact]
  public async void Ack_IfNoItemsAreRemvedFromTheList_ItShouldReturnFalse()
  {
    this._redisDb.Setup(s => s.ListRemoveAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult<long>(0));

    IQueue sut = new Cache(this._redisClient.Object);

    Assert.False(await sut.Ack("test q", "some value"));
  }
}