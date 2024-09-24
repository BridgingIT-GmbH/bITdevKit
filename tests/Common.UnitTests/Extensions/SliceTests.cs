// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests;

[UnitTest("Common")]
public class SliceTests
{
    [Theory]
    [InlineData("a", "a", "", "")]
    [InlineData("a", "z", "", "")]
    [InlineData("bbb", ":", "", "")]
    [InlineData("aaa.bbb", "a", "b", "aa.")]
    [InlineData("aaa.bbb", "aa.", "b", "")]
    [InlineData("aaa.bbb", "bbb", "", "")]
    [InlineData("aaa.bbb", "z", "z", "")]
    [InlineData("Dr. John Doe, Jr", ". ", ",", "John Doe")]
    [InlineData("abcdef.jpg", "abcdef", "jpg", ".")]
    [InlineData("abcdef.jpg", ".jpg", "", "")]
    [InlineData("abcdef.jpg", "", ".jpg", "abcdef")]
    [InlineData("abcdef.jpg.jpg", ".", ".", "jpg")]
    public void Slice_All_Positions(string source, string start, string end, string expected)
    {
        source.Slice(start, end)
            .ShouldBe(expected);
    }

    [Fact]
    public void Slice_ReturnsSubstringBetweenStartAndEnd()
    {
        // Arrange
        const string source = "Hello, World!";
        const string start = "Hello";
        const string end = "World";

        // Act
        var result = source.Slice(start, end);

        // Assert
        result.ShouldBe(", ");
    }

    [Fact]
    public void Slice_ReturnsNull_WhenSourceIsNull()
    {
        // Arrange
        const string source = null;
        const string start = "Hello";
        const string end = "World";

        // Act
        var result = source.Slice(start, end);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void Slice_ReturnsEmptyString_WhenStartIsNotFound()
    {
        // Arrange
        const string source = "Hello, World!";
        const string start = "Hi";
        const string end = "World";

        // Act
        var result = source.Slice(start, end);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Slice_ReturnsEmptyString_WhenEndIsNotFound()
    {
        // Arrange
        const string source = "Hello, World!";
        const string start = "Hello, ";
        const string end = "Universe";

        // Act
        var result = source.Slice(start, end);

        // Assert
        result.ShouldBe("World!");
    }

    [Fact]
    public void Slice_WithValidRange_ReturnsSubstring()
    {
        // Arrange
        const string source = "Hello, World!";
        const int start = 7;
        const int end = 12;

        // Act
        var result = source.Slice(start, end);

        // Assert
        result.ShouldBe("World");
    }

    [Fact]
    public void Slice_WithNegativeEnd_ReturnsSubstringFromStartToEnd()
    {
        // Arrange
        const string source = "Hello, World!";
        const int start = 7;
        const int end = -1;

        // Act
        var result = source.Slice(start, end);

        // Assert
        result.ShouldBe("World!");
    }

    [Fact]
    public void Slice_WithEmptySource_ReturnsEmptyString()
    {
        // Arrange
        const string source = "";
        const int start = 0;
        const int end = 5;

        // Act
        var result = source.Slice(start, end);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Slice_WithEndLessThanStart_ReturnsSubstringFromStartToEnd()
    {
        // Arrange
        const string source = "Hello, World!";
        const int start = 7;
        const int end = 3;

        // Act
        var result = source.Slice(start, end);

        // Assert
        result.ShouldBe("World!");
    }
}