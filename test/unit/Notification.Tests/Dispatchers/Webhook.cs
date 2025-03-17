using System.Net;
using System.Net.Http.Headers;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;
using Notification.Dispatchers;
using SharedLibs.Types;

namespace Notification.Tests.Dispatchers;

[Trait("Type", "Unit")]
public class WebhookTests : IDisposable
{
  private readonly Mock<HttpMessageHandler> _clientMock;
  private readonly Mock<Action<bool>> _callbackMock;

  public WebhookTests()
  {
    this._clientMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
    this._callbackMock = new Mock<Action<bool>>(MockBehavior.Strict);

    this._clientMock.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
      .Returns(Task.FromResult(new HttpResponseMessage { StatusCode = HttpStatusCode.OK }));

  }

  public void Dispose()
  {
    this._clientMock.Reset();
    this._callbackMock.Reset();
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
    await sut.Dispatch(data, "http://a.com", this._callbackMock.Object);

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
    await sut.Dispatch(data, "http://ww.my-test.url.com", this._callbackMock.Object);

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
    await sut.Dispatch(data, "http://a.com", this._callbackMock.Object);

    Assert.Equal(
      await new StringContent(JsonConvert.SerializeObject(data)).ReadAsStringAsync(),
      await (this._clientMock.Invocations[0].Arguments[0] as dynamic).Content.ReadAsStringAsync()
    );
  }

  [Fact]
  public async void Dispatch_ItShouldCallSendAsyncFromTheProvidedHttpClientOnceWithTheExpectedContentTypeHeader()
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
    await sut.Dispatch(data, "http://a.com", this._callbackMock.Object);

    Assert.Equal(
      new MediaTypeHeaderValue("application/json", "utf-8"),
      (this._clientMock.Invocations[0].Arguments[0] as dynamic).Content.Headers.ContentType
    );
  }

  [Fact]
  public async void Dispatch_ItShouldCallTheProvidedCallbackOnceWithTrue()
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

    await sut.Dispatch(data, "http://a.com", this._callbackMock.Object);
    await Task.Delay(5);
    this._callbackMock.Verify(m => m(true), Times.Once());
  }

  [Fact]
  public async void Dispatch_IfThePostCallReturnsAFailureStatusCode_ItShouldCallTheProvidedCallbackOnceWithFalse()
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

    await sut.Dispatch(data, "http://a.com", this._callbackMock.Object);
    await Task.Delay(5);
    this._callbackMock.Verify(m => m(false), Times.Once());
  }
}