// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;
using System.Globalization;

/// <summary>
/// Provides human-readable duration and relative-time extension methods.
/// </summary>
/// <remarks><example><code>var text = TimeSpan.FromMinutes(3).ToDurationText();</code></example></remarks>
public static class HumanReadableTimeExtensions
{
    /// <summary>
    /// Formats a duration as localized text.
    /// </summary>
    /// <param name="source">The duration to format. Negative values are formatted as absolute durations.</param>
    /// <param name="options">Optional formatting options.</param>
    /// <param name="textProvider">Optional text provider.</param>
    /// <returns>A localized duration text.</returns>
    /// <remarks><example><code>var text = TimeSpan.FromMinutes(3).ToDurationText();</code></example></remarks>
    [DebuggerStepThrough]
    public static string ToDurationText(
        this TimeSpan source,
        RelativeTimeFormatOptions options = null,
        IRelativeTimeTextProvider textProvider = null)
    {
        options ??= new RelativeTimeFormatOptions();
        textProvider ??= RelativeTimeTextProvider.Global;

        var component = SelectComponent(source.Duration(), options, RelativeTimeUnit.Millisecond);
        return textProvider.FormatUnit(component.Unit, component.Value, options.Culture, options.UseShortUnits);
    }

    /// <summary>
    /// Formats a <see cref="DateTime"/> relative to an explicit reference value.
    /// </summary>
    /// <param name="source">The target value.</param>
    /// <param name="reference">The reference value.</param>
    /// <param name="options">Optional formatting options.</param>
    /// <param name="textProvider">Optional text provider.</param>
    /// <returns>A localized relative-time text.</returns>
    /// <remarks><example><code>var text = target.ToRelativeTimeText(reference);</code></example></remarks>
    [DebuggerStepThrough]
    public static string ToRelativeTimeText(
        this DateTime source,
        DateTime reference,
        RelativeTimeFormatOptions options = null,
        IRelativeTimeTextProvider textProvider = null)
    {
        return FormatRelative(source - reference, options, textProvider, RelativeTimeUnit.Millisecond);
    }

    /// <summary>
    /// Formats a <see cref="DateTime"/> relative to an appropriate current reference value.
    /// </summary>
    /// <param name="source">The target value.</param>
    /// <param name="options">Optional formatting options.</param>
    /// <param name="textProvider">Optional text provider.</param>
    /// <returns>A localized relative-time text.</returns>
    /// <remarks><example><code>var text = target.ToRelativeTimeText();</code></example></remarks>
    [DebuggerStepThrough]
    public static string ToRelativeTimeText(
        this DateTime source,
        RelativeTimeFormatOptions options = null,
        IRelativeTimeTextProvider textProvider = null)
    {
        var reference = source.Kind switch
        {
            DateTimeKind.Utc => DateTime.UtcNow,
            DateTimeKind.Local => DateTime.Now,
            DateTimeKind.Unspecified => DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            _ => DateTime.UtcNow
        };

        return source.ToRelativeTimeText(reference, options, textProvider);
    }

    /// <summary>
    /// Formats a <see cref="DateTimeOffset"/> relative to an explicit reference instant.
    /// </summary>
    /// <param name="source">The target instant.</param>
    /// <param name="reference">The reference instant.</param>
    /// <param name="options">Optional formatting options.</param>
    /// <param name="textProvider">Optional text provider.</param>
    /// <returns>A localized relative-time text.</returns>
    /// <remarks><example><code>var text = target.ToRelativeTimeText(reference);</code></example></remarks>
    [DebuggerStepThrough]
    public static string ToRelativeTimeText(
        this DateTimeOffset source,
        DateTimeOffset reference,
        RelativeTimeFormatOptions options = null,
        IRelativeTimeTextProvider textProvider = null)
    {
        return FormatRelative(source.UtcDateTime - reference.UtcDateTime, options, textProvider, RelativeTimeUnit.Millisecond);
    }

    /// <summary>
    /// Formats a <see cref="DateOnly"/> relative to an explicit reference date using day-or-larger units.
    /// </summary>
    /// <param name="source">The target date.</param>
    /// <param name="reference">The reference date.</param>
    /// <param name="options">Optional formatting options.</param>
    /// <param name="textProvider">Optional text provider.</param>
    /// <returns>A localized relative-time text.</returns>
    /// <remarks><example><code>var text = target.ToRelativeTimeText(reference);</code></example></remarks>
    [DebuggerStepThrough]
    public static string ToRelativeTimeText(
        this DateOnly source,
        DateOnly reference,
        RelativeTimeFormatOptions options = null,
        IRelativeTimeTextProvider textProvider = null)
    {
        options = WithMinimumUnit(options, RelativeTimeUnit.Day);
        return FormatRelative(TimeSpan.FromDays(source.DayNumber - reference.DayNumber), options, textProvider, RelativeTimeUnit.Day);
    }

    /// <summary>
    /// Formats a <see cref="TimeOnly"/> relative to an explicit reference time using same-day semantics.
    /// </summary>
    /// <param name="source">The target time.</param>
    /// <param name="reference">The reference time.</param>
    /// <param name="options">Optional formatting options.</param>
    /// <param name="textProvider">Optional text provider.</param>
    /// <returns>A localized relative-time text.</returns>
    /// <remarks><example><code>var text = target.ToRelativeTimeText(reference);</code></example></remarks>
    [DebuggerStepThrough]
    public static string ToRelativeTimeText(
        this TimeOnly source,
        TimeOnly reference,
        RelativeTimeFormatOptions options = null,
        IRelativeTimeTextProvider textProvider = null)
    {
        return FormatRelative(source.ToTimeSpan() - reference.ToTimeSpan(), options, textProvider, RelativeTimeUnit.Millisecond);
    }

    private static string FormatRelative(
        TimeSpan delta,
        RelativeTimeFormatOptions options,
        IRelativeTimeTextProvider textProvider,
        RelativeTimeUnit defaultMinimumUnit)
    {
        options ??= new RelativeTimeFormatOptions { MinimumUnit = defaultMinimumUnit };
        textProvider ??= RelativeTimeTextProvider.Global;

        var duration = delta.Duration();
        if (duration <= options.NowThreshold)
        {
            return textProvider.Now(options.Culture, options.UseShortUnits);
        }

        var component = SelectComponent(duration, options, defaultMinimumUnit);
        var durationText = textProvider.FormatUnit(component.Unit, component.Value, options.Culture, options.UseShortUnits);
        if (!options.UseSuffix)
        {
            return durationText;
        }

        var direction = delta < TimeSpan.Zero ? RelativeTimeDirection.Past : RelativeTimeDirection.Future;
        return textProvider.FormatRelative(durationText, direction, options.Culture, options.UseShortUnits);
    }

    private static RelativeTimeFormatOptions WithMinimumUnit(RelativeTimeFormatOptions options, RelativeTimeUnit minimumUnit)
    {
        options ??= new RelativeTimeFormatOptions();
        return options.MinimumUnit < minimumUnit ? options with { MinimumUnit = minimumUnit } : options;
    }

    private static RelativeTimeComponent SelectComponent(TimeSpan duration, RelativeTimeFormatOptions options, RelativeTimeUnit defaultMinimumUnit)
    {
        var minimumUnit = options.MinimumUnit < defaultMinimumUnit ? defaultMinimumUnit : options.MinimumUnit;
        var unit = minimumUnit;
        var seconds = duration.TotalSeconds;

        if (minimumUnit <= RelativeTimeUnit.Year && duration.TotalDays >= 365)
        {
            unit = RelativeTimeUnit.Year;
        }
        else if (minimumUnit <= RelativeTimeUnit.Month && duration.TotalDays >= 30)
        {
            unit = RelativeTimeUnit.Month;
        }
        else if (minimumUnit <= RelativeTimeUnit.Week && duration.TotalDays >= 7)
        {
            unit = RelativeTimeUnit.Week;
        }
        else if (minimumUnit <= RelativeTimeUnit.Day && duration.TotalDays >= 1)
        {
            unit = RelativeTimeUnit.Day;
        }
        else if (minimumUnit <= RelativeTimeUnit.Hour && duration.TotalHours >= 1)
        {
            unit = RelativeTimeUnit.Hour;
        }
        else if (minimumUnit <= RelativeTimeUnit.Minute && duration.TotalMinutes >= 1)
        {
            unit = RelativeTimeUnit.Minute;
        }
        else if (minimumUnit <= RelativeTimeUnit.Second && duration.TotalSeconds >= 1)
        {
            unit = RelativeTimeUnit.Second;
        }
        else if (minimumUnit <= RelativeTimeUnit.Millisecond)
        {
            unit = RelativeTimeUnit.Millisecond;
        }

        var rawValue = unit switch
        {
            RelativeTimeUnit.Millisecond => duration.TotalMilliseconds,
            RelativeTimeUnit.Second => seconds,
            RelativeTimeUnit.Minute => duration.TotalMinutes,
            RelativeTimeUnit.Hour => duration.TotalHours,
            RelativeTimeUnit.Day => duration.TotalDays,
            RelativeTimeUnit.Week => duration.TotalDays / 7,
            RelativeTimeUnit.Month => duration.TotalDays / 30,
            RelativeTimeUnit.Year => duration.TotalDays / 365,
            _ => seconds
        };

        var rounded = options.RoundingMode switch
        {
            RelativeTimeRoundingMode.Ceiling => (long)Math.Ceiling(rawValue),
            RelativeTimeRoundingMode.Round => (long)Math.Round(rawValue, MidpointRounding.AwayFromZero),
            _ => (long)Math.Floor(rawValue)
        };

        return new RelativeTimeComponent(unit, Math.Max(1, rounded));
    }

    private readonly record struct RelativeTimeComponent(RelativeTimeUnit Unit, long Value);
}
