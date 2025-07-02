// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Extensions;

[UnitTest("Common")]
public class MatchPatternTests
{
    [Fact]
    public void Match_Misc_ReturnsExpected()
    {
        Assert.True("Test".Match("*"));
        Assert.True("test_0.log".Match("*.log"));
        Assert.True("test_0.log".Match("*.*"));
        Assert.True("Test".Match("Test"));
        Assert.False("Test1".Match("Test"));
        Assert.False("Test".Match("test", false));
        Assert.True("Test".Match("Test", false));
        Assert.True("1234Test".Match("*Test"));
        Assert.False("Test123".Match("test*", false));
        Assert.True("Test234".Match("Test*", false));
        Assert.True("Test1".Match("Test*"));
        Assert.False("1Test1".Match("Test*"));
        Assert.True("1Test1".Match("*Test*"));
        Assert.True("³[]³}{]}{]}³1Test1³²[²³{[]²³$&%/$&%".Match("*Test*"));
        Assert.True("Te\\asd[\\w]st".Match("Te\\asd[\\w]st"));
    }

    [Fact]
    public void MatchAny_Misc_ReturnsExpected()
    {
        Assert.True("Test".MatchAny(["*"]));
        Assert.True("test_0.log".MatchAny(["*.log"]));
        Assert.True("test_0.log".MatchAny(["*.*"]));
        Assert.True("Test".MatchAny(["Test"]));
        Assert.False("Test1".MatchAny(["Test"]));
        Assert.False("Test".MatchAny(["test"], false));
        Assert.True("Test".MatchAny(["Test"], false));
        Assert.True("1234Test".MatchAny(["*Test"]));
        Assert.False("Test123".MatchAny(["test*"], false));
        Assert.True("Test234".MatchAny(["Test*"], false));
        Assert.True("Test1".MatchAny(["Test*"]));
        Assert.False("1Test1".MatchAny(["Test*"]));
        Assert.True("1Test1".MatchAny(["*Test*"]));
        Assert.True("³[]³}{]}{]}³1Test1³²[²³{[]²³$&%/$&%".MatchAny(["*Test*"]));
        Assert.True("Te\\asd[\\w]st".MatchAny(["Te\\asd[\\w]st"]));
    }

    [Fact]
    public void Match_SourceIsNullAndPatternIsNull_ReturnsTrue()
    {
        // Arrange
        const string source = null;
        const string pattern = null;

        // Act
        var result = source.Match(pattern);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Match_SourceIsNullAndPatternIsNotNull_ReturnsFalse()
    {
        // Arrange
        const string source = null;
        const string pattern = "abc";

        // Act
        var result = source.Match(pattern);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Match_SourceIsNotNullAndPatternIsNull_ReturnsFalse()
    {
        // Arrange
        const string source = "abc";
        const string pattern = null;

        // Act
        var result = source.Match(pattern);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Match_PatternMatches_ReturnsTrue()
    {
        // Arrange
        const string source = "abcdef";
        const string pattern = "abc*def";

        // Act
        var result = source.Match(pattern);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Match_PatternDoesNotMatch_ReturnsFalse()
    {
        // Arrange
        const string source = "abcdef";
        const string pattern = "xyz*";

        // Act
        var result = source.Match(pattern);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Match_PatternWithMultipleWildcards_MatchesCorrectly()
    {
        // Arrange
        const string source = "abcdef123";
        const string pattern = "abc*ef*123";

        // Act
        var result = source.Match(pattern);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Match_PatternWithLeadingWildcards_MatchesCorrectly()
    {
        // Arrange
        const string source = "abcdef123";
        const string pattern = "*ef123";

        // Act
        var result = source.Match(pattern);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Match_PatternWithTrailingWildcards_MatchesCorrectly()
    {
        // Arrange
        const string source = "abcdef123";
        const string pattern = "abc*ef*";

        // Act
        var result = source.Match(pattern);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Match_PatternWithWildcardOnly_MatchesAnySource()
    {
        // Arrange
        const string source = "abcdef123";
        const string pattern = "*";

        // Act
        var result = source.Match(pattern);

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
        var result = source.MatchAny(patterns);

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
        var result = source.MatchAny(patterns);

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
        var result = source.MatchAny(patterns);

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
        var result = source.MatchAny(patterns);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Match_PatternWithWildcardAtBeginning_MatchesCorrectly()
    {
        // Arrange
        const string source = "abcdef";
        const string pattern = "*def";

        // Act
        var result = source.Match(pattern);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Match_PatternWithWildcardAtEnd_MatchesCorrectly()
    {
        // Arrange
        const string source = "abcdef";
        const string pattern = "abc*";

        // Act
        var result = source.Match(pattern);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Match_PatternWithWildcardsAtBothEnds_MatchesCorrectly()
    {
        // Arrange
        const string source = "abcdef";
        const string pattern = "*bc*ef*";

        // Act
        var result = source.Match(pattern);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Match_PatternWithWildcardOnlyAtBeginning_DoesNotMatch()
    {
        // Arrange
        const string source = "abcdef";
        const string pattern = "*abc";

        // Act
        var result = source.Match(pattern);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Match_PatternWithWildcardOnlyAtEnd_DoesNotMatch()
    {
        // Arrange
        const string source = "abcdef";
        const string pattern = "def*";

        // Act
        var result = source.Match(pattern);

        // Assert
        result.ShouldBeFalse();
    }
}