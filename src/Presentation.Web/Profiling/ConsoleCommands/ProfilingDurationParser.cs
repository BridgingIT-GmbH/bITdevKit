// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using System.Globalization;

/// <summary>Parses profiling command durations without extending the shared command binder.</summary>
/// <example><code>var ok = ProfilingDurationParser.TryParse("500ms", out var duration);</code></example>
public static class ProfilingDurationParser
{
    /// <summary>Parses standard <see cref="TimeSpan"/> text or a number with ms, s, m, or h.</summary>
    /// <param name="value">The command option value.</param>
    /// <param name="duration">The parsed duration when successful.</param>
    /// <returns><see langword="true"/> when the value is a supported finite duration.</returns>
    /// <example><code>ProfilingDurationParser.TryParse("1.5s", out var duration);</code></example>
    public static bool TryParse(string value, out TimeSpan duration)
    {
        duration = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();
        if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out duration))
        {
            return true;
        }

        var suffixLength = text.EndsWith("ms", StringComparison.OrdinalIgnoreCase) ? 2 : 1;
        if (text.Length <= suffixLength)
        {
            return false;
        }

        var suffix = text[^suffixLength..].ToLowerInvariant();
        if (suffix is not ("ms" or "s" or "m" or "h"))
        {
            return false;
        }

        if (
            !double.TryParse(
                text[..^suffixLength],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var amount
            )
            || !double.IsFinite(amount)
        )
        {
            return false;
        }

        try
        {
            duration = suffix switch
            {
                "ms" => TimeSpan.FromMilliseconds(amount),
                "s" => TimeSpan.FromSeconds(amount),
                "m" => TimeSpan.FromMinutes(amount),
                "h" => TimeSpan.FromHours(amount),
                _ => default,
            };
            return true;
        }
        catch (OverflowException)
        {
            duration = default;
            return false;
        }
    }
}