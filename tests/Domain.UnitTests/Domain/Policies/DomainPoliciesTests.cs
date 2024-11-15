// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Domain.Policies;

using System.Linq;
using System.Threading.Tasks;
using BridgingIT.DevKit.Domain;
using Shouldly;
using Xunit;

public class DomainPoliciesTests
{
    [Fact]
    public async Task ApplyAsync_ShouldApplyAllPolicies()
    {
        // Arrange
        var context = new StubContext { Value = 1 };
        var policies = new IDomainPolicy<StubContext>[]
        {
            new EnabledPolicy(), new ModifyContextPolicy(), new ConditionalEnabledPolicy()
        };

        // Act
        var result = await DomainPolicies.ApplyAsync(context, policies);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Messages.Count.ShouldBe(3);
        result.Messages.ShouldContain("Always satisfied policy applied");
        result.Messages.ShouldContain("Context modified");
        result.PolicyResults.GetValue<EnabledPolicy, int>().ShouldBe(1);
        result.PolicyResults.GetValue<ConditionalEnabledPolicy, int>().ShouldBe(3);
        context.Value.ShouldBe(11); // Original value + 10
    }

    [Fact]
    public async Task ApplyAsync_WithFailingPolicy_ShouldContinueOnFailure()
    {
        // Arrange
        var context = new StubContext { Value = 1 };
        var policies = new IDomainPolicy<StubContext>[]
        {
            new EnabledPolicy(), new ModifyContextPolicy(), new FailingPolicy(), new ConditionalEnabledPolicy()
        };

        // Act
        var result = await DomainPolicies.ApplyAsync(context, policies);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Messages.Count.ShouldBe(3);
        result.Messages.ShouldContain("Always satisfied policy applied");
        result.Messages.ShouldContain("Context modified");
        result.Errors.Count.ShouldBe(1);
        result.Errors.Single().ShouldBeOfType<TestError>();
        result.PolicyResults.GetValue<EnabledPolicy, int>().ShouldBe(1);
        result.PolicyResults.GetValue<FailingPolicy, int>().ShouldBe(0);
        result.PolicyResults.GetValue<ConditionalEnabledPolicy, int>().ShouldBe(3);
        context.Value.ShouldBe(11); // Original value + 10
    }

    [Fact]
    public async Task ApplyAsync_WithFailingPolicyAndThrowOnFailure_ShouldThrowDomainPolicyException()
    {
        // Arrange
        var context = new StubContext { Value = 1 };
        var policies = new IDomainPolicy<StubContext>[]
        {
            new EnabledPolicy(), new ModifyContextPolicy(), new FailingPolicy(), // this policy will cause throw
            new ConditionalEnabledPolicy()
        };

        // Act & Assert
        var exception = await Should.ThrowAsync<DomainPolicyException>(async () =>
        {
            await DomainPolicies.ApplyAsync(context, policies, DomainPolicyProcessingMode.ThrowOnPolicyFailure);
        });

        exception.ShouldNotBeNull();
        exception.Message.ShouldNotBeNull();
        exception.Message.ShouldBe("FailingPolicy policy failed");
        exception.Result.IsFailure.ShouldBeTrue();
        exception.Result.Messages.Count.ShouldBe(2);
        exception.Result.Messages[0].ShouldBe("Always satisfied policy applied");
        exception.Result.Messages[1].ShouldBe("Context modified");
        exception.Result.Errors.Count.ShouldBe(1);
        exception.Result.Errors.Single().ShouldBeOfType<TestError>();
        context.Value.ShouldBe(11); // Original value + 10
    }

    [Fact]
    public async Task ApplyAsync_WithDisabledPolicy_ShouldSkipPolicy()
    {
        // Arrange
        var context = new StubContext { Value = 1 };
        var policies = new IDomainPolicy<StubContext>[]
        {
            new DisabledPolicy(), new EnabledPolicy()
        };

        // Act
        var result = await DomainPolicies.ApplyAsync(context, policies);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Messages.Count.ShouldBe(1);
        result.Messages.ShouldContain("Always satisfied policy applied");
        result.PolicyResults.GetValue<EnabledPolicy, int>().ShouldBe(1);
        result.PolicyResults.GetValue<DisabledPolicy, int>().ShouldBe(default);
    }

    [Fact]
    public async Task ApplyAsync_WithNoPolicies_ShouldSucceed()
    {
        // Arrange
        var context = new StubContext { Value = 1 };
        var policies = Array.Empty<IDomainPolicy<StubContext>>();

        // Act
        var result = await DomainPolicies.ApplyAsync(context, policies);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Messages.Count.ShouldBe(0);
        context.Value.ShouldBe(1); // Original value
    }

    [Fact]
    public async Task ApplyAsync_WithNullContext_ShouldThrowArgumentNullException()
    {
        // Arrange
        StubContext context = null;
        var policies = new IDomainPolicy<StubContext>[]
        {
            new EnabledPolicy(), new ModifyContextPolicy(), new ConditionalEnabledPolicy()
        };

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(async () =>
        {
            await DomainPolicies.ApplyAsync(context, policies);
        });
    }

    [Fact]
    public async Task ApplyAsync_WithNullPolicies_ShouldSucceed()
    {
        // Arrange
        var context = new StubContext { Value = 1 };
        IDomainPolicy<StubContext>[] policies = null;

        // Act
        var result = await DomainPolicies.ApplyAsync(context, policies);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Messages.Count.ShouldBe(0);
        context.Value.ShouldBe(1); // Original value
    }
}