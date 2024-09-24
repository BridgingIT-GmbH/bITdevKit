// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests;

[UnitTest("Common")]
public class ContainsAnyTests
{
    [Fact]
    public void ContainsAny_ReturnsFalse_GivenNullOrEmptySourceString()
    {
        // Arrange
        string source = null;
        var items = new[] { "abc", "def", "ghi" };

        // Act
        var result = source.ContainsAny(items);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void ContainsAny_ReturnsFalse_GivenNullOrEmptyItemsArray()
    {
        // Arrange
        var source = "Hello world";
        string[] items = null;

        // Act
        var result = source.ContainsAny(items);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void ContainsAny_ReturnsFalse_GivenItemsNotFoundInSourceString()
    {
        // Arrange
        var source = "The quick brown fox jumps over the lazy dog";
        var items = new[] { "cat", "horse" };

        // Act
        var result = source.ContainsAny(items);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void ContainsAny_ReturnsTrue_GivenSingleItemFoundInSourceString()
    {
        // Arrange
        var source = "The quick brown fox jumps over the lazy dog";
        var items = new[] { "fox" };

        // Act
        var result = source.ContainsAny(items);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ContainsAny_ReturnsTrue_GivenMultipleItemsFoundInSourceString()
    {
        // Arrange
        var source = "The quick brown fox jumps over the lazy dog";
        var items = new[] { "fox", "dog" };

        // Act
        var result = source.ContainsAny(items);

        // Assert
        result.ShouldBeTrue();
    }
}