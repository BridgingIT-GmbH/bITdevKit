// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Selects sample quantiles without interpolating or converting actual observations.</summary>
/// <remarks>Callers own sample retention, ascending ordering and the meaning of an empty observation window.</remarks>
/// <example><code>
/// var ordered = observations.OrderBy(value =&gt; value).ToArray();
/// var p95 = SampleStatistics.NearestRank(ordered, 0.95);
/// </code></example>
public static class SampleStatistics
{
    /// <summary>Selects a nearest-rank quantile from nonempty, ascending observations.</summary>
    /// <typeparam name="T">The original observation type, returned without numeric conversion.</typeparam>
    /// <param name="sortedValues">Observations already ordered ascending and stable for this call.</param>
    /// <param name="quantile">The finite fraction from zero through one; zero selects the first observation.</param>
    /// <returns>The actual observation at rank ceil(count multiplied by quantile), with a minimum rank of one.</returns>
    /// <remarks>This method does not sort, copy or validate the ordering. Nearest-rank p50 can differ from a midpoint median.</remarks>
    /// <exception cref="ArgumentNullException">The observations are null.</exception>
    /// <exception cref="ArgumentException">The observations are empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The quantile is nonfinite or outside zero through one.</exception>
    /// <example><code>
    /// var p50 = SampleStatistics.NearestRank(new[] { 10, 20 }, 0.5); // 10
    /// var p95 = SampleStatistics.NearestRank(orderedDurations, 0.95);
    /// </code></example>
    public static T NearestRank<T>(IReadOnlyList<T> sortedValues, double quantile)
    {
        ArgumentNullException.ThrowIfNull(sortedValues);
        if (!double.IsFinite(quantile) || quantile is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(quantile), "A finite fraction from zero through one is required.");
        }

        if (sortedValues.Count == 0)
        {
            throw new ArgumentException("At least one observation is required.", nameof(sortedValues));
        }

        var index = Math.Max(0, (int)Math.Ceiling(sortedValues.Count * quantile) - 1);
        return sortedValues[index];
    }
}