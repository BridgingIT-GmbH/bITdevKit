// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Auditing;

/// <summary>Provides nonblocking bounded attempt admission for this process.</summary>
/// <example><code>using var lease = limiter.TryAcquire();</code></example>
public sealed class AuditExportLimiter
{
    private readonly SemaphoreSlim slots;

    /// <summary>Captures a finite instance-wide active export limit.</summary>
    /// <example><code>var limiter = new AuditExportLimiter(new AuditExportOptions());</code></example>
    public AuditExportLimiter(AuditExportOptions options) { options.Validate(); this.slots = new(options.ActiveExportLimit, options.ActiveExportLimit); }

    /// <summary>Acquires immediately or returns null without queuing another waiter.</summary>
    /// <example><code>using var lease = limiter.TryAcquire(); if (lease is null) return Busy();</code></example>
    public IDisposable TryAcquire() => this.slots.Wait(0) ? new Lease(this.slots) : null;

    private sealed class Lease(SemaphoreSlim slots) : IDisposable
    {
        private int disposed;

        public void Dispose() { if (Interlocked.Exchange(ref this.disposed, 1) == 0) slots.Release(); }
    }
}
