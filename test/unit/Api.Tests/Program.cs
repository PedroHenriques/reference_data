using Moq;

namespace Api.Tests;

[Trait("Type", "Unit")]
public class ProgramTests : IDisposable
{
  public ProgramTests()
  {
    // Setup the mocks here
  }

  public void Dispose()
  {
    // .Reset() all the mocks here
  }

  [Fact]
  public void SomePublicMethod_ItShouldBeGreen()
  {
    Assert.Equal(1, 1);
  }
}