// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Owns one atomic reservation across independent native/transient and hierarchical resident budgets.</summary>
/// <remarks>Pass the most specific resident child once; its parents are charged by that budget. Independent quota counters may accompany it.</remarks>
/// <example><code>if (ResourceCharge.TryReserve(bufferSize, out var charge, transientBudget, residentBudget)) { using (charge) { ReadBuffer(); } }</code></example>
public sealed class ResourceCharge : IDisposable
{
    private ResourceReservation[] reservations;
    private readonly Lock gate = new();

    private ResourceCharge(ResourceReservation[] reservations) => this.reservations = reservations;

    /// <summary>Reserves all applicable finite budgets before allocation, rolling back every earlier reservation on rejection.</summary>
    /// <remarks>Supply each independent budget once. Do not supply both a parent and its descendant; descendants already charge their parents.</remarks>
    /// <example><code>if (ResourceCharge.TryReserve(1024, out var owned, transient, resident)) { using (owned) { ReadBuffer(); } }</code></example>
    public static bool TryReserve(long amount, out ResourceCharge charge, params ResourceBudget[] budgets)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        ArgumentNullException.ThrowIfNull(budgets);
        if (budgets.Length == 0 || budgets.Any(b => b is null))
        {
            throw new ArgumentException("At least one finite non-null budget is required.", nameof(budgets));
        }

        var reservations = new ResourceReservation[budgets.Length];
        for (var i = 0; i < budgets.Length; i++)
        {
            if (!budgets[i].TryReserve(amount, out reservations[i]))
            {
                foreach (var reservation in reservations)
                {
                    reservation?.Dispose();
                }

                charge = null;
                return false;
            }
        }

        charge = new(reservations);
        return true;
    }

    /// <summary>Reduces all independent charges after temporary resources are released, retaining only the actual output's conservative charge.</summary>
    /// <example><code>ReleaseScratchBuffer(); charge.ReduceTo(retainedBytes);</code></example>
    public void ReduceTo(long amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        lock (this.gate)
        {
            ObjectDisposedException.ThrowIf(this.reservations is null, this);
            if (amount > this.reservations[0].Amount) { throw new ArgumentOutOfRangeException(nameof(amount)); }

            foreach (var reservation in this.reservations) { reservation.ReduceTo(amount); }
        }
    }

    /// <summary>Releases this owned charge exactly once, after actual native work or retained ownership ends.</summary>
    /// <example><code>ReleaseBuffer(); charge.Dispose();</code></example>
    public void Dispose()
    {
        ResourceReservation[] owned;
        lock (this.gate) { owned = this.reservations; this.reservations = null; }

        if (owned is null)
        {
            return;
        }

        foreach (var reservation in owned)
        {
            reservation.Dispose();
        }
    }
}