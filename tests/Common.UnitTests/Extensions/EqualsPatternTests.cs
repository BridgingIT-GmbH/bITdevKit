// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Extensions;

[UnitTest("Common")]
public class EqualsPatternTests
{
    [Fact]
    public void EqualsPattern_Misc_ReturnsExpected()
    {
        Assert.True("Test".EqualsPattern("*"));
        Assert.True("Test".EqualsPattern("Test"));
        Assert.False("Test1".EqualsPattern("Test"));
        Assert.False("Test".EqualsPattern("test", false));
        Assert.True("Test".EqualsPattern("Test", false));
        Assert.True("1234Test".EqualsPattern("*Test"));
        Assert.False("Test123".EqualsPattern("test*", false));
        Assert.True("Test234".EqualsPattern("Test*", false));
        Assert.True("Test1".EqualsPattern("Test*"));
        Assert.False("1Test1".EqualsPattern("Test*"));
        Assert.True("1Test1".EqualsPattern("*Test*"));
        Assert.True("³[]³}{]}{]}³1Test1³²[²³{[]²³$&%/$&%".EqualsPattern("*Test*"));
        Assert.True("Te\\asd[\\w]st".EqualsPattern("Te\\asd[\\w]st"));
    }

    [Fact]
    public void EqualsPattern_SourceIsNullAndPatternIsNull_ReturnsTrue()
    {
        // Arrange
        const string source = null;
        const string pattern = null;

        // Act
        var result = source.EqualsPattern(pattern);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void EqualsPattern_SourceIsNullAndPatternIsNotNull_ReturnsFalse()
    {
        // Arrange
        const string source = null;
        const string pattern = "abc";

        // Act
        var result = source.EqualsPattern(pattern);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void EqualsPattern_SourceIsNotNullAndPatternIsNull_ReturnsFalse()
    {
        // Arrange
        const string source = "abc";
        const string pattern = null;

        // Act
        var result = source.EqualsPattern(pattern);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void EqualsPattern_PatternMatches_ReturnsTrue()
    {
        // Arrange
        const string source = "abcdef";
        const string pattern = "abc*def";

        // Act
        var result = source.EqualsPattern(pattern);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void EqualsPattern_PatternDoesNotMatch_ReturnsFalse()
    {
        // Arrange
        const string source = "abcdef";
        const string pattern = "xyz*";

        // Act
        var result = source.EqualsPattern(pattern);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void EqualsPattern_PatternWithMultipleWildcards_MatchesCorrectly()
    {
        // Arrange
        const string source = "abcdef123";
        const string pattern = "abc*ef*123";

        // Act
        var result = source.EqualsPattern(pattern);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void EqualsPattern_PatternWithLeadingWildcards_MatchesCorrectly()
    {
        // Arrange
        const string source = "abcdef123";
        const string pattern = "*ef123";

        // Act
        var result = source.EqualsPattern(pattern);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void EqualsPattern_PatternWithTrailingWildcards_MatchesCorrectly()
    {
        // Arrange
        const string source = "abcdef123";
        const string pattern = "abc*ef*";

        // Act
        var result = source.EqualsPattern(pattern);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void EqualsPattern_PatternWithWildcardOnly_MatchesAnySource()
    {
        // Arrange
        const string source = "abcdef123";
        const string pattern = "*";

        // Act
        var result = source.EqualsPattern(pattern);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void EqualsPatternAny_SourceIsNullAndPatternsIsNull_ReturnsTrue()
    {
        // Arrange
        const string source = null;
        IEnumerable<string> patterns = null;

        // Act
        var result = source.EqualsPatternAny(patterns);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void EqualsPatternAny_PatternsIsNull_ReturnsFalse()
    {
        // Arrange
        const string source = "abc";
        IEnumerable<string> patterns = null;

        // Act
        var result = source.EqualsPatternAny(patterns);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void EqualsPatternAny_NoMatchingPatterns_ReturnsFalse()
    {
        // Arrange
        const string source = "abc";
        IEnumerable<string> patterns = new List<string> { "xyz*", "def*" };

        // Act
        var result = source.EqualsPatternAny(patterns);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void EqualsPatternAny_MatchingPatternExists_ReturnsTrue()
    {
        // Arrange
        const string source = "abc";
        IEnumerable<string> patterns = new List<string> { "xyz*", "abc*" };

        // Act
        var result = source.EqualsPatternAny(patterns);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void EqualsPattern_PatternWithWildcardAtBeginning_MatchesCorrectly()
    {
        // Arrange
        const string source = "abcdef";
        const string pattern = "*def";

        // Act
        var result = source.EqualsPattern(pattern);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void EqualsPattern_PatternWithWildcardAtEnd_MatchesCorrectly()
    {
        // Arrange
        const string source = "abcdef";
        const string pattern = "abc*";

        // Act
        var result = source.EqualsPattern(pattern);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void EqualsPattern_PatternWithWildcardsAtBothEnds_MatchesCorrectly()
    {
        // Arrange
        const string source = "abcdef";
        const string pattern = "*bc*ef*";

        // Act
        var result = source.EqualsPattern(pattern);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void EqualsPattern_PatternWithWildcardOnlyAtBeginning_DoesNotMatch()
    {
        // Arrange
        const string source = "abcdef";
        const string pattern = "*abc";

        // Act
        var result = source.EqualsPattern(pattern);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void EqualsPattern_PatternWithWildcardOnlyAtEnd_DoesNotMatch()
    {
        // Arrange
        const string source = "abcdef";
        const string pattern = "def*";

        // Act
        var result = source.EqualsPattern(pattern);

        // Assert
        result.ShouldBeFalse();
    }
}