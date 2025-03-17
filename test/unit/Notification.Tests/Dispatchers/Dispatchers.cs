using Moq;
using Notification.Dispatchers;
using SharedLibs.Types;
using SutND = Notification.Dispatchers;

namespace Notification.Tests.Dispatchers;

[Trait("Type", "Unit")]
public class DispatchersTests : IDisposable
{
  private readonly Mock<HttpMessageHandler> _httpClientMock;
  private readonly Mock<IEventBus<string, NotifData>> _eventBusMock;

  public DispatchersTests()
  {
    this._httpClientMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
    this._eventBusMock = new Mock<IEventBus<string, NotifData>>(MockBehavior.Strict);
  }

  public void Dispose()
  {
    this._httpClientMock.Reset();
  }

  [Fact]
  public void GetDispatcher_IfTheRequestedProtocolDoesNotExist_ItShouldReturnNull()
  {
    var sut = new SutND.Dispatchers(new HttpClient(this._httpClientMock.Object), this._eventBusMock.Object);

    Assert.Null(sut.GetDispatcher("n/a protocol"));
  }

  [Fact]
  public void GetDispatcher_IfTheRequestedProtocolIsWebhook_ItShouldReturnAnInstanceOfTheCorrespondingDispatcher()
  {
    var sut = new SutND.Dispatchers(new HttpClient(this._httpClientMock.Object), this._eventBusMock.Object);

    Assert.IsType<Webhook>(sut.GetDispatcher("webhook"));
  }

  [Fact]
  public void GetDispatcher_IfTheRequestedProtocolIsEvent_ItShouldReturnAnInstanceOfTheCorrespondingDispatcher()
  {
    var sut = new SutND.Dispatchers(new HttpClient(this._httpClientMock.Object), this._eventBusMock.Object);

    Assert.IsType<Kafka>(sut.GetDispatcher("event"));
  }
}