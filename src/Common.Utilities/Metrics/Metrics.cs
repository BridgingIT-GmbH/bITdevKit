// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;

/// <summary>
/// Provides shared naming and recording helpers for runtime metrics.
/// </summary>
/// <example>
/// <code>
/// var series = DevKitMetrics.Series("requester_send", DevKitMetrics.NormalizeTypeName(typeof(CreateOrderCommand)));
/// var started = DevKitMetrics.StartTimestamp();
/// DevKitMetrics.Increment(meterFactory, series);
/// DevKitMetrics.ChangeCurrent(meterFactory, DevKitMetrics.CurrentSeries(series), 1);
/// DevKitMetrics.RecordDuration(meterFactory, DevKitMetrics.DurationSeries(series), started);
/// </code>
/// </example>
public static class Metrics
{
    public const string MeterName = "bdk";

    /// <summary>
    /// Captures a high-resolution timestamp for subsequent duration measurement.
    /// </summary>
    /// <returns>The current stopwatch timestamp.</returns>
    public static long StartTimestamp()
    {
        return Stopwatch.GetTimestamp();
    }

    /// <summary>
    /// Normalizes a free-form value into a metric-safe token.
    /// </summary>
    /// <param name="value">The value to normalize.</param>
    /// <returns>A normalized lower-case token using underscores as separators.</returns>
    public static string NormalizePart(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        var builder = new StringBuilder(value.Length);
        var previousUnderscore = false;

        foreach (var character in value.Trim())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                previousUnderscore = false;
                continue;
            }

            if (!previousUnderscore)
            {
                builder.Append('_');
                previousUnderscore = true;
            }
        }

        while (builder.Length > 0 && builder[^1] == '_')
        {
            builder.Length--;
        }

        return builder.Length == 0 ? "unknown" : builder.ToString();
    }

    /// <summary>
    /// Normalizes a CLR type name into a stable metric-safe token.
    /// </summary>
    /// <param name="type">The CLR type to normalize.</param>
    /// <returns>A normalized token derived from the supplied type.</returns>
    public static string NormalizeTypeName(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return NormalizePart(FormatTypeName(type));
    }

    /// <summary>
    /// Builds a metric series name from a family name and optional dynamic parts.
    /// </summary>
    /// <param name="family">The base metric family.</param>
    /// <param name="parts">Optional parts appended to the family.</param>
    /// <returns>A normalized metric series name.</returns>
    public static string Series(string family, params string[] parts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(family);

        var normalizedParts = parts
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(NormalizePart)
            .ToArray();

        return normalizedParts.Length == 0
            ? NormalizePart(family)
            : $"{NormalizePart(family)}_{string.Join("_", normalizedParts)}";
    }

    /// <summary>
    /// Builds the failure series name for a base metric series.
    /// </summary>
    /// <param name="series">The base metric series.</param>
    /// <returns>The failure series name.</returns>
    public static string FailureSeries(string series)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(series);

        return $"{series}_failure";
    }

    /// <summary>
    /// Builds the duration series name for a base metric series.
    /// </summary>
    /// <param name="series">The base metric series.</param>
    /// <returns>The duration histogram series name.</returns>
    public static string DurationSeries(string series)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(series);

        return $"{series}_duration";
    }

    /// <summary>
    /// Builds the current live-view series name for a base metric series.
    /// </summary>
    /// <param name="series">The base metric series.</param>
    /// <returns>The current live-view series name.</returns>
    public static string CurrentSeries(string series)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(series);

        return $"{series}_current";
    }

    /// <summary>
    /// Increments the specified cumulative counter series by one.
    /// </summary>
    /// <param name="meterFactory">The meter factory used to create the counter.</param>
    /// <param name="series">The counter series name.</param>
    public static void Increment(IMeterFactory meterFactory, string series)
    {
        if (meterFactory is null || string.IsNullOrWhiteSpace(series))
        {
            return;
        }

        meterFactory.Create(MeterName).CreateCounter<int>(series).Add(1);
    }

    /// <summary>
    /// Adjusts the specified current live-view series using an up/down counter.
    /// </summary>
    /// <param name="meterFactory">The meter factory used to create the up/down counter.</param>
    /// <param name="series">The current series name.</param>
    /// <param name="value">The delta to apply. Positive values increment and negative values decrement.</param>
    public static void ChangeCurrent(IMeterFactory meterFactory, string series, int value)
    {
        if (meterFactory is null || string.IsNullOrWhiteSpace(series) || value == 0)
        {
            return;
        }

        meterFactory.Create(MeterName).CreateUpDownCounter<int>(series).Add(value);
    }

    /// <summary>
    /// Records the elapsed duration since the supplied start timestamp in milliseconds.
    /// </summary>
    /// <param name="meterFactory">The meter factory used to create the histogram.</param>
    /// <param name="series">The histogram series name.</param>
    /// <param name="startedTimestamp">The timestamp captured earlier via <see cref="StartTimestamp"/>.</param>
    public static void RecordDuration(IMeterFactory meterFactory, string series, long startedTimestamp)
    {
        if (meterFactory is null || string.IsNullOrWhiteSpace(series))
        {
            return;
        }

        var elapsedMilliseconds = Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds;
        meterFactory.Create(MeterName).CreateHistogram<double>(series, unit: "ms").Record(elapsedMilliseconds);
    }

    private static string FormatTypeName(Type type)
    {
        var nullableType = Nullable.GetUnderlyingType(type);
        if (nullableType is not null)
        {
            return $"{FormatTypeName(nullableType)}_nullable";
        }

        if (type.IsArray)
        {
            return $"{FormatTypeName(type.GetElementType()!)}_array";
        }

        if (!type.IsGenericType)
        {
            var name = type.Name;
            var tickIndex = name.IndexOf('`');
            return tickIndex > -1 ? name[..tickIndex] : name;
        }

        var genericName = type.Name;
        var genericTickIndex = genericName.IndexOf('`');
        if (genericTickIndex > -1)
        {
            genericName = genericName[..genericTickIndex];
        }

        var genericArguments = type.GetGenericArguments().Select(FormatTypeName);

        return $"{genericName}_{string.Join("_", genericArguments)}";
    }
}