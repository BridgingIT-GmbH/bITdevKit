// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;

/// <summary>Checks cache-owned bounds and whole-key matching around the portable glob algorithm.</summary>
public sealed class CachePatternCompatibilityTests
{
    /// <summary>The cache rejects empty, whitespace-only, and excessive patterns before building a matcher.</summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\0")]
    public void Constructor_InvalidCachePattern_RejectsBeforeMatching(string pattern)
    {
        Should.Throw<ArgumentException>(() => new CachePatternMatcher(pattern));
        Should.Throw<ArgumentException>(() => new CachePatternMatcher(new string('x', 4097)));
    }

    /// <summary>A cache glob matches a complete key and allows newline scalars within a star match.</summary>
    [Fact]
    public void IsMatch_NewlineScalars_PreservesWholeKeyBehavior()
    {
        new CachePatternMatcher("a*").IsMatch("a\nb").ShouldBeTrue();
        new CachePatternMatcher("report").IsMatch("report\n").ShouldBeFalse();
    }

    /// <summary>Matching never silently replaces invalid UTF-16 key text.</summary>
    [Fact]
    public void IsMatch_UnpairedSurrogate_RejectsInvalidKeyText()
    {
        var sut = new CachePatternMatcher("*");

        Should.Throw<ArgumentException>(() => sut.IsMatch(new string((char)0xd800, 1)));
    }
}