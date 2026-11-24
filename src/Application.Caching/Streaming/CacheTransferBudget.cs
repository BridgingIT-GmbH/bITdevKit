// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Applies collection transfer limits to the shared inactivity and absolute-lifetime deadline.</summary>
/// <remarks>Expiry requests cancellation once; successful progress renews only inactivity.</remarks>
/// <example><code>using var budget = new CacheTransferBudget(time, options, StopTransfer); budget.Start();</code></example>
public sealed class CacheTransferBudget : IDisposable
{
    private readonly TransferDeadline deadline;

    /// <summary>Captures collection limits without scheduling the initial deadline check.</summary>
    /// <param name="time">The monotonic clock and timer factory.</param>
    /// <param name="options">The collection's finite idle timeout and absolute lifetime.</param>
    /// <param name="expired">The terminal callback, invoked once even if cancellation callbacks fail.</param>
    /// <example><code>using var budget = new CacheTransferBudget(time, options, StopTransfer);</code></example>
    public CacheTransferBudget(TimeProvider time, CacheStreamOptions options, Action expired)
    {
        this.deadline = new(options.IdleTimeout, options.Lifetime, expired, time);
    }

    /// <summary>Schedules checking without moving the original construction-time lifetime.</summary>
    public void Start() => this.deadline.Start();

    /// <summary>Gets cancellation signaled when either transfer deadline expires.</summary>
    public CancellationToken Token => this.deadline.Token;

    /// <summary>Records successful progress and renews only the inactivity deadline.</summary>
    public void Progress() => this.deadline.Progress();

    /// <summary>Checks and reports whether authoritative transfer expiry has occurred.</summary>
    public bool HasExpired => this.deadline.HasExpired;

    /// <summary>Disposes timer ownership after the transfer's actual work ends.</summary>
    public void Dispose() => this.deadline.Dispose();
}