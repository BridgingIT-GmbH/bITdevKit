// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Abstractions.Extensions;

using System.Globalization;

/// <summary>Checks the established simple-wildcard contract independently of the richer glob syntax.</summary>
public sealed class MatchCompatibilityTests
{
    /// <summary>Question marks and backslashes are literal, and regex-style final-newline anchoring remains observable.</summary>
    [Theory]
    [InlineData("?", "?", true)]
    [InlineData("x", "?", false)]
    [InlineData(@"a\b", @"a\*", true)]
    [InlineData(@"a*b", @"a\*", false)]
    [InlineData("report\n", "report", true)]
    [InlineData("report\n", "*", true)]
    [InlineData("a\nb", "*", true)]
    [InlineData("a\r\nb", "*", true)]
    [InlineData("a\nb", "a*b", true)]
    [InlineData("a\r\nb", "a*b", true)]
    [InlineData("a\rb", "*", true)]
    public void Match_LegacySyntaxAndNewlines_PreserveResults(string source, string pattern, bool expected)
    {
        source.Match(pattern).ShouldBe(expected);
        source.MatchAny([pattern]).ShouldBe(expected);
    }

    /// <summary>Ignore-case matching uses the current culture, including Turkish dotted and dotless I rules.</summary>
    [Fact]
    public void Match_TurkishIgnoreCase_PreservesCultureSpecificComparison()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");

            "i".Match("I").ShouldBeFalse();
            "ı".Match("I").ShouldBeTrue();
            "İ".Match("i").ShouldBeTrue();
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    /// <summary>The legacy UTF-16 API accepts a lone surrogate rather than imposing a Unicode-scalar validation contract.</summary>
    [Fact]
    public void Match_UnpairedSurrogate_PreservesExistingAcceptance()
    {
        var source = new string((char)0xd800, 1);

        source.Match("*").ShouldBeTrue();
        source.Match(source, ignoreCase: false).ShouldBeTrue();
    }
}