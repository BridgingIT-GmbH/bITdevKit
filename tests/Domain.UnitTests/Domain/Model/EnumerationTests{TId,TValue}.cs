// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Domain.Model;

using System;
using System.Linq;
using BridgingIT.DevKit.Domain.Model;
using Xunit;
using Shouldly;

[UnitTest("Domain")]
[Trait("Category", "Domain")]
public class EnumerationGenericTests
{
    [Fact]
    public void GetAll_ShouldReturnAllSubscriptionPlans()
    {
        // Arrange & Act
        var allPlans = SubscriptionPlans.GetAll<SubscriptionPlans>().ToList();

        // Assert
        allPlans.Count.ShouldBe(3);
        allPlans.ShouldContain(p => p.Id == "Free" && p.Value.PricePerMonth == 0m);
        allPlans.ShouldContain(p => p.Id == "Basic" && p.Value.PricePerMonth == 9.99m);
        allPlans.ShouldContain(p => p.Id == "Premium" && p.Value.PricePerMonth == 19.99m);
    }

    [Fact]
    public void FromId_WithValidId_ShouldReturnCorrectPlan()
    {
        // Arrange
        const string basicPlanId = "Basic";

        // Act
        var result = SubscriptionPlans.FromId<SubscriptionPlans>(basicPlanId);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(basicPlanId);
        result.Value.PricePerMonth.ShouldBe(9.99m);
    }

    [Fact]
    public void FromId_WithInvalidId_ShouldThrowInvalidOperationException()
    {
        // Arrange
        const string invalidId = "Invalid";

        // Act & Assert
        Should.Throw<InvalidOperationException>(() =>
            SubscriptionPlans.FromId<SubscriptionPlans>(invalidId)
        ).Message.ShouldBe($"'{invalidId}' is not a valid id for {typeof(SubscriptionPlans)}");
    }

    [Fact]
    public void FromValue_WithValidValue_ShouldReturnCorrectPlan()
    {
        // Arrange
        var premiumPlan = new StubSubscriptionPlanDetails("Premium Plan", 19.99m, 1000, 10);

        // Act
        var result = SubscriptionPlans.FromValue<SubscriptionPlans>(premiumPlan);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe("Premium");
        result.Value.ShouldBe(premiumPlan);
    }

    [Fact]
    public void FromValue_WithInvalidValue_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var invalidPlan = new StubSubscriptionPlanDetails("Invalid", 0m, 0, 0);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() =>
            SubscriptionPlans.FromValue<SubscriptionPlans>(invalidPlan)
        ).Message.ShouldContain("is not a valid value for");
    }

    [Fact]
    public void Equals_WithSamePlan_ShouldReturnTrue()
    {
        // Arrange
        var plan1 = SubscriptionPlans.Basic;
        var plan2 = SubscriptionPlans.FromId<SubscriptionPlans>("Basic");

        // Act & Assert
        plan1.Equals(plan2).ShouldBeTrue();
        (plan1 == plan2).ShouldBeTrue();
    }

    [Fact]
    public void Equals_WithDifferentPlan_ShouldReturnFalse()
    {
        // Arrange
        var freePlan = SubscriptionPlans.Free;
        var premiumPlan = SubscriptionPlans.Premium;

        // Act & Assert
        freePlan.Equals(premiumPlan).ShouldBeFalse();
        (freePlan == premiumPlan).ShouldBeFalse();
    }

    [Fact]
    public void GetHashCode_ShouldBeConsistent()
    {
        // Arrange
        var plan1 = SubscriptionPlans.Premium;
        var plan2 = SubscriptionPlans.FromId<SubscriptionPlans>("Premium");

        // Act & Assert
        plan1.GetHashCode().ShouldBe(plan2.GetHashCode());
    }
}

public class SubscriptionPlans(string id, StubSubscriptionPlanDetails value)
    : Enumeration<string, StubSubscriptionPlanDetails>(id, value)
{
    public static SubscriptionPlans Free =
        new("Free", new StubSubscriptionPlanDetails("Free Plan", 0m, 5, 1));
    public static SubscriptionPlans Basic =
        new("Basic", new StubSubscriptionPlanDetails("Basic Plan", 9.99m, 50, 5));
    public static SubscriptionPlans Premium =
        new("Premium", new StubSubscriptionPlanDetails("Premium Plan", 19.99m, 1000, 10));
}

public class StubSubscriptionPlanDetails(string name, decimal pricePerMonth, int storageInGB, int maxUsers)
        : IEquatable<StubSubscriptionPlanDetails>, IComparable
{
    public string Name { get; set; } = name;

    public decimal PricePerMonth { get; set; } = pricePerMonth;

    public int StorageInGB { get; set; } = storageInGB;

    public int MaxUsers { get; set; } = maxUsers;

    public bool Equals(StubSubscriptionPlanDetails other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return this.Name == other.Name &&
               this.PricePerMonth == other.PricePerMonth &&
               this.StorageInGB == other.StorageInGB &&
               this.MaxUsers == other.MaxUsers;
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != this.GetType())
        {
            return false;
        }

        return this.Equals((StubSubscriptionPlanDetails)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(this.Name, this.PricePerMonth, this.StorageInGB, this.MaxUsers);
    }

    public int CompareTo(object obj)
    {
        if (obj == null)
        {
            return 1;
        }

        if (!(obj is StubSubscriptionPlanDetails other))
        {
            throw new ArgumentException("Object is not a StubSubscriptionPlanDetails");
        }

        var priceComparison = this.PricePerMonth.CompareTo(other.PricePerMonth);
        if (priceComparison != 0)
        {
            return priceComparison;
        }

        return string.Compare(this.Name, other.Name, StringComparison.Ordinal);
    }
}