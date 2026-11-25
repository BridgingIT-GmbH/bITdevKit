// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Abstractions;

/// <summary>Verifies complete-path matching and the path-specific glob grammar.</summary>
/// <example><code>var matcher = new PathGlobMatcher(["/reports/**"]);</code></example>
[UnitTest("Common")]
public sealed class PathGlobMatcherTests
{
    /// <summary>Checks complete-path matching, root aliases, empty segments, and both wildcard forms.</summary>
    /// <example><code>var matches = new PathGlobMatcher(["/**/ready"]).IsMatch("/jobs/daily/ready");</code></example>
    [Theory]
    [InlineData("/reports/*", "/REPORTS/monthly", true)]
    [InlineData("/reports/*", "/reports/monthly/item", false)]
    [InlineData("/reports/*/ready", "/reports//ready", true)]
    [InlineData("/reports/*/ready", "/reports/ready", false)]
    [InlineData("/reports/**", "/reports///", true)]
    [InlineData("/reports/**", "/reports-old/item", false)]
    [InlineData("/reports/**", "/archive/reports/item", false)]
    [InlineData("/**/**/ready", "/ready", true)]
    [InlineData("/**/**/ready", "/jobs/daily/ready", true)]
    [InlineData("/**/ready", "/jobs/ready/extra", false)]
    [InlineData("/jobs/*-daily", "/jobs/-daily/", true)]
    [InlineData("/", "/", true)]
    [InlineData("/", "", true)]
    [InlineData("/", null, true)]
    [InlineData("/reports", "xreports", false)]
    [InlineData("/**", "reports", false)]
    public void IsMatch_PathSegments_MatchesTheCompleteRootedPath(string pattern, string path, bool expected)
    {
        var sut = new PathGlobMatcher([pattern]);

        var result = sut.IsMatch(path);

        result.ShouldBe(expected);
    }

    /// <summary>Checks casing on UTF-16 code units for both Unicode patterns and Unicode values.</summary>
    /// <example><code>var matches = new PathGlobMatcher(["/é*"]).IsMatch("/É42");</code></example>
    [Theory]
    [InlineData("/é*", "/É42", true)]
    [InlineData("/reports/*", "/reports/É42", true)]
    [InlineData("/𐐨*", "/𐐀42", false)]
    [InlineData("/𐐨*", "/𐐨42", true)]
    public void IsMatch_UnicodeSegments_UsesInvariantCodeUnitComparison(string pattern, string path, bool expected)
    {
        var sut = new PathGlobMatcher([pattern]);

        var result = sut.IsMatch(path);

        result.ShouldBe(expected);
    }

    /// <summary>Checks that an exact unpaired surrogate is accepted as a literal.</summary>
    /// <example><code>var matcher = new PathGlobMatcher(["/" + (char)0xd800]);</code></example>
    [Fact]
    public void IsMatch_UnpairedSurrogate_MatchesTheExactLiteral()
    {
        var path = "/" + (char)0xd800;
        var sut = new PathGlobMatcher([path]);

        sut.IsMatch(path).ShouldBeTrue();
        sut.IsMatch("/" + (char)0xd801).ShouldBeFalse();
    }

    /// <summary>Checks that each matcher snapshots its pattern list during construction.</summary>
    /// <example><code>var matcher = new PathGlobMatcher(["/health", "/jobs/**"]);</code></example>
    [Fact]
    public void IsMatch_MultiplePatterns_MatchesAnyOriginalPattern()
    {
        var patterns = new[] { "/health", "/jobs/**" };
        var sut = new PathGlobMatcher(patterns);

        patterns[1] = "/reports";

        sut.IsMatch("/health").ShouldBeTrue();
        sut.IsMatch("/jobs/daily").ShouldBeTrue();
        sut.IsMatch("/reports").ShouldBeFalse();
        new PathGlobMatcher([]).IsMatch("/").ShouldBeFalse();
    }

    /// <summary>Checks validation of unsupported syntax before a matcher can be used.</summary>
    /// <example><code>var matcher = new PathGlobMatcher(["/jobs/**"]);</code></example>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("health")]
    [InlineData("/jobs/**-daily")]
    [InlineData("/jobs/***")]
    [InlineData("/jobs/?")]
    [InlineData("/jobs/#latest")]
    [InlineData("/jobs/\n")]
    [InlineData("/jobs/{id}")]
    [InlineData("/jobs/[1-9]")]
    [InlineData("/jobs/(today)")]
    [InlineData("/jobs\\daily")]
    public void Constructor_InvalidPattern_ThrowsArgumentException(string pattern)
    {
        var error = Should.Throw<ArgumentException>(() => new PathGlobMatcher([pattern]));

        error.ParamName.ShouldBe("patterns");
    }

    /// <summary>Checks validation exactly at the maximum list size and pattern length.</summary>
    /// <example><code>var matcher = new PathGlobMatcher(Enumerable.Repeat("/jobs", 128).ToArray());</code></example>
    [Fact]
    public void Constructor_PatternLimits_RejectsOnlyValuesBeyondTheLimits()
    {
        var maximumLength = string.Concat(Enumerable.Repeat("/a", 128));
        var maximumCount = Enumerable.Repeat(maximumLength, 128).ToList();
        var sut = new PathGlobMatcher(maximumCount);

        sut.IsMatch(maximumLength).ShouldBeTrue();
        Should.Throw<ArgumentException>(() => new PathGlobMatcher([maximumLength + "a"])).ParamName.ShouldBe("patterns");
        maximumCount.Add("/jobs");
        Should.Throw<ArgumentException>(() => new PathGlobMatcher(maximumCount)).ParamName.ShouldBe("patterns");
        Should.Throw<ArgumentNullException>(() => new PathGlobMatcher(null)).ParamName.ShouldBe("patterns");
    }
}
