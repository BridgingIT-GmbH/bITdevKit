// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Rules;

[UnitTest("Common")]
public class RulesSettingsTests(RulesFixture fixture) : IClassFixture<RulesFixture>
{
    private readonly RulesFixture fixture = fixture;

    [Fact]
    public void Setup_ShouldConfigureGlobalSettings()
    {
        // Arrange
        // Act
        Rule.Setup(b => b
            .ThrowOnRuleFailure()
            .ThrowOnRuleException(false));

        // Assert
        Rule.Settings.ThrowOnRuleFailure.ShouldBeTrue();
        Rule.Settings.ThrowOnRuleException.ShouldBeFalse();
    }

    [Fact]
    public void Setup_ShouldUpdateAllSettings()
    {
        // Arrange
        var logger = Substitute.For<IRuleLogger>();
        var customExceptionFactory = new Func<IRule, RuleException>(r => new RuleException(r, "Custom"));
        var customErrorFactory = new Func<IRule, Exception, RuleExceptionError>((r, ex) => new RuleExceptionError(r, ex));

        // Act
        Rule.Setup(b => b
            .ThrowOnRuleFailure()
            .ThrowOnRuleException()
            .SetLogger(logger)
            .SetRuleFailureExceptionFactory(customExceptionFactory)
            .SetRuleExceptionErrorFactory(customErrorFactory));

        // Assert
        var settings = Rule.Settings;
        settings.ThrowOnRuleFailure.ShouldBeTrue();
        settings.ThrowOnRuleException.ShouldBeTrue();
        settings.Logger.ShouldBe(logger);
        settings.RuleFailureExceptionFactory.ShouldBe(customExceptionFactory);
        settings.RuleExceptionErrorFactory.ShouldBe(customErrorFactory);
    }

    [Fact]
    public void Setup_WithContinueOnRuleFailure_ShouldUpdateSetting()
    {
        // Act
        Rule.Setup(b => b.ContinueOnRuleFailure(true));

        // Assert
        Rule.Settings.ContinueOnRuleFailure.ShouldBeTrue();
    }

    [Fact]
    public void Setup_WithNullLogger_ShouldUseNullLogger()
    {
        // Act
        Rule.Setup(b => b.SetLogger(null));

        // Assert
        Rule.Settings.Logger.ShouldBeOfType<RuleNullLogger>();
    }

    [Fact]
    public void Setup_WithNullFactories_ShouldUseDefaultFactories()
    {
        // Act
        Rule.Setup(b => b
            .SetRuleFailureExceptionFactory(null)
            .SetRuleExceptionErrorFactory(null));

        // Assert
        Rule.Settings.RuleFailureExceptionFactory.ShouldNotBeNull();
        Rule.Settings.RuleExceptionErrorFactory.ShouldNotBeNull();

        // Verify default factory behavior
        var rule = Substitute.For<IRule>();
        var exception = Rule.Settings.RuleFailureExceptionFactory(rule);
        exception.ShouldBeOfType<RuleException>();

        var error = Rule.Settings.RuleExceptionErrorFactory(rule, new Exception());
        error.ShouldBeOfType<RuleExceptionError>();
    }

    [Fact]
    public void Setup_MultipleConfigurations_ShouldMaintainLastSetValues()
    {
        // Act
        Rule.Setup(b => b.ThrowOnRuleFailure(true));
        Rule.Setup(b => b.ThrowOnRuleFailure(false));

        // Assert
        Rule.Settings.ThrowOnRuleFailure.ShouldBeFalse();
    }
}