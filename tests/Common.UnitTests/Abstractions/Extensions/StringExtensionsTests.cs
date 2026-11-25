// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Abstractions.Extensions;

/// <summary>Verifies word and UTF-8 byte operations on strings.</summary>
/// <example><code>var label = "Café!".TruncateUtf8(5);</code></example>
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

    /// <summary>Checks byte budgets across ASCII, multibyte and supplementary scalars.</summary>
    /// <example><code>var label = "é😀".TruncateUtf8(5); // "é"</code></example>
    [Theory]
    [InlineData(null, 0, null)]
    [InlineData("", 0, "")]
    [InlineData("hello", 0, "")]
    [InlineData("hello", 4, "hell")]
    [InlineData("Café!", 5, "Café")]
    [InlineData("é😀!", 5, "é")]
    [InlineData("é😀!", 6, "é😀")]
    [InlineData("😀é", 3, "")]
    public void TruncateUtf8_ByteBudget_ReturnsLongestFittingPrefix(string source, int maximumBytes, string expected)
    {
        var result = source.TruncateUtf8(maximumBytes);

        result.ShouldBe(expected);
    }

    /// <summary>Checks that a fitting string is returned without being copied.</summary>
    /// <example><code>var label = "Café".TruncateUtf8(5);</code></example>
    [Fact]
    public void TruncateUtf8_WholeValueFits_ReturnsOriginalString()
    {
        var source = new string(['C', 'a', 'f', 'é']);

        var result = source.TruncateUtf8(5);

        result.ShouldBeSameAs(source);
        var ascii = new string('a', 32);
        ascii.TruncateUtf8(32).ShouldBeSameAs(ascii);
    }

    /// <summary>Checks that truncating a long value returns only the fitting prefix.</summary>
    /// <example><code>var prefix = new string('a', 1_048_576).TruncateUtf8(32);</code></example>
    [Fact]
    public void TruncateUtf8_LongInputWithSmallBudget_RetainsExactPrefix()
    {
        var prefix = new string('a', 32);
        var source = prefix + new string('b', 1_048_576 - prefix.Length);

        source.TruncateUtf8(32).ShouldBe(prefix);
        ("a😀" + source).TruncateUtf8(4).ShouldBe("a");
    }

    /// <summary>Checks replacement of an unpaired surrogate during truncation.</summary>
    /// <example><code>var label = "A\uD800!".TruncateUtf8(4); // "A\uFFFD"</code></example>
    [Fact]
    public void TruncateUtf8_UnpairedSurrogate_UsesFallbackWhenTruncating()
    {
        var source = "A\uD800!";

        source.TruncateUtf8(5).ShouldBeSameAs(source);
        source.TruncateUtf8(4).ShouldBe("A\uFFFD");
        source.TruncateUtf8(3).ShouldBe("A");
    }

    /// <summary>Checks that even null input rejects a negative byte budget.</summary>
    /// <example><code>var error = Should.Throw&lt;ArgumentOutOfRangeException&gt;(() => "Café".TruncateUtf8(-1));</code></example>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Café")]
    public void TruncateUtf8_NegativeBudget_Throws(string source)
    {
        var error = Should.Throw<ArgumentOutOfRangeException>(() => source.TruncateUtf8(-1));

        error.ParamName.ShouldBe("maximumBytes");
    }
}
