// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Charges finite owned capacity through a parent/child budget hierarchy.</summary>
/// <remarks>This accounts owned capacity and conservative metadata/graph estimates. It is not a hard limit on the managed heap.</remarks>
/// <example><code>if (budget.TryReserve(capacity, out var reservation)) { using (reservation) { UseBuffer(); } }</code></example>
public sealed class ResourceBudget
{
    private readonly ResourceBudget parent;
    private long used;

    /// <summary>Creates a positive finite budget with an optional smaller-applicable-parent ceiling.</summary>
    /// <example><code>var budget = new ResourceBudget("uploads", ByteSize.Megabytes(8), processBudget);</code></example>
    public ResourceBudget(string name, long maximum, ResourceBudget parent = null)
    {
        this.Name = name ?? throw new ArgumentNullException(nameof(name));
        this.Maximum = maximum > 0 ? maximum : throw new ArgumentOutOfRangeException(nameof(maximum));
        this.parent = parent;
    }

    /// <summary>Gets the caller-defined diagnostic category.</summary>
    /// <example><code>logger.LogInformation("Budget {Name}", budget.Name);</code></example>
    public string Name { get; }

    /// <summary>Gets this budget's finite capacity.</summary>
    /// <example><code>var remaining = budget.Maximum - budget.Used;</code></example>
    public long Maximum { get; }

    /// <summary>Gets current owned/reserved charges.</summary>
    /// <example><code>var occupiedBytes = budget.Used;</code></example>
    public long Used => Interlocked.Read(ref this.used);

    /// <summary>Atomically reserves each applicable ceiling, rolling back parents if a child rejects admission.</summary>
    /// <example><code>if (budget.TryReserve(1024, out var owned)) { using (owned) { AllocateBuffer(); } }</code></example>
    public bool TryReserve(long amount, out ResourceReservation reservation)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        if (!this.TryCharge(amount))
        {
            reservation = null;
            return false;
        }

        reservation = new(this, amount);
        return true;
    }

    /// <summary>Attempts an atomic raw charge across all applicable parent and child ceilings, rolling back a parent when this child rejects it.</summary>
    /// <remarks>The caller owns a successful charge and must return it to this same budget. Prefer <see cref="TryReserve" /> for disposable ownership.</remarks>
    /// <example><code>if (budget.TryCharge(1024)) { try { AllocateBuffer(); } finally { budget.Return(1024); } }</code></example>
    public bool TryCharge(long amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        if (this.parent is not null && !this.parent.TryCharge(amount))
        {
            return false;
        }

        while (true)
        {
            var current = Interlocked.Read(ref this.used);
            if (amount > this.Maximum - current)
            {
                this.parent?.Return(amount);
                return false;
            }

            if (Interlocked.CompareExchange(ref this.used, current + amount, current) == current)
            {
                return true;
            }
        }
    }

    /// <summary>Returns a caller-owned raw charge to this budget and its parents without changing counters on underflow.</summary>
    /// <remarks>Pair this with a successful <see cref="TryCharge" /> on the same budget. Prefer <see cref="TryReserve" /> for disposable ownership.</remarks>
    /// <example><code>budget.Return(previouslyChargedBytes);</code></example>
    public void Return(long amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        while (true)
        {
            var current = Interlocked.Read(ref this.used);
            if (amount > current)
            {
                throw new InvalidOperationException("Resource accounting released more capacity than it owned.");
            }

            if (Interlocked.CompareExchange(ref this.used, current - amount, current) == current)
            {
                break;
            }
        }

        this.parent?.Return(amount);
    }
}