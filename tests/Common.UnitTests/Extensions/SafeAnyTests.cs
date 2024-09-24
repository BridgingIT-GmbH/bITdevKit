// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests;

[UnitTest("Common")]
public class SafeAnyTests
{
    [Fact]
    public void SafeAny_ReturnsFalse_GivenNullEnumerable()
    {
        // Arrange
        IEnumerable<int> enumerable = null;

        // Act
        var result = enumerable.SafeAny();

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void SafeAny_ReturnsFalse_GivenEmptyEnumerable()
    {
        // Arrange
        var enumerable = Enumerable.Empty<int>();

        // Act
        var result = enumerable.SafeAny();

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void SafeAny_ReturnsFalse_GivenEnumerableWithNullItems()
    {
        // Arrange
        var enumerable = new int?[] { null };

        // Act
        var result = enumerable.SafeAny();

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void SafeAny_ReturnsTrue_GivenEnumerableWithSomeNullItems()
    {
        // Arrange
        var enumerable = new int?[] { 1, null, 3 };

        // Act
        var result = enumerable.SafeAny();

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void SafeAny_ReturnsTrue_GivenEnumerableWithNonNull()
    {
        // Arrange
        var enumerable = new int?[] { 1 };

        // Act
        var result = enumerable.SafeAny();

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void SafeAny_ReturnsTrue_GivenPredicateSatisfied()
    {
        // Arrange
        var enumerable = new[] { 1, 2, 3, 4, 5 };

        // Act
        var result = enumerable.SafeAny(i => i % 2 == 0);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void SafeAny_ReturnsFalse_GivenPredicateNotSatisfied()
    {
        // Arrange
        var enumerable = new[] { 1, 3, 5 };

        // Act
        var result = enumerable.SafeAny(i => i % 2 == 0);

        // Assert
        result.ShouldBeFalse();
    }
}