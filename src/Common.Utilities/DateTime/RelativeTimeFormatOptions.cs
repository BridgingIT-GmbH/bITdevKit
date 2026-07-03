// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Globalization;

/// <summary>
/// Configures relative-time formatting.
/// </summary>
/// <remarks><example><code>var options = new RelativeTimeFormatOptions { UseShortUnits = true };</code></example></remarks>
public sealed record RelativeTimeFormatOptions
{
    /// <summary>Gets the culture used for language selection.</summary>
    public CultureInfo Culture { get; init; } = CultureInfo.CurrentUICulture;

    /// <summary>Gets the maximum number of units. Currently single-unit output is used.</summary>
    public int MaxUnits { get; init; } = 1;

    /// <summary>Gets the smallest unit allowed in output.</summary>
    public RelativeTimeUnit MinimumUnit { get; init; } = RelativeTimeUnit.Millisecond;

    /// <summary>Gets the rounding mode.</summary>
    public RelativeTimeRoundingMode RoundingMode { get; init; } = RelativeTimeRoundingMode.Floor;

    /// <summary>Gets a value indicating whether relative suffixes are included.</summary>
    public bool UseSuffix { get; init; } = true;

    /// <summary>Gets a value indicating whether short unit names are used.</summary>
    public bool UseShortUnits { get; init; }

    /// <summary>Gets the threshold that formats relative values as now.</summary>
    public TimeSpan NowThreshold { get; init; } = TimeSpan.FromSeconds(5);
}
