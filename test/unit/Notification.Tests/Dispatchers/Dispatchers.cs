using Moq;
using Notification.Dispatchers;
using Notification.Types;
using Toolkit.Types;
using SutND = Notification.Dispatchers;

namespace Notification.Tests.Dispatchers;

[Trait("Type", "Unit")]
public class DispatchersTests : IDisposable
{
  private readonly Mock<HttpMessageHandler> _httpClientMock;
  private readonly Mock<IKafka<NotifDataKafkaKey, NotifDataKafkaValue>> _kafkaMock;
  private readonly Mock<ILogger> _logger;

  public DispatchersTests()
  {
    this._httpClientMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
    this._kafkaMock = new Mock<IKafka<NotifDataKafkaKey, NotifDataKafkaValue>>(MockBehavior.Strict);
    this._logger = new Mock<ILogger>(MockBehavior.Strict);
  }

  public void Dispose()
  {
    this._httpClientMock.Reset();
    this._kafkaMock.Reset();
    this._logger.Reset();
  }

  [Fact]
  public void GetDispatcher_IfTheRequestedProtocolDoesNotExist_ItShouldReturnNull()
  {
    var sut = new SutND.Dispatchers(new HttpClient(this._httpClientMock.Object), this._kafkaMock.Object, this._logger.Object);

    Assert.Null(sut.GetDispatcher("n/a protocol"));
  }

  [Fact]
  public void GetDispatcher_IfTheRequestedProtocolIsWebhook_ItShouldReturnAnInstanceOfTheCorrespondingDispatcher()
  {
    var sut = new SutND.Dispatchers(new HttpClient(this._httpClientMock.Object), this._kafkaMock.Object, this._logger.Object);

    Assert.IsType<Webhook>(sut.GetDispatcher("webhook"));
  }

  [Fact]
  public void GetDispatcher_IfTheRequestedProtocolIsEvent_ItShouldReturnAnInstanceOfTheCorrespondingDispatcher()
  {
    var sut = new SutND.Dispatchers(new HttpClient(this._httpClientMock.Object), this._kafkaMock.Object, this._logger.Object);

    Assert.IsType<Kafka>(sut.GetDispatcher("event"));
  }
}