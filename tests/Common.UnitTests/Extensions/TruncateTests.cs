// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Extensions;

[UnitTest("Common")]
public class TruncateTests
{
    [Fact]
    public void Truncate_Left()
    {
        Assert.Equal(string.Empty, string.Empty.TruncateLeft(10));
        Assert.Equal("World!", "Hello-World!".TruncateLeft(6));
        Assert.Equal(string.Empty, "Hello-World!".TruncateLeft(0));
        Assert.Equal(string.Empty, "Hello-World!".TruncateLeft(-6));
        Assert.Equal("Hello-World!", "Hello-World!".TruncateLeft(100));
    }

    [Fact]
    public void Truncate_Right()
    {
        Assert.Equal(string.Empty, string.Empty.TruncateRight(10));
        Assert.Equal("Hello-", "Hello-World!".TruncateRight(6));
        Assert.Equal(string.Empty, "Hello-World!".TruncateRight(0));
        Assert.Equal(string.Empty, "Hello-World!".TruncateRight(-6));
        Assert.Equal("Hello-World!", "Hello-World!".TruncateRight(100));
    }

    [Fact]
    public void TruncateLeft_SourceIsNull_ReturnsNull()
    {
        // Arrange
        const string source = null;
        const int length = 5;

        // Act
        var result = source.TruncateLeft(length);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void TruncateLeft_LengthIsNegative_ReturnsEmpty()
    {
        // Arrange
        const string source = "Hello, World!";
        const int length = -5;

        // Act
        var result = source.TruncateLeft(length);

        // Assert
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void TruncateLeft_SourceLengthIsLessThanLength_ReturnsSource()
    {
        // Arrange
        const string source = "Hello";
        const int length = 10;

        // Act
        var result = source.TruncateLeft(length);

        // Assert
        result.ShouldBe(source);
    }

    [Fact]
    public void TruncateLeft_SourceLengthIsGreaterThanLength_ReturnsTruncatedSource()
    {
        // Arrange
        const string source = "Hello, World!";
        const int length = 5;

        // Act
        var result = source.TruncateLeft(length);

        // Assert
        result.ShouldBe("orld!");
    }

    [Fact]
    public void TruncateRight_SourceIsNull_ReturnsNull()
    {
        // Arrange
        const string source = null;
        const int length = 5;

        // Act
        var result = source.TruncateRight(length);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void TruncateRight_LengthIsNegative_ReturnsEmpty()
    {
        // Arrange
        const string source = "Hello, World!";
        const int length = -5;

        // Act
        var result = source.TruncateRight(length);

        // Assert
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void TruncateRight_SourceLengthIsLessThanLength_ReturnsSource()
    {
        // Arrange
        const string source = "Hello";
        const int length = 10;

        // Act
        var result = source.TruncateRight(length);

        // Assert
        result.ShouldBe(source);
    }

    [Fact]
    public void TruncateRight_SourceLengthIsGreaterThanLength_ReturnsTruncatedSource()
    {
        // Arrange
        const string source = "Hello, World!";
        const int length = 5;

        // Act
        var result = source.TruncateRight(length);

        // Assert
        result.ShouldBe("Hello");
    }
}