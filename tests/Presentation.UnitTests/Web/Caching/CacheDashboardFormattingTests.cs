// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Caching;

using BridgingIT.DevKit.Presentation.Web.Caching.Dashboard;
using BridgingIT.DevKit.Application.Caching;

/// <summary>Checks the cache dashboard's size units and typed dimension labels.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~CacheDashboardFormattingTests</code></example>
public class CacheDashboardFormattingTests
{
    /// <summary>Sizes retain explicit binary units and match chart hover labels across magnitude boundaries.</summary>
    /// <param name="bytes">The byte count presented by a cache observation.</param>
    /// <param name="expected">The expected human-readable label.</param>
    [Theory]
    [InlineData(0L, "0 B")]
    [InlineData(1023L, "1023 B")]
    [InlineData(1024L, "1 KiB")]
    [InlineData(1152L, "1.13 KiB")]
    [InlineData(1536L, "1.5 KiB")]
    [InlineData(904512L, "883.31 KiB")]
    [InlineData(1048576L, "1 MiB")]
    [InlineData(1073741824L, "1 GiB")]
    [InlineData(1099511627776L, "1 TiB")]
    [InlineData(1125899906842624L, "1 PiB")]
    [InlineData(1152921504606846976L, "1 EiB")]
    [InlineData(long.MaxValue, "8 EiB")]
    [InlineData(-1024L, "-1 KiB")]
    public void Bytes_FormatsBinaryMagnitudeAndPrecision(long bytes, string expected)
    {
        var result = CacheDashboardFormatting.Bytes(bytes);

        result.ShouldBe(expected);
    }

    /// <summary>Dimensions keep explicit null, empty strings, empty sets and half-open intervals distinct.</summary>
    [Fact]
    public void Dimension_PreservesPresenceAndShapeDistinctions()
    {
        CacheDashboardFormatting.Dimension(CacheDimension.Scalar("region", null, CacheDimensionKind.String)).ShouldBe("null");
        CacheDashboardFormatting.Dimension(CacheDimension.Scalar("region", "")).ShouldBe("\"\"");
        CacheDashboardFormatting.Dimension(CacheDimension.Set("tags", CacheDimensionKind.String)).ShouldBe("{}");
        CacheDashboardFormatting.Dimension(CacheDimension.Set("tags", CacheDimensionKind.String, "east", "west")).ShouldBe("{EAST, WEST}");
        CacheDashboardFormatting.Dimension(CacheDimension.Interval("stock", -10L, 20L)).ShouldBe("[-10, 20)");
    }

    /// <summary>Scalar values retain Int64 precision, canonical identifiers and exact UTC timestamps.</summary>
    [Fact]
    public void Dimension_FormatsTypedScalarValues()
    {
        var timestamp = new DateTimeOffset(2026, 11, 11, 14, 15, 16, TimeSpan.FromHours(2)).AddTicks(1);
        var identifier = Guid.Parse("9df5dca7-dfac-43f5-928f-ac359fa9505e");

        CacheDashboardFormatting.Dimension(CacheDimension.Scalar("enabled", true)).ShouldBe("true");
        CacheDashboardFormatting.Dimension(CacheDimension.Scalar("size", long.MaxValue)).ShouldBe("9223372036854775807");
        CacheDashboardFormatting.Dimension(CacheDimension.Scalar("owner", identifier)).ShouldBe(identifier.ToString("D"));
        CacheDashboardFormatting.Dimension(CacheDimension.Scalar("published", timestamp)).ShouldBe("2026-11-11T12:15:16.0000001+00:00");
    }
}