using SharedLibs.Services;
using LaunchDarkly.Sdk;
using LaunchDarkly.Sdk.Server.Interfaces;
using Moq;
using Toolkit.Types;

namespace SharedLibs.Tests.Services;

[Trait("Type", "Unit")]
public class FeatureFlagsTests : IDisposable
{
  private readonly Mock<IFeatureFlags> _featureFlagsMock;

  public FeatureFlagsTests()
  {
    this._featureFlagsMock = new Mock<IFeatureFlags>(MockBehavior.Strict);

    this._featureFlagsMock.Setup(s => s.GetBoolFlagValue(It.IsAny<string>()))
      .Returns(true);
    this._featureFlagsMock.Setup(s => s.SubscribeToValueChanges(It.IsAny<string>(), It.IsAny<Action<FlagValueChangeEvent>>()));
  }

  public void Dispose()
  {
    this._featureFlagsMock.Reset();
  }

  [Fact]
  public void Constructor_IfTheKeysArrayHasThreeValues_ItShouldCallGetBoolFlagValueFromTheProvidedIFeatureFlagInstanceThreeTimes()
  {
    var sut = new FeatureFlags(this._featureFlagsMock.Object, ["test flag key", "key 2", "something"]);

    this._featureFlagsMock.Verify(m => m.GetBoolFlagValue(It.IsAny<string>()), Times.Exactly(3));
  }

  [Fact]
  public void Constructor_IfTheKeysArrayHasThreeValues_FirstCallToGetBoolFlagValueFromTheProvidedIFeatureFlagInstance_ItShouldHaveTheFirstKey()
  {
    var sut = new FeatureFlags(this._featureFlagsMock.Object, ["test flag key", "key 2", "something"]);

    this._featureFlagsMock.Verify(m => m.GetBoolFlagValue("test flag key"), Times.Once());
  }

  [Fact]
  public void Constructor_IfTheKeysArrayHasThreeValues_SecondCallToGetBoolFlagValueFromTheProvidedIFeatureFlagInstance_ItShouldHaveTheSecondKey()
  {
    var sut = new FeatureFlags(this._featureFlagsMock.Object, ["test flag key", "key 2", "something"]);

    this._featureFlagsMock.Verify(m => m.GetBoolFlagValue("key 2"), Times.Once());
  }

  [Fact]
  public void Constructor_IfTheKeysArrayHasThreeValues_ThirdCallToGetBoolFlagValueFromTheProvidedIFeatureFlagInstance_ItShouldHaveTheThirdKey()
  {
    var sut = new FeatureFlags(this._featureFlagsMock.Object, ["test flag key", "key 2", "something"]);

    this._featureFlagsMock.Verify(m => m.GetBoolFlagValue("something"), Times.Once());
  }

  [Fact]
  public void Constructor_IfTheKeysArrayHasThreeValues_ItShouldSetEachFlagValueInThePublicPropertyFlagValues()
  {
    this._featureFlagsMock.SetupSequence(s => s.GetBoolFlagValue(It.IsAny<string>()))
      .Returns(false).Returns(true).Returns(false);

    var sut = new FeatureFlags(this._featureFlagsMock.Object, ["test flag key", "key 2", "something"]);

    Assert.Equal(
      new Dictionary<string, bool> {
        { "test flag key", false },
        { "key 2", true },
        { "something", false },
      },
      FeatureFlags.FlagValues
    );
  }

  [Fact]
  public void Constructor_IfTheKeysArrayHasThreeValues_ItShouldCallSubscribeToValueChangesFromTheProvidedIFeatureFlagInstanceThreeTimes()
  {
    var sut = new FeatureFlags(this._featureFlagsMock.Object, ["test flag key", "key 2", "something"]);

    this._featureFlagsMock.Verify(m => m.SubscribeToValueChanges(It.IsAny<string>(), It.IsAny<Action<FlagValueChangeEvent>>()), Times.Exactly(3));
  }

  [Fact]
  public void Constructor_IfTheKeysArrayHasThreeValues_FirstCallToSubscribeToValueChangesFromTheProvidedIFeatureFlagInstance_ItShouldHaveTheFirstKeyAndAHandler()
  {
    var sut = new FeatureFlags(this._featureFlagsMock.Object, ["test flag key", "key 2", "something"]);

    this._featureFlagsMock.Verify(m => m.SubscribeToValueChanges("test flag key", It.IsAny<Action<FlagValueChangeEvent>>()), Times.Once());
  }

  [Fact]
  public void Constructor_IfTheKeysArrayHasThreeValues_FirstCallToSubscribeToValueChangesFromTheProvidedIFeatureFlagInstance_InvokingTheHandler_ItShouldUpdateTheFlagValueInThePublicPropertyFlagValues()
  {
    this._featureFlagsMock.SetupSequence(s => s.GetBoolFlagValue(It.IsAny<string>()))
      .Returns(true).Returns(false).Returns(true);
    var sut = new FeatureFlags(this._featureFlagsMock.Object, ["test flag key", "key 2", "something"]);

    var testEvent = new FlagValueChangeEvent("test flag key", LdValue.Of(true), LdValue.Of(false));

    (this._featureFlagsMock.Invocations[1].Arguments[1] as Action<FlagValueChangeEvent>)(testEvent);
    Assert.Equal(
      new Dictionary<string, bool> {
        { "test flag key", false },
        { "key 2", false },
        { "something", true },
      },
      FeatureFlags.FlagValues
    );
  }

  [Fact]
  public void Constructor_IfTheKeysArrayHasThreeValues_SecondCallToSubscribeToValueChangesFromTheProvidedIFeatureFlagInstance_ItShouldHaveTheSecondKeyAndAHandler()
  {
    var sut = new FeatureFlags(this._featureFlagsMock.Object, ["test flag key", "key 2", "something"]);

    this._featureFlagsMock.Verify(m => m.SubscribeToValueChanges("key 2", It.IsAny<Action<FlagValueChangeEvent>>()), Times.Once());
  }

  [Fact]
  public void Constructor_IfTheKeysArrayHasThreeValues_SecondCallToSubscribeToValueChangesFromTheProvidedIFeatureFlagInstance_InvokingTheHandler_ItShouldUpdateTheFlagValueInThePublicPropertyFlagValues()
  {
    this._featureFlagsMock.SetupSequence(s => s.GetBoolFlagValue(It.IsAny<string>()))
      .Returns(true).Returns(false).Returns(true);
    var sut = new FeatureFlags(this._featureFlagsMock.Object, ["test flag key", "key 2", "something"]);

    var testEvent = new FlagValueChangeEvent("key 2", LdValue.Of(false), LdValue.Of(true));

    (this._featureFlagsMock.Invocations[3].Arguments[1] as Action<FlagValueChangeEvent>)(testEvent);
    Assert.Equal(
      new Dictionary<string, bool> {
        { "test flag key", true },
        { "key 2", true },
        { "something", true },
      },
      FeatureFlags.FlagValues
    );
  }

  [Fact]
  public void Constructor_IfTheKeysArrayHasThreeValues_ThirdCallToSubscribeToValueChangesFromTheProvidedIFeatureFlagInstance_ItShouldHaveTheThirdKeyAndAHandler()
  {
    var sut = new FeatureFlags(this._featureFlagsMock.Object, ["test flag key", "key 2", "something"]);

    this._featureFlagsMock.Verify(m => m.SubscribeToValueChanges("something", It.IsAny<Action<FlagValueChangeEvent>>()), Times.Once());
  }

  [Fact]
  public void Constructor_IfTheKeysArrayHasThreeValues_ThirdCallToSubscribeToValueChangesFromTheProvidedIFeatureFlagInstance_InvokingTheHandler_ItShouldUpdateTheFlagValueInThePublicPropertyFlagValues()
  {
    this._featureFlagsMock.SetupSequence(s => s.GetBoolFlagValue(It.IsAny<string>()))
      .Returns(true).Returns(false).Returns(true);
    var sut = new FeatureFlags(this._featureFlagsMock.Object, ["test flag key", "key 2", "something"]);

    var testEvent = new FlagValueChangeEvent("something", LdValue.Of(true), LdValue.Of(false));

    (this._featureFlagsMock.Invocations[3].Arguments[1] as Action<FlagValueChangeEvent>)(testEvent);
    Assert.Equal(
      new Dictionary<string, bool> {
        { "test flag key", true },
        { "key 2", false },
        { "something", false },
      },
      FeatureFlags.FlagValues
    );
  }
}