// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Caching.Dashboard;

using System.Globalization;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Common;

/// <summary>Formats cache sizes and typed dimensions for dashboard summaries and tables.</summary>
/// <example><code>var label = CacheDashboardFormatting.Bytes(904512); // 883.31 KiB</code></example>
public static class CacheDashboardFormatting
{
    /// <summary>Formats a byte count using at most two decimal places and the corresponding binary unit.</summary>
    /// <param name="bytes">The byte count to display.</param>
    /// <returns>An invariant-culture size label without digit grouping.</returns>
    /// <example><code>var label = CacheDashboardFormatting.Bytes(1024); // 1 KiB</code></example>
    public static string Bytes(long bytes) => ByteSize.FormatBinary(bytes);

    /// <summary>Formats a dimension while distinguishing scalar values, empty sets, explicit nulls, and half-open intervals.</summary>
    /// <param name="dimension">The validated stored dimension.</param>
    /// <returns>A plain-text value for HTML-encoded rendering.</returns>
    /// <example><code>var label = CacheDashboardFormatting.Dimension(CacheDimension.Interval("stock", 10L, 20L)); // [10, 20)</code></example>
    public static string Dimension(CacheDimension dimension)
    {
        ArgumentNullException.ThrowIfNull(dimension);
        var values = dimension.Values.Select(DimensionValue).ToArray();
        return dimension.Form switch
        {
            CacheDimensionForm.Set => "{" + string.Join(", ", values) + "}",
            CacheDimensionForm.Interval => "[" + string.Join(", ", values) + ")",
            _ => values[0]
        };
    }

    private static string DimensionValue(CacheDimensionValue value) => value.IsNull ? "null" : value.Kind switch
    {
        CacheDimensionKind.Timestamp => new DateTimeOffset(long.Parse(value.CanonicalValue, CultureInfo.InvariantCulture), TimeSpan.Zero)
            .ToString("O", CultureInfo.InvariantCulture),
        CacheDimensionKind.String when value.CanonicalValue.Length == 0 => "\"\"",
        _ => value.CanonicalValue
    };
}