// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Domain.Policies;

using BridgingIT.DevKit.Domain;
using Shouldly;
using Xunit;

public class DomainPolicyResultsTests
{
    [Fact]
    public void AddValue_ShouldStoreValue()
    {
        // Arrange
        var results = new DomainPolicyResults<StubContext>();
        var value = 42;

        // Act
        results.AddValue<ConditionalEnabledPolicy>(value);

        // Assert
        results.GetValue<ConditionalEnabledPolicy, int>().ShouldBe(value);
    }

    [Fact]
    public void GetValue_ShouldReturnDefault_WhenPolicyTypeNotFound()
    {
        // Arrange
        var results = new DomainPolicyResults<StubContext>();

        // Act
        var result = results.GetValue<ConditionalEnabledPolicy, int>();

        // Assert
        result.ShouldBe(default);
    }

    [Fact]
    public void GetValue_ShouldReturnDefault_WhenValueTypeMismatch()
    {
        // Arrange
        var results = new DomainPolicyResults<StubContext>();
        results.AddValue<ConditionalEnabledPolicy>("string value");

        // Act
        var result = results.GetValue<ConditionalEnabledPolicy, int>();

        // Assert
        result.ShouldBe(default);
    }

    [Fact]
    public void AddValue_ShouldOverwriteExistingValue()
    {
        // Arrange
        var results = new DomainPolicyResults<StubContext>();
        var initialValue = 42;
        var newValue = 84;

        // Act
        results.AddValue<ConditionalEnabledPolicy>(initialValue);
        results.AddValue<ConditionalEnabledPolicy>(newValue);

        // Assert
        results.GetValue<ConditionalEnabledPolicy, int>().ShouldBe(newValue);
    }

    [Fact]
    public void GetValue_ByType_ShouldReturnCorrectValue()
    {
        // Arrange
        var results = new DomainPolicyResults<StubContext>();
        var value = 42;
        var policyType = typeof(ConditionalEnabledPolicy);

        // Act
        results.AddValue(policyType, value);
        var result = results.GetValue<int>(policyType);

        // Assert
        result.ShouldBe(value);
    }

    [Fact]
    public void GetValue_ByType_ShouldReturnDefault_WhenTypeNotFound()
    {
        // Arrange
        var results = new DomainPolicyResults<StubContext>();
        var policyType = typeof(ConditionalEnabledPolicy);

        // Act
        var result = results.GetValue<int>(policyType);

        // Assert
        result.ShouldBe(default);
    }
}