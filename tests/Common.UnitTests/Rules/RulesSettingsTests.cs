// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Rules;

using Rules = BridgingIT.DevKit.Common.Rules;

[UnitTest("Common")]
public class RulesSettingsTests(RulesFixture fixture) : IClassFixture<RulesFixture>
{
    private readonly RulesFixture fixture = fixture;
    private readonly Faker faker = new();

    [Fact]
    public void Setup_ShouldConfigureGlobalSettings()
    {
        // Arrange
        var settingsBuilder = new RuleSettingsBuilder
        {
            ThrowOnRuleFailure = true,
            ThrowOnRuleException = false
        };

        // Act
        Rules.Setup(builder =>
        {
            builder.ThrowOnRuleFailure = settingsBuilder.ThrowOnRuleFailure;
            builder.ThrowOnRuleException = settingsBuilder.ThrowOnRuleException;
        });

        // Assert
        Rules.Settings.ThrowOnRuleFailure.ShouldBeTrue();
        Rules.Settings.ThrowOnRuleException.ShouldBeFalse();
    }
}