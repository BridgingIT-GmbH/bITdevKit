// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests;

public class EmptyToNullTests
{
    private readonly Faker faker = new();

    [Fact]
    public void EmptyToNull_NullEnumerable_ReturnsNull()
    {
        // Arrange
        IEnumerable<string> source = null;

        // Act
        var result = source.EmptyToNull();

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void EmptyToNull_EmptyEnumerable_ReturnsNull()
    {
        // Arrange
        var source = new List<string>();

        // Act
        var result = source.EmptyToNull();

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void EmptyToNull_NonEmptyEnumerable_ReturnsSameEnumerable()
    {
        // Arrange
        var source = this.faker.Make(3, () => this.faker.Lorem.Word());

        // Act
        var result = source.EmptyToNull();

        // Assert
        result.ShouldBe(source);
    }

    [Fact]
    public void EmptyToNull_NullString_ReturnsNull()
    {
        // Arrange
        string source = null;

        // Act
        var result = source.EmptyToNull();

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void EmptyToNull_EmptyString_ReturnsNull()
    {
        // Arrange
        var source = string.Empty;

        // Act
        var result = source.EmptyToNull();

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void EmptyToNull_NonEmptyString_ReturnsSameString()
    {
        // Arrange
        var source = this.faker.Lorem.Word();

        // Act
        var result = source.EmptyToNull();

        // Assert
        result.ShouldBe(source);
    }

    [Fact]
    public void EmptyToNull_WhitespaceString_ReturnsSameString()
    {
        // Arrange
        var source = "   ";

        // Act
        var result = source.EmptyToNull();

        // Assert
        result.ShouldBe(source);
    }
}