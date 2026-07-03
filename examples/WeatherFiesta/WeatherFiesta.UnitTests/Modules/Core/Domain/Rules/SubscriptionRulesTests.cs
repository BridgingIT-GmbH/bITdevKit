// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.UnitTests.Modules.Core.Domain.Rules;

using BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;
using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;

/// <summary>
/// Unit tests for subscription plan enforcement rules.
/// </summary>
public class SubscriptionRulesTests
{
    // ──────────────────────────────────────────────────────────────────────────────
    // SubscriptionCityLimitRule
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SubscriptionCityLimitRule_WhenUnderLimit_ReturnsSuccess()
    {
        // Arrange
        var subscription = UserSubscription.CreateFree("user1");
        var rule = new SubscriptionCityLimitRule(subscription, 2);

        // Act
        var result = Rule.Check(rule);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void SubscriptionCityLimitRule_WhenAtLimit_ReturnsFailure()
    {
        // Arrange
        var subscription = UserSubscription.CreateFree("user1");
        var rule = new SubscriptionCityLimitRule(subscription, 3);

        // Act
        var result = Rule.Check(rule);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Message.Contains("allows up to 3 cities"));
    }

    [Fact]
    public void SubscriptionCityLimitRule_WhenUnlimited_ReturnsSuccess()
    {
        // Arrange
        var subscription = UserSubscription.CreateFree("user1");
        subscription.ChangePlan(SubscriptionPlan.Enterprise, SubscriptionBillingCycle.Monthly);
        var rule = new SubscriptionCityLimitRule(subscription, 999);

        // Act
        var result = Rule.Check(rule);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // SubscriptionComparisonAllowedRule
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SubscriptionComparisonAllowedRule_WhenAllowed_ReturnsSuccess()
    {
        // Arrange
        var subscription = UserSubscription.CreateFree("user1");
        subscription.ChangePlan(SubscriptionPlan.Basic, SubscriptionBillingCycle.Monthly);
        var rule = new SubscriptionComparisonAllowedRule(subscription);

        // Act
        var result = Rule.Check(rule);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void SubscriptionComparisonAllowedRule_WhenNotAllowed_ReturnsFailure()
    {
        // Arrange
        var subscription = UserSubscription.CreateFree("user1");
        var rule = new SubscriptionComparisonAllowedRule(subscription);

        // Act
        var result = Rule.Check(rule);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Message.Contains("does not allow city comparison"));
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // SubscriptionExportAllowedRule
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SubscriptionExportAllowedRule_WhenAllowed_ReturnsSuccess()
    {
        // Arrange
        var subscription = UserSubscription.CreateFree("user1");
        subscription.ChangePlan(SubscriptionPlan.Basic, SubscriptionBillingCycle.Monthly);
        var rule = new SubscriptionExportAllowedRule(subscription);

        // Act
        var result = Rule.Check(rule);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void SubscriptionExportAllowedRule_WhenNotAllowed_ReturnsFailure()
    {
        // Arrange
        var subscription = UserSubscription.CreateFree("user1");
        var rule = new SubscriptionExportAllowedRule(subscription);

        // Act
        var result = Rule.Check(rule);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Message.Contains("does not allow data export"));
    }

    [Fact]
    public void UserSubscription_ActivePeriod_UsesStartAndOpenEnd()
    {
        // Arrange
        var subscription = UserSubscription.CreateFree("user1");

        // Act
        var period = subscription.ActivePeriod;

        // Assert
        period.StartInclusive.ShouldBe(subscription.StartDate);
        period.EndExclusive.ShouldBeNull();
        period.Contains(DateTime.UtcNow).ShouldBeTrue();
        period.ToIsoRangeString().ShouldNotBeNullOrWhiteSpace();
    }
}
