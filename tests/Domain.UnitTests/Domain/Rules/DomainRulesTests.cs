// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Domain.Rules;

[UnitTest("Domain")]
public class DomainRulesTests
{
    [Fact]
    public async Task ReturnAsync_SatisfiedDomainRule()
    {
        // Arrange
        var rule = new StubDomainRule(true);

        // Act
        var result = await DomainRules.ReturnAsync(rule);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Return_SatisfiedDomainRule()
    {
        // Arrange
        var rule = new StubDomainRule(true);

        // Act
        var result = DomainRules.Return(rule);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ReturnAsync_ManySatisfiedDomainRule()
    {
        // Arrange
        var rule1 = new StubDomainRule(true);
        var rule2 = new StubDomainRule(true);
        var rule3 = new StubDomainRule(true);

        // Act
        var result = await DomainRules.ReturnAsync(new[] { rule1, rule2, rule3 });

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Return_ManySatisfiedDomainRule()
    {
        // Arrange
        var rule1 = new StubDomainRule(true);
        var rule2 = new StubDomainRule(true);
        var rule3 = new StubDomainRule(true);

        // Act
        var result = DomainRules.Return(new[] { rule1, rule2, rule3 });

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ReturnAsync_NotSatisfiedDomainRule()
    {
        // Arrange
        var rule = new StubDomainRule(false);

        // Act
        var result = await DomainRules.ReturnAsync(rule);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Return_NotSatisfiedDomainRule()
    {
        // Arrange
        var rule = new StubDomainRule(false);

        // Act
        var result = DomainRules.Return(rule);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void ApplyAsync_ThrowNotSatisfiedDomainRule()
    {
        // Arrange
        var rule = new StubDomainRule(false);

        // Act & Assert
        Should.Throw<DomainRuleException>(async () => await DomainRules.ApplyAsync(rule));
    }

    [Fact]
    public void ApplyAsync_ThrowExceptionDueToFail()
    {
        // Arrange
        var rule = new StubDomainRule(true, true);

        // Act & Assert
        Should.Throw<ApplicationException>(async () => await DomainRules.ApplyAsync(rule));
    }

    [Fact]
    public void Apply_ThrowNotSatisfiedDomainRule()
    {
        // Arrange
        var rule = new StubDomainRule(false);

        // Act & Assert
        Should.Throw<DomainRuleException>(() => DomainRules.Apply(rule));
    }

    [Fact]
    public void Apply_ThrowExceptionDueToFail()
    {
        // Arrange
        var rule = new StubDomainRule(true, true);

        // Act & Assert
        Should.Throw<ApplicationException>(() => DomainRules.Apply(rule));
    }

    [Fact]
    public void ApplyAsync_ThrowManySatisfiedDomainRule()
    {
        // Arrange
        var rule1 = new StubDomainRule(true);
        var rule2 = new StubDomainRule(true);
        var rule3 = new StubDomainRule(true);

        // Act & Assert
        Task.Run(async () => await DomainRules.ApplyAsync(new[] { rule1, rule2, rule3 }));
    }

    [Fact]
    public async Task ApplyAsync_ThrowManySatisfiedDomainRuleApplyAction()
    {
        // Arrange
        var rule1 = new StubDomainRule(true);
        var rule2 = new StubDomainRule(true);
        var rule3 = new StubDomainRule(true);

        // Act
        var result = await DomainRules.ApplyAsync(new[] { rule1, rule2, rule3 }, null, () => 1);

        // Assert
        result.ShouldBe(1);
    }

    [Fact]
    public void Apply_ThrowManySatisfiedDomainRule()
    {
        // Arrange
        var rule1 = new StubDomainRule(true);
        var rule2 = new StubDomainRule(true);
        var rule3 = new StubDomainRule(true);

        // Act & Assert
        DomainRules.Apply(new[] { rule1, rule2, rule3 });
    }

    [Fact]
    public void Apply_ThrowManySatisfiedDomainRuleApplyAction()
    {
        // Arrange
        var rule1 = new StubDomainRule(true);
        var rule2 = new StubDomainRule(true);
        var rule3 = new StubDomainRule(true);

        // Act
        var result = DomainRules.Apply(new[] { rule1, rule2, rule3 }, null, () => 1);

        // Assert
        // Assert
        result.ShouldBe(1);
    }

    [Fact]
    public void ApplyAsync_ThrowManyNotSatisfiedDomainRule()
    {
        // Arrange
        var rule1 = new StubDomainRule(true);
        var rule2 = new StubDomainRule(false);
        var rule3 = new StubDomainRule(true);

        // Act & Assert
        Should.Throw<DomainRuleException>(async () => await DomainRules.ApplyAsync(new[] { rule1, rule2, rule3 }));
    }

    [Fact]
    public void ApplyAsync_ThrowManyExceptionDueToFail()
    {
        // Arrange
        var rule1 = new StubDomainRule(true);
        var rule2 = new StubDomainRule(true, true);
        var rule3 = new StubDomainRule(true, true);

        // Act & Assert
        Should.Throw<ApplicationException>(async () => await DomainRules.ApplyAsync(new[] { rule1, rule2, rule3 }));
    }

    [Fact]
    public void Apply_ThrowManyNotSatisfiedDomainRule()
    {
        // Arrange
        var rule1 = new StubDomainRule(true);
        var rule2 = new StubDomainRule(false);
        var rule3 = new StubDomainRule(true);

        // Act & Assert
        Should.Throw<DomainRuleException>(() => DomainRules.Apply(new[] { rule1, rule2, rule3 }));
    }

    [Fact]
    public void Apply_ThrowManyExceptionDueToFail()
    {
        // Arrange
        var rule1 = new StubDomainRule(true);
        var rule2 = new StubDomainRule(true, true);
        var rule3 = new StubDomainRule(true, true);

        // Act & Assert
        Should.Throw<ApplicationException>(() => DomainRules.Apply(new[] { rule1, rule2, rule3 }));
    }

    [Fact]
    public async Task ReturnAsync_ManyNotSatisfiedDomainRule()
    {
        // Arrange
        var rule1 = new StubDomainRule(true);
        var rule2 = new StubDomainRule(false);
        var rule3 = new StubDomainRule(true);

        // Act
        var result = await DomainRules.ReturnAsync(new[] { rule1, rule2, rule3 });

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Return_ManyNotSatisfiedDomainRule()
    {
        // Arrange
        var rule1 = new StubDomainRule(true);
        var rule2 = new StubDomainRule(false);
        var rule3 = new StubDomainRule(true);

        // Act
        var result = DomainRules.Return(new[] { rule1, rule2, rule3 });

        // Assert
        result.ShouldBeFalse();
    }
}

public class StubDomainRule(bool isSatisfied, bool fail = false) : IDomainRule
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