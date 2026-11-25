// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Profiling;

/// <summary>Checks the established UTF-16 segment contract around reusable ASCII glob matching.</summary>
/// <example><code>var matcher = new RequestProfilingPathMatcher(["/reports/*"]);</code></example>
public sealed class RequestProfilingPatternCompatibilityTests
{
    /// <summary>Segment matching uses invariant character comparison without changing supplementary-letter behavior.</summary>
    [Theory]
    [InlineData("/Reports/*", "/reports/item", true)]
    [InlineData("/Reports/*", "/reports/item/extra", false)]
    [InlineData("/é*", "/É42", true)]
    [InlineData("/𐐨*", "/𐐀42", false)]
    [InlineData("/𐐨*", "/𐐨42", true)]
    public void IsMatch_AsciiAndUnicodeSegments_PreserveResults(string pattern, string path, bool expected)
    {
        var sut = new RequestProfilingPathMatcher([pattern]);

        sut.IsMatch(path).ShouldBe(expected);
    }

    /// <summary>The path API preserves its existing acceptance of exact UTF-16 literals outside the ASCII fast path.</summary>
    [Fact]
    public void IsMatch_UnpairedSurrogateLiteral_PreservesExactMatching()
    {
        var path = "/" + (char)0xd800;
        var sut = new RequestProfilingPathMatcher([path]);

        sut.IsMatch(path).ShouldBeTrue();
    }

    /// <summary>Checks the Presentation API's original results for inputs without a leading slash.</summary>
    /// <example><code>var matches = new RequestProfilingPathMatcher(["/reports"]).IsMatch("xreports");</code></example>
    [Theory]
    [InlineData("/reports", "xreports", true)]
    [InlineData("/reports", "reports", false)]
    [InlineData("/", "x///", true)]
    public void IsMatch_UnrootedPath_PreservesLegacyFirstCodeUnitHandling(string pattern, string path, bool expected)
    {
        var sut = new RequestProfilingPathMatcher([pattern]);

        var result = sut.IsMatch(path);

        result.ShouldBe(expected);
    }
}
