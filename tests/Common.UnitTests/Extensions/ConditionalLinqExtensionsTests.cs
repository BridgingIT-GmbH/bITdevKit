// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests;

[UnitTest("Common")]
public class ConditionalLinqExtensionsTests
{
    private readonly List<int> numbers = [1, 2, 3, 4, 5];

    [Fact]
    public void WhereIf_WhenConditionIsTrue_ShouldApplyPredicate()
    {
        // Arrange
        const bool condition = true;

        // Act
        var result = this.numbers.WhereIf(x => x % 2 == 0, condition)
            .ToList();

        // Assert
        result.ShouldBe([2, 4]);
    }

    [Fact]
    public void WhereIf_WhenConditionIsFalse_ShouldReturnOriginalSequence()
    {
        // Arrange
        const bool condition = false;

        // Act
        var result = this.numbers.WhereIf(x => x % 2 == 0, condition)
            .ToList();

        // Assert
        result.ShouldBe(this.numbers);
    }

    [Fact]
    public void WhereIfElse_WhenConditionIsTrue_ShouldApplyIfPredicate()
    {
        // Arrange
        const bool condition = true;

        // Act
        var result = this.numbers.WhereIfElse(x => x % 2 == 0, x => x % 2 != 0, condition)
            .ToList();

        // Assert
        result.ShouldBe([2, 4]);
    }

    [Fact]
    public void WhereIfElse_WhenConditionIsFalse_ShouldApplyElsePredicate()
    {
        // Arrange
        const bool condition = false;

        // Act
        var result = this.numbers.WhereIfElse(x => x % 2 == 0, x => x % 2 != 0, condition)
            .ToList();

        // Assert
        result.ShouldBe([1, 3, 5]);
    }

    [Fact]
    public void SelectIf_WhenConditionIsTrue_ShouldApplySelector()
    {
        // Arrange
        const bool condition = true;

        // Act
        var result = this.numbers.SelectIf(x => x * 2, condition)
            .ToList();

        // Assert
        result.ShouldBe([2, 4, 6, 8, 10]);
    }

    [Fact]
    public void SelectIf_WhenConditionIsFalse_ShouldReturnOriginalSequence()
    {
        // Arrange
        const bool condition = false;

        // Act
        var result = this.numbers.SelectIf(x => x * 2, condition)
            .ToList();

        // Assert
        result.ShouldBe(this.numbers);
    }

    [Fact]
    public void SelectIfElse_WhenConditionIsTrue_ShouldApplyIfSelector()
    {
        // Arrange
        const bool condition = true;

        // Act
        var result = this.numbers.SelectIfElse(x => x * 2, x => x * 3, condition)
            .ToList();

        // Assert
        result.ShouldBe([2, 4, 6, 8, 10]);
    }

    [Fact]
    public void SelectIfElse_WhenConditionIsFalse_ShouldApplyElseSelector()
    {
        // Arrange
        const bool condition = false;

        // Act
        var result = this.numbers.SelectIfElse(x => x * 2, x => x * 3, condition)
            .ToList();

        // Assert
        result.ShouldBe([3, 6, 9, 12, 15]);
    }

    [Fact]
    public void OrderByIf_WhenConditionIsTrue_ShouldApplyOrderBy()
    {
        // Arrange
        const bool condition = true;

        // Act
        var result = this.numbers.OrderByIf(x => -x, condition)
            .ToList();

        // Assert
        result.ShouldBe([5, 4, 3, 2, 1]);
    }

    [Fact]
    public void OrderByIf_WhenConditionIsFalse_ShouldReturnOriginalSequence()
    {
        // Arrange
        const bool condition = false;

        // Act
        var result = this.numbers.OrderByIf(x => -x, condition)
            .ToList();

        // Assert
        result.ShouldBe(this.numbers);
    }

    [Fact]
    public void FirstOrDefaultIf_WhenConditionIsTrue_ShouldApplyPredicate()
    {
        // Arrange
        const bool condition = true;

        // Act
        var result = this.numbers.FirstOrDefaultIf(x => x > 3, condition);

        // Assert
        result.ShouldBe(4);
    }

    [Fact]
    public void FirstOrDefaultIf_WhenConditionIsFalse_ShouldReturnFirstElement()
    {
        // Arrange
        const bool condition = false;

        // Act
        var result = this.numbers.FirstOrDefaultIf(x => x > 3, condition);

        // Assert
        result.ShouldBe(1);
    }

    [Fact]
    public void CountIf_WhenConditionIsTrue_ShouldApplyPredicate()
    {
        // Arrange
        const bool condition = true;

        // Act
        var result = this.numbers.CountIf(x => x % 2 == 0, condition);

        // Assert
        result.ShouldBe(2);
    }

    [Fact]
    public void CountIf_WhenConditionIsFalse_ShouldReturnTotalCount()
    {
        // Arrange
        const bool condition = false;

        // Act
        var result = this.numbers.CountIf(x => x % 2 == 0, condition);

        // Assert
        result.ShouldBe(5);
    }

    [Fact]
    public void SumIf_WhenConditionIsTrue_ShouldApplySelector()
    {
        // Arrange
        const bool condition = true;

        // Act
        var result = this.numbers.SumIf(x => (double)x * 2, condition);

        // Assert
        result.ShouldBe(30);
    }

    [Fact]
    public void SumIf_WhenConditionIsFalse_ShouldReturnZero()
    {
        // Arrange
        const bool condition = false;

        // Act
        var result = this.numbers.SumIf(x => (double)x * 2, condition);

        // Assert
        result.ShouldBe(0);
    }

    [Fact]
    public void DistinctIf_WhenConditionIsTrue_ShouldApplyDistinct()
    {
        // Arrange
        var numbers = new List<int>
        {
            1,
            2,
            2,
            3,
            3,
            4,
            5
        };
        const bool condition = true;

        // Act
        var result = numbers.DistinctIf(condition)
            .ToList();

        // Assert
        result.ShouldBe([1, 2, 3, 4, 5]);
    }

    [Fact]
    public void DistinctIf_WhenConditionIsFalse_ShouldReturnOriginalSequence()
    {
        // Arrange
        var numbers = new List<int>
        {
            1,
            2,
            2,
            3,
            3,
            4,
            5
        };
        const bool condition = false;

        // Act
        var result = numbers.DistinctIf(condition)
            .ToList();

        // Assert
        result.ShouldBe(numbers);
    }

    [Fact]
    public void AnyIf_WhenConditionIsTrue_ShouldApplyPredicate()
    {
        // Arrange
        const bool condition = true;

        // Act
        var result = this.numbers.AnyIf(x => x > 4, condition);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void AnyIf_WhenConditionIsFalse_ShouldReturnTrueIfNotEmpty()
    {
        // Arrange
        const bool condition = false;

        // Act
        var result = this.numbers.AnyIf(x => x > 10, condition);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void AllIf_WhenConditionIsTrue_ShouldApplyPredicate()
    {
        // Arrange
        const bool condition = true;

        // Act
        var result = this.numbers.AllIf(x => x < 6, condition);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void AllIf_WhenConditionIsFalse_ShouldReturnTrue()
    {
        // Arrange
        const bool condition = false;

        // Act
        var result = this.numbers.AllIf(x => x < 0, condition);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ToListIf_WhenConditionIsTrue_ShouldReturnList()
    {
        // Arrange
        const bool condition = true;

        // Act
        var result = this.numbers.ToListIf(condition);

        // Assert
        result.ShouldBeOfType<List<int>>();
        result.ShouldBe(this.numbers);
    }

    [Fact]
    public void ToListIf_WhenConditionIsFalse_ShouldReturnEmptyList()
    {
        // Arrange
        const bool condition = false;

        // Act
        var result = this.numbers.ToListIf(condition);

        // Assert
        result.ShouldBeOfType<List<int>>();
        result.ShouldBeEmpty();
    }

    [Fact]
    public void ToArrayIf_WhenConditionIsTrue_ShouldReturnArray()
    {
        // Arrange
        const bool condition = true;

        // Act
        var result = this.numbers.ToArrayIf(condition);

        // Assert
        result.ShouldBeOfType<int[]>();
        result.ShouldBe([.. this.numbers]);
    }

    [Fact]
    public void ToArrayIf_WhenConditionIsFalse_ShouldReturnEmptyArray()
    {
        // Arrange
        const bool condition = false;

        // Act
        var result = this.numbers.ToArrayIf(condition);

        // Assert
        result.ShouldBeOfType<int[]>();
        result.ShouldBeEmpty();
    }
}