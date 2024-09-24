// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Domain.Model;

using DevKit.Domain.Model;

[UnitTest("Domain")]
public class ComparableValueObjectTests
{
    private readonly Faker faker = new();

    [Fact]
    public void LessThan_LeftIsNull_ReturnsTrue()
    {
        // Arrange
        ComparableValueObject left = null;
        var right = Substitute.For<ComparableValueObject>();

        // Act
        var result = left < right;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void LessThan_LeftIsLessThanRight_ReturnsTrue()
    {
        // Arrange
        var left = new TestComparableValueObject(1);
        var right = new TestComparableValueObject(2);

        // Act
        var result = left < right;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void LessThanOrEqual_LeftIsNull_ReturnsTrue()
    {
        // Arrange
        ComparableValueObject left = null;
        var right = Substitute.For<ComparableValueObject>();

        // Act
        var result = left <= right;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void LessThanOrEqual_LeftIsEqualToRight_ReturnsTrue()
    {
        // Arrange
        var value = this.faker.Random.Int();
        var left = new TestComparableValueObject(value);
        var right = new TestComparableValueObject(value);

        // Act
        var result = left <= right;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void GreaterThan_LeftIsGreaterThanRight_ReturnsTrue()
    {
        // Arrange
        var left = new TestComparableValueObject(2);
        var right = new TestComparableValueObject(1);

        // Act
        var result = left > right;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void GreaterThan_LeftIsNull_ReturnsFalse()
    {
        // Arrange
        ComparableValueObject left = null;
        var right = Substitute.For<ComparableValueObject>();

        // Act
        var result = left > right;

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void GreaterThanOrEqual_LeftIsGreaterThanRight_ReturnsTrue()
    {
        // Arrange
        var left = new TestComparableValueObject(2);
        var right = new TestComparableValueObject(1);

        // Act
        var result = left >= right;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void GreaterThanOrEqual_BothAreNull_ReturnsTrue()
    {
        // Arrange
        ComparableValueObject left = null;
        ComparableValueObject right = null;

        // Act
        var result = left >= right;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void CompareTo_SameReference_ReturnsZero()
    {
        // Arrange
        var sut = new TestComparableValueObject(1);
        object other = sut;

        // Act
        var result = sut.CompareTo(other);

        // Assert
        result.ShouldBe(0);
    }

    [Fact]
    public void CompareTo_OtherIsNull_ReturnsOne()
    {
        // Arrange
        var sut = new TestComparableValueObject(1);

        // Act
        var result = sut.CompareTo(null);

        // Assert
        result.ShouldBe(1);
    }

    [Fact]
    public void CompareTo_DifferentTypes_ThrowsInvalidOperationException()
    {
        // Arrange
        var sut = new TestComparableValueObject(1);
        var other = new object();

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => sut.CompareTo(other));
    }

    [Fact]
    public void CompareTo_SameType_ReturnsExpectedComparison()
    {
        // Arrange
        var sut = new TestComparableValueObject(2);
        var other = new TestComparableValueObject(1);

        // Act
        var result = sut.CompareTo(other);

        // Assert
        result.ShouldBeGreaterThan(0);
    }

    private class TestComparableValueObject(int value) : ComparableValueObject
    {
        protected override IEnumerable<IComparable> GetComparableAtomicValues()
        {
            yield return value;
        }

        protected override IEnumerable<object> GetAtomicValues()
        {
            yield return value;
        }
    }
}