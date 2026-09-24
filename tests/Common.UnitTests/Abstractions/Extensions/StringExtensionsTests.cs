// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Abstractions.Extensions;

[UnitTest("Common")]
public class StringExtensionsTests
{
    [Fact]
    public void Distinct_DuplicateWords_RetainsFirstOccurrenceOrder()
    {
        "red blue red green blue".Distinct().ShouldBe("red blue green");
    }

    [Fact]
    public void Distinct_ExtraSpaces_NormalizesSpacing()
    {
        "  red   blue  ".Distinct().ShouldBe("red blue");
    }

    [Fact]
    public void Distinct_DifferentCasing_PreservesBothWords()
    {
        "red Red red".Distinct().ShouldBe("red Red");
    }

    [Fact]
    public void Distinct_NullOrEmpty_ReturnsOriginalValue()
    {
        ((string)null).Distinct().ShouldBeNull();
        string.Empty.Distinct().ShouldBeEmpty();
    }
}