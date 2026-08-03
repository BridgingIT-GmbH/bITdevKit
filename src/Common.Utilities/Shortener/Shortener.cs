// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Applies compact, readable representations to paths and other separator-delimited values.
/// </summary>
/// <example>
/// <code>
/// var display = Shortener.Apply(
///     "archives/2026/july/customer-report.pdf",
///     new PathShorteningOptions { MaximumLength = 28 });
/// // "ar/20/ju/customer-report.pdf" when it fits, otherwise an adaptive shorter form.
/// </code>
/// </example>
public static class Shortener
{
    /// <summary>
    /// Gets the strategy that truncates from the left and preserves the terminal characters.
    /// </summary>
    /// <example>
    /// <code>
    /// var display = Shortener.Apply("a/b/report.pdf", new() { Strategy = Shortener.LeftTruncate });
    /// </code>
    /// </example>
    public static IShorteningStrategy LeftTruncate { get; } = new LeftTruncatingPathShorteningStrategy();

    /// <summary>
    /// Gets the strategy that truncates from the right and preserves the initial characters.
    /// </summary>
    /// <example>
    /// <code>
    /// var display = Shortener.Apply("a/b/report.pdf", new() { Strategy = Shortener.RightTruncate });
    /// </code>
    /// </example>
    public static IShorteningStrategy RightTruncate { get; } = new RightTruncatingPathShorteningStrategy();

    /// <summary>
    /// Gets the strategy that retains one character from every non-terminal segment.
    /// </summary>
    /// <example>
    /// <code>
    /// var display = Shortener.Apply("archives/2026/report.pdf", new() { Strategy = Shortener.SegmentInitials });
    /// </code>
    /// </example>
    public static IShorteningStrategy SegmentInitials { get; } = new SegmentInitialPathShorteningStrategy();

    /// <summary>
    /// Gets the strategy that retains a configured prefix from every non-terminal segment.
    /// </summary>
    /// <example>
    /// <code>
    /// var display = Shortener.Apply("archives/2026/report.pdf", new() { Strategy = Shortener.SegmentPrefixes });
    /// </code>
    /// </example>
    public static IShorteningStrategy SegmentPrefixes { get; } = new SegmentPrefixPathShorteningStrategy();

    /// <summary>
    /// Gets the strategy that creates initials from camel-case words in every non-terminal segment.
    /// </summary>
    /// <example>
    /// <code>
    /// var display = Shortener.Apply(
    ///     "FirstProduct/Items/PriceDiscount/aaa.json",
    ///     new() { Strategy = Shortener.CamelCaseInitials });
    /// // "FP/I/PD/aaa.json"
    /// </code>
    /// </example>
    public static IShorteningStrategy CamelCaseInitials { get; } = new CamelCaseInitialPathShorteningStrategy();

    /// <summary>
    /// Gets the strategy that progressively shortens parent segments before left-truncating only when necessary.
    /// </summary>
    /// <example>
    /// <code>
    /// var display = Shortener.Apply("archives/2026/report.pdf", new() { Strategy = Shortener.Adaptive });
    /// </code>
    /// </example>
    public static IShorteningStrategy Adaptive { get; } = new AdaptivePathShorteningStrategy();

    /// <summary>
    /// Applies the supplied shortening options to a value.
    /// </summary>
    /// <param name="value">The value to shorten.</param>
    /// <param name="options">The configured length, separator, placeholder, and strategy.</param>
    /// <returns>A compact value that fits the configured maximum length.</returns>
    /// <example>
    /// <code>
    /// var display = Shortener.Apply(
    ///     "logs/production/api/request-2026-07-16.json",
    ///     new PathShorteningOptions { MaximumLength = 32 });
    /// </code>
    /// </example>
    public static string Apply(string value, ShorteningOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);
        EnsureValid(options);

        return value.Length <= options.MaximumLength
            ? value
            : (options.Strategy ?? Adaptive).Apply(value, options);
    }

    /// <summary>
    /// Applies the adaptive strategy with the supplied display parameters.
    /// </summary>
    /// <param name="value">The value to shorten.</param>
    /// <param name="maximumLength">The maximum number of characters in the result.</param>
    /// <param name="separator">The segment separator; defaults to <c>/</c>.</param>
    /// <param name="placeholder">The truncation marker; defaults to <c>...</c>.</param>
    /// <returns>A compact value that fits the supplied maximum length.</returns>
    /// <example>
    /// <code>
    /// var display = Shortener.Apply("Company.Product.Feature.Handler", 30, ".");
    /// </code>
    /// </example>
    public static string Apply(string value, int maximumLength, string separator = "/", string placeholder = "...") =>
        Apply(value, new ShorteningOptions
        {
            MaximumLength = maximumLength,
            Separator = separator,
            Placeholder = placeholder
        });

    internal static string TruncateLeft(string value, ShorteningOptions options) =>
        Truncate(value, options, preserveEnd: true);

    internal static string TruncateRight(string value, ShorteningOptions options) =>
        Truncate(value, options, preserveEnd: false);

    internal static string TruncateOverflow(string value, ShorteningOptions options) =>
        options.OverflowTruncation is ShorteningOverflowTruncation.Right
            ? TruncateRight(value, options)
            : TruncateLeft(value, options);

    internal static string ShortenSegments(string value, ShorteningOptions options, int segmentPrefixLength)
    {
        var segments = value.Split([options.Separator], StringSplitOptions.None);
        var terminalIndex = Array.FindLastIndex(segments, segment => !string.IsNullOrEmpty(segment));
        if (terminalIndex <= 0)
        {
            return value;
        }

        for (var index = 0; index < terminalIndex; index++)
        {
            if (!string.IsNullOrEmpty(segments[index]))
            {
                segments[index] = segments[index][..Math.Min(segmentPrefixLength, segments[index].Length)];
            }
        }

        return string.Join(options.Separator, segments);
    }

    internal static string ShortenCamelCaseSegments(string value, ShorteningOptions options)
    {
        var segments = value.Split([options.Separator], StringSplitOptions.None);
        var terminalIndex = Array.FindLastIndex(segments, segment => !string.IsNullOrEmpty(segment));
        if (terminalIndex <= 0)
        {
            return value;
        }

        for (var index = 0; index < terminalIndex; index++)
        {
            if (!string.IsNullOrEmpty(segments[index]))
            {
                segments[index] = CreateCamelCaseInitials(segments[index]);
            }
        }

        return string.Join(options.Separator, segments);
    }

    internal static void EnsureValid(ShorteningOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.MaximumLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The maximum length must be greater than or equal to zero.");
        }

        if (string.IsNullOrEmpty(options.Separator))
        {
            throw new ArgumentException("The separator must not be null or empty.", nameof(options));
        }

        if (options.SegmentPrefixLength < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The segment prefix length must be at least one.");
        }

        if (!Enum.IsDefined(options.OverflowTruncation))
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The overflow truncation mode is invalid.");
        }
    }

    private static string Truncate(string value, ShorteningOptions options, bool preserveEnd)
    {
        if (value.Length <= options.MaximumLength)
        {
            return value;
        }

        if (options.MaximumLength == 0)
        {
            return string.Empty;
        }

        var placeholder = options.Placeholder ?? string.Empty;
        if (placeholder.Length >= options.MaximumLength)
        {
            return placeholder[..options.MaximumLength];
        }

        var retainedLength = options.MaximumLength - placeholder.Length;
        return preserveEnd
            ? $"{placeholder}{value[^retainedLength..]}"
            : $"{value[..retainedLength]}{placeholder}";
    }

    private static string CreateCamelCaseInitials(string value)
    {
        var initials = new System.Text.StringBuilder(value.Length);
        var previousWasWordCharacter = false;

        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            if (!char.IsLetterOrDigit(current))
            {
                previousWasWordCharacter = false;
                continue;
            }

            var previous = index > 0 ? value[index - 1] : '\0';
            var next = index + 1 < value.Length ? value[index + 1] : '\0';
            var isWordStart = !previousWasWordCharacter ||
                char.IsDigit(current) && !char.IsDigit(previous) ||
                char.IsUpper(current) && (!char.IsUpper(previous) || char.IsLower(next));

            if (isWordStart)
            {
                initials.Append(current);
            }

            previousWasWordCharacter = true;
        }

        return initials.Length > 0 ? initials.ToString() : value[..1];
    }
}

/// <summary>
/// Truncates a value from the left, retaining its terminal characters.
/// </summary>
/// <example>
/// <code>
/// var display = new LeftTruncatingPathShorteningStrategy().Apply("archives/2026/report.pdf", new() { MaximumLength = 18 });
/// </code>
/// </example>
public sealed class LeftTruncatingPathShorteningStrategy : IShorteningStrategy
{
    /// <inheritdoc />
    public string Apply(string value, ShorteningOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);
        Shortener.EnsureValid(options);
        return Shortener.TruncateLeft(value, options);
    }
}

/// <summary>
/// Truncates a value from the right, retaining its initial characters.
/// </summary>
/// <example>
/// <code>
/// var display = new RightTruncatingPathShorteningStrategy().Apply("archives/2026/report.pdf", new() { MaximumLength = 18 });
/// </code>
/// </example>
public sealed class RightTruncatingPathShorteningStrategy : IShorteningStrategy
{
    /// <inheritdoc />
    public string Apply(string value, ShorteningOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);
        Shortener.EnsureValid(options);
        return Shortener.TruncateRight(value, options);
    }
}

/// <summary>
/// Replaces every non-terminal segment with its first character, then left-truncates only if necessary.
/// </summary>
/// <example>
/// <code>
/// var display = new SegmentInitialPathShorteningStrategy().Apply("archives/2026/report.pdf", new() { MaximumLength = 18 });
/// </code>
/// </example>
public sealed class SegmentInitialPathShorteningStrategy : IShorteningStrategy
{
    /// <inheritdoc />
    public string Apply(string value, ShorteningOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);
        Shortener.EnsureValid(options);
        var shortened = Shortener.ShortenSegments(value, options, 1);
        return shortened.Length <= options.MaximumLength
            ? shortened
            : Shortener.TruncateOverflow(shortened, options);
    }
}

/// <summary>
/// Replaces every non-terminal segment with a configurable prefix, then left-truncates only if necessary.
/// </summary>
/// <example>
/// <code>
/// var display = new SegmentPrefixPathShorteningStrategy().Apply(
///     "archives/2026/report.pdf",
///     new() { MaximumLength = 22, SegmentPrefixLength = 2 });
/// </code>
/// </example>
public sealed class SegmentPrefixPathShorteningStrategy : IShorteningStrategy
{
    /// <inheritdoc />
    public string Apply(string value, ShorteningOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);
        Shortener.EnsureValid(options);
        var shortened = Shortener.ShortenSegments(value, options, options.SegmentPrefixLength);
        return shortened.Length <= options.MaximumLength
            ? shortened
            : Shortener.TruncateOverflow(shortened, options);
    }
}

/// <summary>
/// Replaces every non-terminal segment with initials derived from its camel-case words, then left-truncates only if necessary.
/// </summary>
/// <example>
/// <code>
/// var display = new CamelCaseInitialPathShorteningStrategy().Apply(
///     "FirstProduct/Items/PriceDiscount/aaa.json",
///     new() { MaximumLength = 20 });
/// // "FP/I/PD/aaa.json"
/// </code>
/// </example>
public sealed class CamelCaseInitialPathShorteningStrategy : IShorteningStrategy
{
    /// <inheritdoc />
    public string Apply(string value, ShorteningOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);
        Shortener.EnsureValid(options);
        var shortened = Shortener.ShortenCamelCaseSegments(value, options);
        return shortened.Length <= options.MaximumLength
            ? shortened
            : Shortener.TruncateOverflow(shortened, options);
    }
}

/// <summary>
/// Progressively reduces non-terminal segment prefixes before using left truncation as a final fallback.
/// </summary>
/// <example>
/// <code>
/// var display = new AdaptivePathShorteningStrategy().Apply(
///     "archives/2026/july/report.pdf",
///     new() { MaximumLength = 20, SegmentPrefixLength = 3 });
/// </code>
/// </example>
public sealed class AdaptivePathShorteningStrategy : IShorteningStrategy
{
    /// <inheritdoc />
    public string Apply(string value, ShorteningOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);
        Shortener.EnsureValid(options);

        for (var prefixLength = options.SegmentPrefixLength; prefixLength >= 2; prefixLength--)
        {
            var shortened = Shortener.ShortenSegments(value, options, prefixLength);
            if (shortened.Length <= options.MaximumLength)
            {
                return shortened;
            }
        }

        var camelCaseInitials = Shortener.ShortenCamelCaseSegments(value, options);
        if (camelCaseInitials.Length <= options.MaximumLength)
        {
            return camelCaseInitials;
        }

        var initials = Shortener.ShortenSegments(value, options, 1);
        return initials.Length <= options.MaximumLength
            ? initials
            : Shortener.TruncateOverflow(initials, options);
    }
}
