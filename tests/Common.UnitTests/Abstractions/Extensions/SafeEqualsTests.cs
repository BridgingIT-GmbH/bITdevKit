// ReSharper disable ExpressionIsAlwaysNull

namespace BridgingIT.DevKit.Common.UnitTests.Abstractions.Extensions;

using System;
using Bogus;
using Shouldly;
using Xunit;

public class SafeEqualsTests
{
    private readonly Faker faker = new();

    [Fact]
    public void SafeEquals_BothStringsNull_ReturnsTrue()
    {
        // Arrange
        const string source = null;
        const string value = null;

        // Act
        var result = source.SafeEquals(value);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void SafeEquals_SourceNullValueNotNull_ReturnsFalse()
    {
        // Arrange
        const string source = null;
        var value = this.faker.Lorem.Word();

        // Act
        var result = source.SafeEquals(value);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void SafeEquals_SourceNotNullValueNull_ReturnsFalse()
    {
        // Arrange
        var source = this.faker.Lorem.Word();
        const string value = null;

        // Act
        var result = source.SafeEquals(value);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void SafeEquals_EqualStrings_ReturnsTrue()
    {
        // Arrange
        var source = this.faker.Lorem.Word();
        var value = source;

        // Act
        var result = source.SafeEquals(value);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void SafeEquals_DifferentStrings_ReturnsFalse()
    {
        // Arrange
        var source = this.faker.Lorem.Word();
        var value = this.faker.Lorem.Word();

        // Act
        var result = source.SafeEquals(value);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void SafeEquals_CaseInsensitiveComparison_ReturnsTrue()
    {
        // Arrange
        var source = this.faker.Lorem.Word().ToLower();
        var value = source.ToUpper();

        // Act
        var result = source.SafeEquals(value);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void SafeEquals_CaseSensitiveComparison_ReturnsFalse()
    {
        // Arrange
        var source = this.faker.Lorem.Word().ToLower();
        var value = source.ToUpper();
        const StringComparison comparisonType = StringComparison.Ordinal;

        // Act
        var result = source.SafeEquals(value, comparisonType);

        // Assert
        result.ShouldBeFalse();
    }
}