using System.Net;
using MongoDB.Bson;
using Moq;
using Moq.Protected;
using Notification.Dispatchers;
using SharedLibs.Types.Notification;

namespace Notification.Tests.Dispatchers;

[Trait("Type", "Unit")]
public class WebhookTests : IDisposable
{
  private readonly Mock<HttpMessageHandler> _clientMock;

  public WebhookTests()
  {
    this._clientMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

    this._clientMock.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
      .Returns(Task.FromResult(new HttpResponseMessage { StatusCode = HttpStatusCode.OK }));

  }

  public void Dispose()
  {
    this._clientMock.Reset();
  }

  [Fact]
  public async void Dispatch_ItShouldCallSendAsyncFromTheProvidedHttpClientOnceWithTheExpectedMethod()
  {
    var sut = new Webhook(new HttpClient(this._clientMock.Object));

    NotifData data = new NotifData
    {
      ChangeTime = DateTime.Now,
      EventTime = DateTime.Now,
      ChangeType = "",
      Entity = "",
      Id = "",
    };
    await sut.Dispatch(data, "http://a.com");

    Assert.Equal(
      HttpMethod.Post,
      (this._clientMock.Invocations[0].Arguments[0] as dynamic).Method
    );
  }

  [Fact]
  public async void Dispatch_ItShouldCallSendAsyncFromTheProvidedHttpClientOnceWithTheExpectedRequestUri()
  {
    var sut = new Webhook(new HttpClient(this._clientMock.Object));

    NotifData data = new NotifData
    {
      ChangeTime = DateTime.Now,
      EventTime = DateTime.Now,
      ChangeType = "",
      Entity = "",
      Id = "",
    };
    await sut.Dispatch(data, "http://ww.my-test.url.com");

    Assert.Equal(
      new Uri("http://ww.my-test.url.com"),
      (this._clientMock.Invocations[0].Arguments[0] as dynamic).RequestUri
    );
  }

  [Fact]
  public async void Dispatch_ItShouldCallSendAsyncFromTheProvidedHttpClientOnceWithTheExpectedContent()
  {
    var sut = new Webhook(new HttpClient(this._clientMock.Object));

    NotifData data = new NotifData
    {
      ChangeTime = DateTime.Now,
      EventTime = DateTime.Now,
      ChangeType = "test change type",
      Entity = "test entity name",
      Id = "test id",
    };
    await sut.Dispatch(data, "http://a.com");

    Assert.Equal(
      await new StringContent(data.ToJson()).ReadAsStringAsync(),
      await (this._clientMock.Invocations[0].Arguments[0] as dynamic).Content.ReadAsStringAsync()
    );
  }

  [Fact]
  public async void Dispatch_ItShouldReturnTrue()
  {
    var sut = new Webhook(new HttpClient(this._clientMock.Object));

    NotifData data = new NotifData
    {
      ChangeTime = DateTime.Now,
      EventTime = DateTime.Now,
      ChangeType = "test change type",
      Entity = "test entity name",
      Id = "test id",
    };

    Assert.True(await sut.Dispatch(data, "http://a.com"));
  }

  [Fact]
  public async void Dispatch_IfThePostCallReturnsAFailureStatusCode_ItShouldReturnFalse()
  {
    this._clientMock.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
      .Returns(Task.FromResult(new HttpResponseMessage { StatusCode = HttpStatusCode.NotFound }));

    var sut = new Webhook(new HttpClient(this._clientMock.Object));

    NotifData data = new NotifData
    {
      ChangeTime = DateTime.Now,
      EventTime = DateTime.Now,
      ChangeType = "test change type",
      Entity = "test entity name",
      Id = "test id",
    };

    Assert.False(await sut.Dispatch(data, "http://a.com"));
  }
}