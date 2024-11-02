// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Domain.Rules;

using Rules = BridgingIT.DevKit.Domain.Rules;

[UnitTest("Domain")]
public class RulesTests
{
    [Fact]
    public async Task ReturnAsync_SatisfiedDomainRule()
    {
        // Arrange
        var rule = new StubRule(true);

        // Act
        var result = await Rules.ReturnAsync(rule);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Return_SatisfiedDomainRule()
    {
        // Arrange
        var rule = new StubRule(true);

        // Act
        var result = Rules.Return(rule);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ReturnAsync_ManySatisfiedDomainRule()
    {
        // Arrange
        var rule1 = new StubRule(true);
        var rule2 = new StubRule(true);
        var rule3 = new StubRule(true);

        // Act
        var result = await Rules.ReturnAsync([rule1, rule2, rule3]);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Return_ManySatisfiedDomainRule()
    {
        // Arrange
        var rule1 = new StubRule(true);
        var rule2 = new StubRule(true);
        var rule3 = new StubRule(true);

        // Act
        var result = Rules.Return([rule1, rule2, rule3]);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ReturnAsync_NotSatisfiedDomainRule()
    {
        // Arrange
        var rule = new StubRule(false);

        // Act
        var result = await Rules.ReturnAsync(rule);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Return_NotSatisfiedDomainRule()
    {
        // Arrange
        var rule = new StubRule(false);

        // Act
        var result = Rules.Return(rule);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void ApplyAsync_ThrowNotSatisfiedDomainRule()
    {
        // Arrange
        var rule = new StubRule(false);

        // Act & Assert
        Should.Throw<RuleException>(async () => await Rules.ApplyAsync(rule));
    }

    [Fact]
    public void ApplyAsync_ThrowExceptionDueToFail()
    {
        // Arrange
        var rule = new StubRule(true, true);

        // Act & Assert
        Should.Throw<ApplicationException>(async () => await Rules.ApplyAsync(rule));
    }

    [Fact]
    public void Apply_ThrowNotSatisfiedDomainRule()
    {
        // Arrange
        var rule = new StubRule(false);

        // Act & Assert
        Should.Throw<RuleException>(() => Rules.Apply(rule));
    }

    [Fact]
    public void Apply_ThrowExceptionDueToFail()
    {
        // Arrange
        var rule = new StubRule(true, true);

        // Act & Assert
        Should.Throw<ApplicationException>(() => Rules.Apply(rule));
    }

    [Fact]
    public void ApplyAsync_ThrowManySatisfiedDomainRule()
    {
        // Arrange
        var rule1 = new StubRule(true);
        var rule2 = new StubRule(true);
        var rule3 = new StubRule(true);

        // Act & Assert
        Task.Run(async () => await Rules.ApplyAsync([rule1, rule2, rule3]));
    }

    [Fact]
    public async Task ApplyAsync_ThrowManySatisfiedDomainRuleApplyAction()
    {
        // Arrange
        var rule1 = new StubRule(true);
        var rule2 = new StubRule(true);
        var rule3 = new StubRule(true);

        // Act
        var result = await Rules.ApplyAsync([rule1, rule2, rule3], null, () => 1);

        // Assert
        result.ShouldBe(1);
    }

    [Fact]
    public void Apply_ThrowManySatisfiedDomainRule()
    {
        // Arrange
        var rule1 = new StubRule(true);
        var rule2 = new StubRule(true);
        var rule3 = new StubRule(true);

        // Act & Assert
        Rules.Apply([rule1, rule2, rule3]);
    }

    [Fact]
    public void Apply_ThrowManySatisfiedDomainRuleApplyAction()
    {
        // Arrange
        var rule1 = new StubRule(true);
        var rule2 = new StubRule(true);
        var rule3 = new StubRule(true);

        // Act
        var result = Rules.Apply([rule1, rule2, rule3], null, () => 1);

        // Assert
        // Assert
        result.ShouldBe(1);
    }

    [Fact]
    public void ApplyAsync_ThrowManyNotSatisfiedDomainRule()
    {
        // Arrange
        var rule1 = new StubRule(true);
        var rule2 = new StubRule(false);
        var rule3 = new StubRule(true);

        // Act & Assert
        Should.Throw<RuleException>(async () => await Rules.ApplyAsync([rule1, rule2, rule3]));
    }

    [Fact]
    public void ApplyAsync_ThrowManyExceptionDueToFail()
    {
        // Arrange
        var rule1 = new StubRule(true);
        var rule2 = new StubRule(true, true);
        var rule3 = new StubRule(true, true);

        // Act & Assert
        Should.Throw<ApplicationException>(async () => await Rules.ApplyAsync([rule1, rule2, rule3]));
    }

    [Fact]
    public void Apply_ThrowManyNotSatisfiedDomainRule()
    {
        // Arrange
        var rule1 = new StubRule(true);
        var rule2 = new StubRule(false);
        var rule3 = new StubRule(true);

        // Act & Assert
        Should.Throw<RuleException>(() => Rules.Apply([rule1, rule2, rule3]));
    }

    [Fact]
    public void Apply_ThrowManyExceptionDueToFail()
    {
        // Arrange
        var rule1 = new StubRule(true);
        var rule2 = new StubRule(true, true);
        var rule3 = new StubRule(true, true);

        // Act & Assert
        Should.Throw<ApplicationException>(() => Rules.Apply([rule1, rule2, rule3]));
    }

    [Fact]
    public async Task ReturnAsync_ManyNotSatisfiedDomainRule()
    {
        // Arrange
        var rule1 = new StubRule(true);
        var rule2 = new StubRule(false);
        var rule3 = new StubRule(true);

        // Act
        var result = await Rules.ReturnAsync([rule1, rule2, rule3]);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Return_ManyNotSatisfiedDomainRule()
    {
        // Arrange
        var rule1 = new StubRule(true);
        var rule2 = new StubRule(false);
        var rule3 = new StubRule(true);

        // Act
        var result = Rules.Return([rule1, rule2, rule3]);

        // Assert
        result.ShouldBeFalse();
    }
}

public class StubRule(bool isSatisfied, bool fail = false) : IRule
{
    private readonly bool isSatisfied = isSatisfied;
    private readonly bool fail = fail;

    public string Message => "help";

    public Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public async Task<bool> ApplyAsync(CancellationToken cancellationToken = default)
    {
        if (this.fail)
        {
            throw new ApplicationException("failed rule");
        }

        return await Task.FromResult(this.isSatisfied);
    }
}