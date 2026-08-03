// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Configures a <see cref="Shortener" /> operation.
/// </summary>
/// <example>
/// <code>
/// var options = new PathShorteningOptions
/// {
///     MaximumLength = 36,
///     Separator = "/",
///     Placeholder = "...",
///     Strategy = Shortener.Adaptive
/// };
/// </code>
/// </example>
public sealed record ShorteningOptions
{
    /// <summary>
    /// Gets the maximum number of characters in the shortened value.
    /// </summary>
    /// <example>
    /// <code>
    /// var maximumLength = options.MaximumLength;
    /// </code>
    /// </example>
    public int MaximumLength { get; init; } = 80;

    /// <summary>
    /// Gets the separator that divides parent segments from the terminal segment.
    /// </summary>
    /// <example>
    /// <code>
    /// options = options with { Separator = "." };
    /// </code>
    /// </example>
    public string Separator { get; init; } = "/";

    /// <summary>
    /// Gets the marker inserted when truncation removes characters. Set this to an empty string to omit a marker.
    /// </summary>
    /// <example>
    /// <code>
    /// options = options with { Placeholder = ".." };
    /// </code>
    /// </example>
    public string Placeholder { get; init; } = "...";

    /// <summary>
    /// Gets the number of characters retained from each non-terminal segment by prefix-based strategies.
    /// </summary>
    /// <example>
    /// <code>
    /// options = options with { SegmentPrefixLength = 2 };
    /// </code>
    /// </example>
    public int SegmentPrefixLength { get; init; } = 3;

    /// <summary>
    /// Gets the truncation direction used when a segment-based strategy remains too long after abbreviation.
    /// </summary>
    /// <example>
    /// <code>
    /// options = options with { OverflowTruncation = PathShorteningOverflowTruncation.Right };
    /// </code>
    /// </example>
    public ShorteningOverflowTruncation OverflowTruncation { get; init; } = ShorteningOverflowTruncation.Left;

    /// <summary>
    /// Gets the strategy that creates the compact display value.
    /// </summary>
    /// <example>
    /// <code>
    /// options = options with { Strategy = Shortener.SegmentInitials };
    /// </code>
    /// </example>
    public IShorteningStrategy Strategy { get; init; } = Shortener.Adaptive;
}
