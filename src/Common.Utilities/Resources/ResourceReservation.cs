// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Owns one hierarchical capacity charge until the actual buffer, stream, pin, or native work stops.</summary>
/// <example><code>using var reservation = acquired; if (!reservation.TryGrow(additionalCapacity)) { RejectGrowth(); }</code></example>
public sealed class ResourceReservation : IDisposable
{
    private readonly Lock gate = new();
    private ResourceBudget budget;
    private long amount;

    /// <summary>Wraps an existing owned charge so its remaining capacity can be returned exactly once.</summary>
    /// <remarks>This does not charge the budget. Supply only a successful raw charge which has no other owner. Prefer <see cref="ResourceBudget.TryReserve" />.</remarks>
    /// <example><code>if (budget.TryCharge(1024)) { using var owned = new ResourceReservation(budget, 1024); AllocateBuffer(); }</code></example>
    public ResourceReservation(ResourceBudget budget, long amount)
    {
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        this.budget = budget;
        this.amount = amount;
    }

    /// <summary>Gets current reserved capacity. Disposed reservations report zero.</summary>
    /// <example><code>var allocatedBytes = reservation.Amount;</code></example>
    public long Amount
    {
        get
        {
            lock (this.gate)
            {
                return this.amount;
            }
        }
    }

    /// <summary>Reserves additional actual capacity before allocation. Growth never releases an existing charge.</summary>
    /// <example><code>if (reservation.TryGrow(1024)) { GrowBuffer(); }</code></example>
    public bool TryGrow(long additional)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(additional);
        lock (this.gate)
        {
            ObjectDisposedException.ThrowIf(this.budget is null, this);
            if (additional > long.MaxValue - this.amount || !this.budget.TryCharge(additional))
            {
                return false;
            }

            this.amount += additional;
            return true;
        }
    }

    /// <summary>Reduces the owned capacity after the corresponding temporary resources have actually been released. It cannot grow a reservation.</summary>
    /// <example><code>ReleaseScratchBuffer(); reservation.ReduceTo(retainedBytes);</code></example>
    public void ReduceTo(long amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        lock (this.gate)
        {
            ObjectDisposedException.ThrowIf(this.budget is null, this);
            if (amount > this.amount) { throw new ArgumentOutOfRangeException(nameof(amount), "Use TryGrow before allocating additional capacity."); }

            this.budget.Return(this.amount - amount);
            this.amount = amount;
        }
    }

    /// <summary>Releases the owned charge exactly once. Call only after the associated resources actually stop.</summary>
    /// <example><code>ReleaseBuffer(); reservation.Dispose();</code></example>
    public void Dispose()
    {
        lock (this.gate)
        {
            if (this.budget is null)
            {
                return;
            }

            this.budget.Return(this.amount);
            this.budget = null;
            this.amount = 0;
        }
    }
}