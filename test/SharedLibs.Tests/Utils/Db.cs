using MongoDB.Bson;
using MongoDB.Driver;
using SharedLibs.Types.Db;

namespace SharedLibs.Utils.Tests;

public class DbTests : IDisposable
{
  public DbTests() { }

  public void Dispose() { }

  [Fact]
  public void BuildStreamOpts_ItShouldReturnNull()
  {
    Assert.Null(Db.BuildStreamOpts(new ResumeData()));
  }

  [Fact]
  public void BuildStreamOpts_IfAResumeTokenIsProvided_ItShouldReturnTheExpectedValue()
  {
    var testToken = new BsonDocument("hello", "world");

    var result = Db.BuildStreamOpts(new ResumeData { ResumeToken = testToken.ToJson(), ClusterTime = "test time" });
    Assert.NotNull(result);
    Assert.Equal(testToken, result.ResumeAfter);
  }

  [Fact]
  public void BuildStreamOpts_IfAResumeTokenIsProvided_ItShouldReturnTheResultWithoutTheClusterTime()
  {
    var testToken = new BsonDocument("another hello", "world again");

    var result = Db.BuildStreamOpts(new ResumeData { ResumeToken = testToken.ToJson(), ClusterTime = "test time" });
    Assert.NotNull(result);
    Assert.Null(result.StartAtOperationTime);
  }

  [Fact]
  public void BuildStreamOpts_IfAResumeTokenIsNotProvided_ItShouldReturnTheExpectedValue()
  {
    var testTime = new BsonTimestamp(123456789);

    var result = Db.BuildStreamOpts(new ResumeData { ClusterTime = testTime.ToString() });
    Assert.NotNull(result);
    Assert.Equal(testTime, result.StartAtOperationTime);
  }

  [Fact]
  public void BuildStreamOpts_IfAResumeTokenIsNotProvided_ItShouldReturnTheResultWithoutTheResumeToken()
  {
    var testTime = new BsonTimestamp(987654321);

    var result = Db.BuildStreamOpts(new ResumeData { ClusterTime = testTime.ToString() });
    Assert.NotNull(result);
    Assert.Null(result.ResumeAfter);
  }
}