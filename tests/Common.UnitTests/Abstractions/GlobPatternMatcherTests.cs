// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Abstractions;

/// <summary>Checks reusable whole-text glob syntax, scalar boundaries, and independent calls.</summary>
public sealed class GlobPatternMatcherTests
{
    /// <summary>String and span inputs use the same complete-text glob rules.</summary>
    [Theory]
    [InlineData("", "", true)]
    [InlineData("", "x", false)]
    [InlineData("*", "", true)]
    [InlineData("?", "", false)]
    [InlineData("?", "😀", true)]
    [InlineData("??", "😀", false)]
    [InlineData("*", "a\nb", true)]
    [InlineData("report", "report\n", false)]
    [InlineData(@"report:\?*", "REPORT:?42", true)]
    [InlineData(@"a\*", "a*", true)]
    [InlineData(@"a\\*", @"a\b", true)]
    [InlineData("**a**b", "AAAb", true)]
    [InlineData("ab*cd?", "abXXXXcd😀", true)]
    [InlineData("ab*cd?", "abXXXXcd😀x", false)]
    [InlineData("é*", "É42", true)]
    [InlineData("𐐨*", "𐐀42", true)]
    [InlineData(" ", " ", true)]
    public void IsMatch_GlobSyntaxAndScalars_ReturnExpectedResults(string pattern, string text, bool expected)
    {
        var sut = new GlobPatternMatcher(pattern);

        sut.IsMatch(text).ShouldBe(expected);
        sut.IsMatch(text.AsSpan()).ShouldBe(expected);
    }

    /// <summary>Case-sensitive callers can use the same glob syntax without invariant folding.</summary>
    [Theory]
    [InlineData("Report:42", true)]
    [InlineData("report:42", false)]
    public void IsMatch_CaseSensitiveLiteral_PreservesCase(string text, bool expected)
    {
        var sut = new GlobPatternMatcher("Report:*", ignoreCase: false);

        sut.IsMatch(text).ShouldBe(expected);
    }

    /// <summary>Unsupported and unfinished escapes fail when the reusable pattern is prepared.</summary>
    [Theory]
    [InlineData(@"bad\")]
    [InlineData(@"bad\x")]
    public void Constructor_InvalidEscape_RejectsPattern(string pattern)
    {
        Should.Throw<ArgumentException>(() => new GlobPatternMatcher(pattern));
    }

    /// <summary>Unpaired UTF-16 is not silently converted into replacement scalars.</summary>
    [Fact]
    public void ConstructorAndMatching_InvalidUnicode_RejectEncounteredText()
    {
        var invalid = new string((char)0xd800, 1);

        Should.Throw<ArgumentException>(() => new GlobPatternMatcher(invalid));
        Should.Throw<ArgumentException>(() => new GlobPatternMatcher("*").IsMatch(invalid));
        Should.Throw<ArgumentNullException>(() => new GlobPatternMatcher(null));
        Should.Throw<ArgumentNullException>(() => new GlobPatternMatcher("*").IsMatch((string)null));
    }

    /// <summary>A prepared matcher can serve concurrent callers without retaining per-call progress.</summary>
    [Fact]
    public void IsMatch_ConcurrentCalls_DoNotShareProgress()
    {
        var sut = new GlobPatternMatcher("report:*:?");

        Parallel.For(0, 1000, index =>
        {
            sut.IsMatch("REPORT:" + index + ":😀").ShouldBeTrue();
            sut.IsMatch("REPORT:" + index + ":😀x").ShouldBeFalse();
        });
    }
}