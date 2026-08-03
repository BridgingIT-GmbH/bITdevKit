// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents an explicit expiration mutation without nullable-value ambiguity.
/// </summary>
/// <example>
/// <code>
/// var preserve = ExpirationChange.Preserve;
/// var expiresTomorrow = ExpirationChange.At(DateTimeOffset.UtcNow.AddDays(1));
/// var clear = ExpirationChange.Clear;
/// </code>
/// </example>
public sealed record ExpirationChange
{
    private ExpirationChange(ExpirationChangeMode mode, DateTimeOffset? expiresAt = null, TimeSpan? timeToLive = null)
    {
        this.Mode = mode;
        this.ExpiresAt = expiresAt;
        this.TimeToLive = timeToLive;
    }

    /// <summary>Gets a change that preserves the current expiration.</summary>
    public static ExpirationChange Preserve { get; } = new(ExpirationChangeMode.Preserve);

    /// <summary>Gets a change that clears the current expiration.</summary>
    public static ExpirationChange Clear { get; } = new(ExpirationChangeMode.Clear);

    /// <summary>Gets the change mode.</summary>
    public ExpirationChangeMode Mode { get; }

    /// <summary>Gets the absolute expiration for <see cref="ExpirationChangeMode.Set"/>.</summary>
    public DateTimeOffset? ExpiresAt { get; }

    /// <summary>Gets the relative duration for <see cref="ExpirationChangeMode.After"/>.</summary>
    public TimeSpan? TimeToLive { get; }

    /// <summary>Creates an absolute expiration change.</summary>
    /// <param name="expiresAt">The expiration timestamp.</param>
    /// <returns>The expiration change.</returns>
    /// <example><code>var change = ExpirationChange.At(DateTimeOffset.UtcNow.AddHours(1));</code></example>
    public static ExpirationChange At(DateTimeOffset expiresAt) => new(ExpirationChangeMode.Set, expiresAt);

    /// <summary>Creates a relative expiration change.</summary>
    /// <param name="timeToLive">The duration from the operation time.</param>
    /// <returns>The expiration change.</returns>
    /// <example><code>var change = ExpirationChange.After(TimeSpan.FromMinutes(30));</code></example>
    public static ExpirationChange After(TimeSpan timeToLive)
    {
        if (timeToLive <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeToLive), "Time to live must be greater than zero.");
        }

        return new ExpirationChange(ExpirationChangeMode.After, timeToLive: timeToLive);
    }
}
