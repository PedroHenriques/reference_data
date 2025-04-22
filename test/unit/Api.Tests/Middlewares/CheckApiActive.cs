using System.Net;
using Api.Middleware;
using SharedLibs.Services;
using Microsoft.AspNetCore.Http;
using Moq;

namespace Api.Tests.Middlewares;

[Trait("Type", "Unit")]
public class CheckApiActiveTests : IDisposable
{
  private readonly Mock<RequestDelegate> _reqDelegateMock;
  private readonly Mock<HttpContext> _contextMock;
  private readonly Mock<HttpResponse> _responseMock;

  public CheckApiActiveTests()
  {
    Environment.SetEnvironmentVariable("LD_API_ACTIVE_KEY", "test flag key");

    FeatureFlags.FlagValues = new Dictionary<string, bool> { { "test flag key", true } };
    this._reqDelegateMock = new Mock<RequestDelegate>(MockBehavior.Strict);
    this._contextMock = new Mock<HttpContext>(MockBehavior.Strict);
    this._responseMock = new Mock<HttpResponse>();

    this._reqDelegateMock.Setup(s => s.Invoke(It.IsAny<HttpContext>()))
      .Returns(Task.CompletedTask);

    this._contextMock.Setup(s => s.Response)
      .Returns(this._responseMock.Object);
  }

  public void Dispose()
  {
    Environment.SetEnvironmentVariable("LD_API_ACTIVE_KEY", null);

    this._reqDelegateMock.Reset();
    this._contextMock.Reset();
    this._responseMock.Reset();
  }

  [Fact]
  public async Task InvokeAsync_ItShouldCallTheProvidedRequestDelegateOnceWithTheHttpContextAsArgument()
  {
    var sut = new CheckApiActiveMiddleware(this._reqDelegateMock.Object);

    await sut.InvokeAsync(this._contextMock.Object);

    this._reqDelegateMock.Verify(m => m.Invoke(this._contextMock.Object), Times.Once());
  }

  [Fact]
  public async Task InvokeAsync_ItShouldNotSetTheResponseStatusCode()
  {
    var sut = new CheckApiActiveMiddleware(this._reqDelegateMock.Object);

    await sut.InvokeAsync(this._contextMock.Object);

    this._responseMock.Verify(m => m.StatusCode, Times.Never());
  }

  [Fact]
  public async Task InvokeAsync_IfTheFeatureFlagForTheApiBeingActiveIsFalse_ItShouldNotCallTheProvidedRequestDelegate()
  {
    FeatureFlags.FlagValues["test flag key"] = false;
    var sut = new CheckApiActiveMiddleware(this._reqDelegateMock.Object);

    await sut.InvokeAsync(this._contextMock.Object);

    this._reqDelegateMock.Verify(m => m.Invoke(It.IsAny<HttpContext>()), Times.Never());
  }

  [Fact]
  public async Task InvokeAsync_IfTheFeatureFlagForTheApiBeingActiveIsFalse_ItShouldSetTheResponseStatusCodeTo503()
  {
    FeatureFlags.FlagValues["test flag key"] = false;
    var sut = new CheckApiActiveMiddleware(this._reqDelegateMock.Object);

    await sut.InvokeAsync(this._contextMock.Object);

    this._responseMock.VerifySet(m => m.StatusCode = (int)HttpStatusCode.ServiceUnavailable);
  }
}