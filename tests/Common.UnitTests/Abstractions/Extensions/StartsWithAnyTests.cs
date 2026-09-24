// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Abstractions.Extensions;

[UnitTest("Common")]
public class StartsWithAnyTests
{
    [Fact]
    public void StartsWithAny_DefaultComparison_IgnoresCase()
    {
        "HTTPS://example.test".StartsWithAny(["http://", "https://"]).ShouldBeTrue();
    }

    [Fact]
    public void StartsWithAny_OrdinalComparison_IsCaseSensitive()
    {
        "HTTPS://example.test".StartsWithAny(["https://"], StringComparison.Ordinal).ShouldBeFalse();
    }

    [Fact]
    public void StartsWithAny_NullPrefix_SkipsPrefix()
    {
        "https://example.test".StartsWithAny([null, "https://"]).ShouldBeTrue();
    }

    [Fact]
    public void StartsWithAny_NullOrEmptyInput_ReturnsFalse()
    {
        ((string)null).StartsWithAny(["https://"]).ShouldBeFalse();
        "value".StartsWithAny(null).ShouldBeFalse();
        "value".StartsWithAny([]).ShouldBeFalse();
    }
}