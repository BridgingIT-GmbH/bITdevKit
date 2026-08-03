// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

[UnitTest("Common")]
public class ShortenerTests
{
    [Fact]
    public void Apply_ValueFits_ReturnsOriginalValue()
    {
        const string value = "archives/2026/report.pdf";

        var result = Shortener.Apply(value, new ShorteningOptions { MaximumLength = value.Length });

        result.ShouldBe(value);
    }

    [Fact]
    public void LeftTruncate_ValueExceedsLimit_PreservesTerminalCharacters()
    {
        var result = Shortener.LeftTruncate.Apply(
            "archives/2026/report.pdf",
            new ShorteningOptions { MaximumLength = 12 });

        result.ShouldBe("...eport.pdf");
        result.Length.ShouldBe(12);
    }

    [Fact]
    public void RightTruncate_ValueExceedsLimit_PreservesInitialCharacters()
    {
        var result = Shortener.RightTruncate.Apply(
            "archives/2026/report.pdf",
            new ShorteningOptions { MaximumLength = 12 });

        result.ShouldBe("archives/...");
        result.Length.ShouldBe(12);
    }

    [Fact]
    public void SegmentInitials_PathExceedsLimit_CompressesParentSegmentsAndPreservesFileName()
    {
        var result = Shortener.SegmentInitials.Apply(
            "archives/2026/july/report.pdf",
            new ShorteningOptions { MaximumLength = 18 });

        result.ShouldBe("a/2/j/report.pdf");
        result.Length.ShouldBeLessThanOrEqualTo(18);
    }

    [Fact]
    public void SegmentPrefixes_CustomSeparator_CompressesDelimitedValue()
    {
        var result = Shortener.SegmentPrefixes.Apply(
            "Company.Product.Feature.Handler",
            new ShorteningOptions
            {
                MaximumLength = 16,
                Separator = ".",
                SegmentPrefixLength = 2
            });

        result.ShouldBe("Co.Pr.Fe.Handler");
    }

    [Fact]
    public void CamelCaseInitials_PathWithPascalCaseSegments_UsesWordInitials()
    {
        var result = Shortener.CamelCaseInitials.Apply(
            "FirstProduct/Items/PriceDiscount/aaa.json",
            new ShorteningOptions { MaximumLength = 20 });

        result.ShouldBe("FP/I/PD/aaa.json");
    }

    [Fact]
    public void CamelCaseInitials_PathWithAcronymRun_PreservesAcronymAndFollowingWordInitial()
    {
        var result = Shortener.CamelCaseInitials.Apply(
            "XMLDocument/ItemPrices/aaa.json",
            new ShorteningOptions { MaximumLength = 20 });

        result.ShouldBe("XD/IP/aaa.json");
    }

    [Theory]
    [InlineData(ShorteningOverflowTruncation.Left, "...aaa.json")]
    [InlineData(ShorteningOverflowTruncation.Right, "FP/I/PD/...")]
    public void CamelCaseInitials_AbbreviatedValueStillExceedsLimit_UsesConfiguredOverflowTruncation(
        ShorteningOverflowTruncation overflowTruncation,
        string expected)
    {
        var result = Shortener.CamelCaseInitials.Apply(
            "FirstProduct/Items/PriceDiscount/aaa.json",
            new ShorteningOptions
            {
                MaximumLength = 11,
                OverflowTruncation = overflowTruncation
            });

        result.ShouldBe(expected);
    }

    [Fact]
    public void Adaptive_PathExceedsLimit_UsesLongestFittingSegmentPrefixes()
    {
        var result = Shortener.Apply(
            "archives/2026/july/report.pdf",
            new ShorteningOptions { MaximumLength = 20, SegmentPrefixLength = 3 });

        result.ShouldBe("ar/20/ju/report.pdf");
        result.Length.ShouldBeLessThanOrEqualTo(20);
    }

    [Fact]
    public void Shorten_EmptyPlaceholder_OmitsMarker()
    {
        var result = Shortener.Apply(
            "archives/2026/report.pdf",
            new ShorteningOptions
            {
                MaximumLength = 8,
                Placeholder = string.Empty,
                Strategy = Shortener.LeftTruncate
            });

        result.ShouldBe("port.pdf");
    }

    [Theory]
    [InlineData(-1, "/", 3)]
    [InlineData(12, "", 3)]
    [InlineData(12, "/", 0)]
    public void Shorten_InvalidOptions_ThrowsArgumentException(int maximumLength, string separator, int prefixLength)
    {
        Should.Throw<ArgumentException>(() => Shortener.Apply(
            "archives/2026/report.pdf",
            new ShorteningOptions
            {
                MaximumLength = maximumLength,
                Separator = separator,
                SegmentPrefixLength = prefixLength
            }));
    }
}
