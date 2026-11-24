// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Benchmarks.Caching;

/// <summary>Advances authoritative UTC validity while preserving real elapsed-time measurement and real cancellation timers.</summary>
/// <remarks>Advance only between completed cache operations. This clock exercises logical expiration without waiting for a TTL in real time; it does not simulate scheduler timing or clock-skew recovery.</remarks>
/// <example><code>var clock = new CacheBenchmarkClock(); clock.Advance(TimeSpan.FromSeconds(20));</code></example>
public sealed class CacheBenchmarkClock : TimeProvider
{
    private long advancedTicks;

    /// <summary>Gets the cumulative validity offset, bounded to ten years for one benchmark host.</summary>
    public long AdvancedTicks => Interlocked.Read(ref this.advancedTicks);

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => TimeProvider.System.GetUtcNow().AddTicks(this.AdvancedTicks);

    /// <inheritdoc />
    public override long GetTimestamp() => TimeProvider.System.GetTimestamp();

    /// <inheritdoc />
    public override long TimestampFrequency => TimeProvider.System.TimestampFrequency;

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object state, TimeSpan dueTime, TimeSpan period) =>
        TimeProvider.System.CreateTimer(callback, state, dueTime, period);

    /// <summary>Moves subsequent UTC validity observations forward after all previous source/read/write work has completed.</summary>
    /// <param name="duration">A nonnegative advance of at most one hour, within the host's cumulative ten-year bound.</param>
    /// <exception cref="ArgumentOutOfRangeException">The requested advance exceeds a finite bound.</exception>
    public void Advance(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero || duration > TimeSpan.FromHours(1)) { throw new ArgumentOutOfRangeException(nameof(duration)); }

        while (true)
        {
            var before = this.AdvancedTicks;
            var after = checked(before + duration.Ticks);
            if (after > TimeSpan.FromDays(3650).Ticks) { throw new ArgumentOutOfRangeException(nameof(duration)); }

            if (Interlocked.CompareExchange(ref this.advancedTicks, after, before) == before) { return; }
        }
    }
}