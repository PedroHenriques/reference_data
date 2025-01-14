using MongoDB.Bson;
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

  [Fact]
  public void BuildChangeRecord_IfTheChangeIsForAnInsert_ItShouldReturnTheExpectedResult()
  {
    string changeStr = "{ \"_id\" : { \"_data\" : \"8267855CE0000000022B042C0100296E5A1004394D1CDEF4AA4FB5AC600371893E6E98463C6F7065726174696F6E54797065003C696E736572740046646F63756D656E744B65790046645F6964006467855CE01C6EB237197D1491000004\" }, \"operationType\" : \"insert\", \"clusterTime\" : { \"$timestamp\" : { \"t\" : 1736793312, \"i\" : 2 } }, \"wallTime\" : { \"$date\" : \"2025-01-13T18:35:12.212Z\" }, \"fullDocument\" : { \"_id\" : { \"$oid\" : \"67855ce01c6eb237197d1491\" }, \"name\" : \"myname1\", \"description\" : \"my desc 1\", \"deleted_at\" : null, \"some key\" : true }, \"ns\" : { \"db\" : \"RefData\", \"coll\" : \"Entities\" }, \"documentKey\" : { \"_id\" : { \"$oid\" : \"67855ce01c6eb237197d1491\" } } }";
    Dictionary<string, dynamic?> expectedInsertedOrEdited = new Dictionary<string, dynamic?>
    {
      { "_id", "{ \"$oid\" : \"67855ce01c6eb237197d1491\" }" },
      { "name", "\"myname1\"" },
      { "description", "\"my desc 1\"" },
      { "deleted_at", "null" },
      { "some key", "true" },
    };

    var result = Db.BuildChangeRecord(BsonDocument.Parse(changeStr));
    Assert.Equal(ChangeRecordTypes.Insert, result.ChangeType);
    Assert.Equal("67855ce01c6eb237197d1491", result.Id);
    Assert.Equal(expectedInsertedOrEdited, result.InsertedOrEdited);
  }

  [Fact]
  public void BuildChangeRecord_IfTheChangeIsForADelete_ItShouldReturnTheExpectedResult()
  {
    string changeStr = "{ \"_id\" : { \"_data\" : \"826785926B000000012B042C0100296E5A1004F7759FD7E91B4070A19D647641B40BB2463C6F7065726174696F6E54797065003C64656C6574650046646F63756D656E744B65790046645F696400646785925BEC2196EEFA69AC15000004\" }, \"operationType\" : \"delete\", \"clusterTime\" : { \"$timestamp\" : { \"t\" : 1736807019, \"i\" : 1 } }, \"wallTime\" : { \"$date\" : \"2025-01-13T22:23:39.475Z\" }, \"ns\" : { \"db\" : \"RefData\", \"coll\" : \"Entities\" }, \"documentKey\" : { \"_id\" : { \"$oid\" : \"6785925bec2196eefa69ac15\" } } }";

    var result = Db.BuildChangeRecord(BsonDocument.Parse(changeStr));
    Assert.Equal(
      new ChangeRecord
      {
        ChangeType = ChangeRecordTypes.Delete,
        Id = "6785925bec2196eefa69ac15",
      },
      result
    );
  }

  [Fact]
  public void BuildChangeRecord_IfTheChangeIsForAReplace_ItShouldReturnTheExpectedResult()
  {
    string changeStr = "{ \"_id\" : { \"_data\" : \"82678593B2000000012B042C0100296E5A1004F7759FD7E91B4070A19D647641B40BB2463C6F7065726174696F6E54797065003C7265706C6163650046646F63756D656E744B65790046645F6964006467859332EC2196EEFA69AC16000004\" }, \"operationType\" : \"replace\", \"clusterTime\" : { \"$timestamp\" : { \"t\" : 1736807346, \"i\" : 1 } }, \"wallTime\" : { \"$date\" : \"2025-01-13T22:29:06.099Z\" }, \"fullDocument\" : { \"_id\" : { \"$oid\" : \"67859332ec2196eefa69ac16\" }, \"name\" : \"new myname1\", \"description\" : \"my new desc 1\", \"deleted_at\" : null }, \"ns\" : { \"db\" : \"RefData\", \"coll\" : \"Entities\" }, \"documentKey\" : { \"_id\" : { \"$oid\" : \"67859332ec2196eefa69ac16\" } } }";
    Dictionary<string, dynamic?> expectedInsertedOrEdited = new Dictionary<string, dynamic?>
    {
      { "_id", "{ \"$oid\" : \"67859332ec2196eefa69ac16\" }" },
      { "name", "\"new myname1\"" },
      { "description", "\"my new desc 1\"" },
      { "deleted_at", "null" },
    };

    var result = Db.BuildChangeRecord(BsonDocument.Parse(changeStr));
    Assert.Equal(ChangeRecordTypes.Replace, result.ChangeType);
    Assert.Equal("67859332ec2196eefa69ac16", result.Id);
    Assert.Equal(expectedInsertedOrEdited, result.InsertedOrEdited);
  }

  [Fact]
  public void BuildChangeRecord_IfTheChangeIsForAnUpdate_ItShouldReturnTheExpectedResult()
  {
    string changeStr = "{ \"_id\" : { \"_data\" : \"8267867A2D000000012B042C0100296E5A1004136DA7DB84F74CBAAFFF0F382113F33A463C6F7065726174696F6E54797065003C7570646174650046646F63756D656E744B65790046645F696400646786797ED75765301BE8E23B000004\" }, \"operationType\" : \"update\", \"clusterTime\" : { \"$timestamp\" : { \"t\" : 1736866349, \"i\" : 1 } }, \"wallTime\" : { \"$date\" : \"2025-01-14T14:52:29.913Z\" }, \"ns\" : { \"db\" : \"RefData\", \"coll\" : \"Entities\" }, \"documentKey\" : { \"_id\" : { \"$oid\" : \"6786797ed75765301be8e23b\" } }, \"updateDescription\" : { \"updatedFields\" : { \"name\" : \"new name\" }, \"removedFields\" : [\"description\"], \"truncatedArrays\" : [] } }";
    Dictionary<string, dynamic?> expectedInsertedOrEdited = new Dictionary<string, dynamic?>
    {
      { "name", "\"new name\"" },
    };
    string[] expectedRemoved = new[] { "description" };

    var result = Db.BuildChangeRecord(BsonDocument.Parse(changeStr));
    Assert.Equal(ChangeRecordTypes.Updated, result.ChangeType);
    Assert.Equal("6786797ed75765301be8e23b", result.Id);
    Assert.Equal(expectedInsertedOrEdited, result.InsertedOrEdited);
    Assert.Equal(expectedRemoved, result.Removed);
  }

  [Fact]
  public void BuildChangeRecord_IfTheChangeDoesNotHaveADocumentKey_ItShouldThrowAnException()
  {
    string changeStr = "{ \"_id\" : { \"_data\" : \"8267867A2D000000012B042C0100296E5A1004136DA7DB84F74CBAAFFF0F382113F33A463C6F7065726174696F6E54797065003C7570646174650046646F63756D656E744B65790046645F696400646786797ED75765301BE8E23B000004\" }, \"operationType\" : \"update\", \"clusterTime\" : { \"$timestamp\" : { \"t\" : 1736866349, \"i\" : 1 } }, \"wallTime\" : { \"$date\" : \"2025-01-14T14:52:29.913Z\" }, \"ns\" : { \"db\" : \"RefData\", \"coll\" : \"Entities\" }, \"updateDescription\" : { \"updatedFields\" : { \"name\" : \"new name\" }, \"removedFields\" : [\"description\"], \"truncatedArrays\" : [] } }";

    Exception e = Assert.Throws<Exception>(() => Db.BuildChangeRecord(BsonDocument.Parse(changeStr)));
    Assert.Equal(
      "The change stream's backing document with ID { \"_data\" : \"8267867A2D000000012B042C0100296E5A1004136DA7DB84F74CBAAFFF0F382113F33A463C6F7065726174696F6E54797065003C7570646174650046646F63756D656E744B65790046645F696400646786797ED75765301BE8E23B000004\" } doesn't contain the value of 'documentKey'.",
      e.Message
    );
  }
}