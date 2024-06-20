// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Domain.Rules;

using Shouldly;
using Xunit;

[UnitTest("Domain")]
public class CheckTests
{
    [Fact]
    public async Task ReturnAsync_SatisfiedBusinessRule()
    {
        // Arrange
        var rule = new StubBusinessRule(true);

        // Act
        var result = await Check.ReturnAsync(rule);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Return_SatisfiedBusinessRule()
    {
        // Arrange
        var rule = new StubBusinessRule(true);

        // Act
        var result = Check.Return(rule);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ReturnAsync_ManySatisfiedBusinessRule()
    {
        // Arrange
        var rule1 = new StubBusinessRule(true);
        var rule2 = new StubBusinessRule(true);
        var rule3 = new StubBusinessRule(true);

        // Act
        var result = await Check.ReturnAsync(new[] { rule1, rule2, rule3 });

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Return_ManySatisfiedBusinessRule()
    {
        // Arrange
        var rule1 = new StubBusinessRule(true);
        var rule2 = new StubBusinessRule(true);
        var rule3 = new StubBusinessRule(true);

        // Act
        var result = Check.Return(new[] { rule1, rule2, rule3 });

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ReturnAsync_NotSatisfiedBusinessRule()
    {
        // Arrange
        var rule = new StubBusinessRule(false);

        // Act
        var result = await Check.ReturnAsync(rule);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Return_NotSatisfiedBusinessRule()
    {
        // Arrange
        var rule = new StubBusinessRule(false);

        // Act
        var result = Check.Return(rule);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void ThrowAsync_ThrowNotSatisfiedBusinessRule()
    {
        // Arrange
        var rule = new StubBusinessRule(false);

        // Act & Assert
        Should.Throw<BusinessRuleNotSatisfiedException>(async () => await Check.ThrowAsync(rule));
    }

    [Fact]
    public void ThrowAsync_ThrowExceptionDueToFail()
    {
        // Arrange
        var rule = new StubBusinessRule(true, true);

        // Act & Assert
        Should.Throw<ApplicationException>(async () => await Check.ThrowAsync(rule));
    }

    [Fact]
    public void Throw_ThrowNotSatisfiedBusinessRule()
    {
        // Arrange
        var rule = new StubBusinessRule(false);

        // Act & Assert
        Should.Throw<BusinessRuleNotSatisfiedException>(() => Check.Throw(rule));
    }

    [Fact]
    public void Throw_ThrowExceptionDueToFail()
    {
        // Arrange
        var rule = new StubBusinessRule(true, true);

        // Act & Assert
        Should.Throw<ApplicationException>(() => Check.Throw(rule));
    }

    [Fact]
    public void ThrowAsync_ThrowManySatisfiedBusinessRule()
    {
        // Arrange
        var rule1 = new StubBusinessRule(true);
        var rule2 = new StubBusinessRule(true);
        var rule3 = new StubBusinessRule(true);

        // Act & Assert
        Task.Run(async () => await Check.ThrowAsync(new[] { rule1, rule2, rule3 }));
    }

    [Fact]
    public async Task ThrowAsync_ThrowManySatisfiedBusinessRuleApplyAction()
    {
        // Arrange
        var rule1 = new StubBusinessRule(true);
        var rule2 = new StubBusinessRule(true);
        var rule3 = new StubBusinessRule(true);

        // Act
        var result = await Check.ThrowAsync(new[] { rule1, rule2, rule3 }, null, () => 1);

        // Assert
        result.ShouldBe(1);
    }

    [Fact]
    public void Throw_ThrowManySatisfiedBusinessRule()
    {
        // Arrange
        var rule1 = new StubBusinessRule(true);
        var rule2 = new StubBusinessRule(true);
        var rule3 = new StubBusinessRule(true);

        // Act & Assert
        Check.Throw(new[] { rule1, rule2, rule3 });
    }

    [Fact]
    public void Throw_ThrowManySatisfiedBusinessRuleApplyAction()
    {
        // Arrange
        var rule1 = new StubBusinessRule(true);
        var rule2 = new StubBusinessRule(true);
        var rule3 = new StubBusinessRule(true);

        // Act
        var result = Check.Throw(new[] { rule1, rule2, rule3 }, null, () => 1);

        // Assert
        // Assert
        result.ShouldBe(1);
    }

    [Fact]
    public void ThrowAsync_ThrowManyNotSatisfiedBusinessRule()
    {
        // Arrange
        var rule1 = new StubBusinessRule(true);
        var rule2 = new StubBusinessRule(false);
        var rule3 = new StubBusinessRule(true);

        // Act & Assert
        Should.Throw<BusinessRuleNotSatisfiedException>(async () => await Check.ThrowAsync(new[] { rule1, rule2, rule3 }));
    }

    [Fact]
    public void ThrowAsync_ThrowManyExceptionDueToFail()
    {
        // Arrange
        var rule1 = new StubBusinessRule(true);
        var rule2 = new StubBusinessRule(true, true);
        var rule3 = new StubBusinessRule(true, true);

        // Act & Assert
        Should.Throw<ApplicationException>(async () => await Check.ThrowAsync(new[] { rule1, rule2, rule3 }));
    }

    [Fact]
    public void Throw_ThrowManyNotSatisfiedBusinessRule()
    {
        // Arrange
        var rule1 = new StubBusinessRule(true);
        var rule2 = new StubBusinessRule(false);
        var rule3 = new StubBusinessRule(true);

        // Act & Assert
        Should.Throw<BusinessRuleNotSatisfiedException>(() => Check.Throw(new[] { rule1, rule2, rule3 }));
    }

    [Fact]
    public void Throw_ThrowManyExceptionDueToFail()
    {
        // Arrange
        var rule1 = new StubBusinessRule(true);
        var rule2 = new StubBusinessRule(true, true);
        var rule3 = new StubBusinessRule(true, true);

        // Act & Assert
        Should.Throw<ApplicationException>(() => Check.Throw(new[] { rule1, rule2, rule3 }));
    }

    [Fact]
    public async Task ReturnAsync_ManyNotSatisfiedBusinessRule()
    {
        // Arrange
        var rule1 = new StubBusinessRule(true);
        var rule2 = new StubBusinessRule(false);
        var rule3 = new StubBusinessRule(true);

        // Act
        var result = await Check.ReturnAsync(new[] { rule1, rule2, rule3 });

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Return_ManyNotSatisfiedBusinessRule()
    {
        // Arrange
        var rule1 = new StubBusinessRule(true);
        var rule2 = new StubBusinessRule(false);
        var rule3 = new StubBusinessRule(true);

        // Act
        var result = Check.Return(new[] { rule1, rule2, rule3 });

        // Assert
        result.ShouldBeFalse();
    }
}

public class StubBusinessRule(bool isSatisfied, bool fail = false) : IBusinessRule
{
    private readonly bool isSatisfied = isSatisfied;
    private readonly bool fail = fail;

    public string Message => "help";

    public async Task<bool> IsSatisfiedAsync(CancellationToken cancellationToken = default)
    {
        if (this.fail)
        {
            throw new ApplicationException("failed rule");
        }

        return await Task.FromResult(this.isSatisfied);
    }
}
