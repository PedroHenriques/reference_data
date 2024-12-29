using Moq;
using Microsoft.AspNetCore.Http;
using Api.Models;
using System.Text;

namespace Api.Tests.Models;

public class EntityTests : IDisposable
{
  private readonly Mock<HttpContext> _contextMock;
  public EntityTests()
  {
    this._contextMock = new Mock<HttpContext>(MockBehavior.Strict);

    this._contextMock.Setup(s => s.Request.Body)
      .Returns(new MemoryStream(Encoding.UTF8.GetBytes("{\"name\": \"test name\", \"description\": \"test desc\"}")));
  }

  public void Dispose()
  {
    this._contextMock.Reset();
  }

  [Fact]
  public async void BindAsync_ItShouldReadTheRequestBodyOnce()
  {
    Entity? sutResult = await Entity.BindAsync(this._contextMock.Object);

    this._contextMock.Verify(m => m.Request.Body, Times.Once);
  }

  [Fact]
  public async void BindAsync_ItShouldReturnAnInstanceOfTheModelEntity()
  {
    Entity? sutResult = await Entity.BindAsync(this._contextMock.Object);

    Assert.IsType<Entity>(sutResult);
  }
  
  [Fact]
  public async void BindAsync_ItShouldReturnAnInstanceOfTheModelEntityWithTheExpectedName()
  {
    Entity? sutResult = await Entity.BindAsync(this._contextMock.Object);

    Assert.Equal("test name", sutResult.Name);
  }

  [Fact]
  public async void BindAsync_ItShouldReturnAnInstanceOfTheModelEntityWithTheExpectedDescription()
  {
    Entity? sutResult = await Entity.BindAsync(this._contextMock.Object);

    Assert.Equal("test desc", sutResult.Desc);
  }

  [Fact]
  public async void BindAsync_IfTheRequestBodyIsEmpty_ItShouldThrowAnExceptionWithTheExpectedMessage()
  {
    this._contextMock.Setup(s => s.Request.Body)
      .Returns(new MemoryStream(Encoding.UTF8.GetBytes("")));
    
    Exception e = await Assert.ThrowsAsync<Exception>(async () => await Entity.BindAsync(this._contextMock.Object));
    Assert.Equal("Deserializing Entity produced NULL.", e.Message);
  }
}