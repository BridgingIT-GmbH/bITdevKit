// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Defines a strategy for rendering a segmented value within a fixed character budget.
/// </summary>
/// <example>
/// <code>
/// var text = new SegmentInitialPathShorteningStrategy().Apply(
///     "archives/2026/july/report.pdf",
///     new PathShorteningOptions { MaximumLength = 20 });
/// </code>
/// </example>
public interface IShorteningStrategy
{
    /// <summary>
    /// Applies the supplied shortening options to a value.
    /// </summary>
    /// <param name="value">The value to shorten.</param>
    /// <param name="options">The configured length, separator, and placeholder options.</param>
    /// <returns>A value whose length does not exceed <see cref="ShorteningOptions.MaximumLength" />.</returns>
    /// <example>
    /// <code>
    /// var result = strategy.Apply("reports/2026/summary.pdf", options);
    /// </code>
    /// </example>
    string Apply(string value, ShorteningOptions options);
}
