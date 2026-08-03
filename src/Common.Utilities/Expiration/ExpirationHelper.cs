// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Resolves and evaluates provider-neutral expiration values.
/// </summary>
/// <example>
/// <code>
/// var expiresAt = ExpirationHelper.Resolve(ExpirationChange.After(TimeSpan.FromHours(1)), null, TimeProvider.System);
/// </code>
/// </example>
public static class ExpirationHelper
{
    /// <summary>Resolves an expiration change against the current value and clock.</summary>
    /// <param name="change">The requested change.</param>
    /// <param name="current">The current expiration value.</param>
    /// <param name="timeProvider">The operation clock.</param>
    /// <returns>The resolved UTC expiration, or null for no expiration.</returns>
    /// <example><code>var value = ExpirationHelper.Resolve(ExpirationChange.Clear, current, TimeProvider.System);</code></example>
    public static DateTimeOffset? Resolve(
        ExpirationChange change,
        DateTimeOffset? current,
        TimeProvider timeProvider = null)
    {
        change ??= ExpirationChange.Preserve;
        timeProvider ??= TimeProvider.System;

        return change.Mode switch
        {
            ExpirationChangeMode.Preserve => NormalizeUtc(current),
            ExpirationChangeMode.Clear => null,
            ExpirationChangeMode.Set => NormalizeUtc(change.ExpiresAt),
            ExpirationChangeMode.After => timeProvider.GetUtcNow().Add(
                change.TimeToLive ?? throw new InvalidOperationException("Relative expiration requires a duration.")),
            _ => throw new ArgumentOutOfRangeException(nameof(change), change.Mode, "Unsupported expiration change mode.")
        };
    }

    /// <summary>Normalizes an optional timestamp to UTC.</summary>
    /// <param name="value">The timestamp.</param>
    /// <returns>The UTC timestamp.</returns>
    /// <example><code>var utc = ExpirationHelper.NormalizeUtc(value);</code></example>
    public static DateTimeOffset? NormalizeUtc(DateTimeOffset? value) => value?.ToUniversalTime();

    /// <summary>Determines whether an expiration is due at the supplied cutoff.</summary>
    /// <param name="expiresAt">The expiration timestamp.</param>
    /// <param name="cutoff">The inclusive cutoff.</param>
    /// <returns>True when the expiration is due.</returns>
    /// <example><code>var due = ExpirationHelper.IsDue(expiresAt, TimeProvider.System.GetUtcNow());</code></example>
    public static bool IsDue(DateTimeOffset? expiresAt, DateTimeOffset cutoff) =>
        expiresAt is not null && expiresAt.Value <= cutoff;
}
