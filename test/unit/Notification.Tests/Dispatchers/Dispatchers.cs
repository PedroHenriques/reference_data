using Moq;
using Notification.Dispatchers;
using SharedLibs.Types;
using SutNS = Notification.Dispatchers;

namespace Notification.Tests.Dispatchers;

[Trait("Type", "Unit")]
public class DispatchersTests : IDisposable
{
  private readonly Mock<HttpMessageHandler> _httpClientMock;

  public DispatchersTests()
  {
    this._httpClientMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
  }

  public void Dispose()
  {
    this._httpClientMock.Reset();
  }

  [Fact]
  public void GetDispatcher_IfTheRequestedProtocolDoesNotExist_ItShouldReturnNull()
  {
    var sut = new SutNS.Dispatchers(new HttpClient(this._httpClientMock.Object));

    Assert.Null(sut.GetDispatcher("n/a protocol"));
  }

  [Fact]
  public void GetDispatcher_IfTheRequestedProtocolIsWebhook_ItShouldReturnAnInstanceOfTheCorrespondingDispatcher()
  {
    var sut = new SutNS.Dispatchers(new HttpClient(this._httpClientMock.Object));

    Assert.IsType<Webhook>(sut.GetDispatcher("webhook"));
  }
}