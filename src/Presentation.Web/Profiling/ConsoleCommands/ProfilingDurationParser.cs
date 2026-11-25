// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Common;

/// <summary>Parses profiling command durations without extending the shared command binder.</summary>
/// <example><code>var ok = ProfilingDurationParser.TryParse("500ms", out var duration);</code></example>
public static class ProfilingDurationParser
{
    /// <summary>Parses standard <see cref="TimeSpan"/> text or a number with ms, s, m, or h.</summary>
    /// <param name="value">The command option value.</param>
    /// <param name="duration">The parsed duration when successful, or <see cref="TimeSpan.Zero"/> on failure.</param>
    /// <returns><see langword="true"/> when the value is a supported finite duration.</returns>
    /// <example><code>ProfilingDurationParser.TryParse("1.5s", out var duration);</code></example>
    public static bool TryParse(string value, out TimeSpan duration) => value.TryParseDuration(out duration);
}
