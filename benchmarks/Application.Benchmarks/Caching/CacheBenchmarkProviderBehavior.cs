// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Benchmarks.Caching;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Common;

/// <summary>Counts actual provider-boundary work and optionally injects a named infrastructure failure into quota inspection.</summary>
/// <remarks>These are provider calls, not SQL statement or operating-system I/O counts. Controlled faults are limited to benchmark scenarios.</remarks>
/// <example><code>var observer = new CacheBenchmarkProviderBehavior(native);</code></example>
public sealed class CacheBenchmarkProviderBehavior(ICacheStoreProvider inner) : CacheStoreProviderBehavior(inner)
{
    private int acquisitions;
    private int validations;
    private int payloadLookups;
    private int quotaCalls;
    private int observers;
    private int maximumObservers;
    private TaskCompletionSource pending = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets the number of actual source-authority acquisitions.</summary>
    public int Acquisitions => Volatile.Read(ref this.acquisitions);

    /// <summary>Gets strict retained-version validation calls.</summary>
    public int Validations => Volatile.Read(ref this.validations);

    /// <summary>Gets payload-opening lookup requests.</summary>
    public int PayloadLookups => Volatile.Read(ref this.payloadLookups);

    /// <summary>Gets native quota calls, including explicit failure injections.</summary>
    public int QuotaCalls => Volatile.Read(ref this.quotaCalls);

    /// <summary>Gets peak concurrent completion observers on this node.</summary>
    public int MaximumObservers => Volatile.Read(ref this.maximumObservers);

    /// <summary>Gets the current scenario's first confirmed remote-pending ownership observation.</summary>
    public Task Pending => this.pending.Task;

    /// <summary>Gets or sets whether quota calls return a controlled infrastructure failure before native quota access.</summary>
    public bool FailQuota { get; set; }

    /// <summary>Resets the remote-pending barrier only after every previous scenario has completed.</summary>
    public void ResetPending() => this.pending = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <inheritdoc />
    public override async ValueTask<Result<CacheOwnershipObservation>> AcquireOwnershipAsync(CacheOwnershipRequest request, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref this.acquisitions);
        var result = await base.AcquireOwnershipAsync(request, cancellationToken);
        if (result.IsSuccess && result.Value.State == CacheOwnershipState.Pending) { this.pending.TrySetResult(); }

        return result;
    }

    /// <inheritdoc />
    public override async ValueTask<Result<CacheOwnershipObservation>> ObserveOwnershipAsync(CacheOwnershipRequest request, string flightId, CancellationToken cancellationToken = default)
    {
        var active = Interlocked.Increment(ref this.observers);
        for (var previous = this.MaximumObservers; active > previous; previous = this.MaximumObservers)
        {
            if (Interlocked.CompareExchange(ref this.maximumObservers, active, previous) == previous) { break; }
        }

        try { return await base.ObserveOwnershipAsync(request, flightId, cancellationToken); }
        finally { Interlocked.Decrement(ref this.observers); }
    }

    /// <inheritdoc />
    public override ValueTask<Result<CacheReadValidation>> ValidateReadAsync(CacheEntryIdentity identity, CacheEntryVersion version, CacheReadRequest request,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref this.validations);
        return base.ValidateReadAsync(identity, version, request, cancellationToken);
    }

    /// <inheritdoc />
    public override ValueTask<Result<CacheReadValidation>> LookupAsync(CacheEntryIdentity identity, CacheReadRequest request, CancellationToken cancellationToken = default)
    {
        if (request.OpenPayload) { Interlocked.Increment(ref this.payloadLookups); }

        return base.LookupAsync(identity, request, cancellationToken);
    }

    /// <inheritdoc />
    public override ValueTask<Result<CacheProviderQuota>> GetQuotaAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref this.quotaCalls);
        return this.FailQuota ? ValueTask.FromResult(Result<CacheProviderQuota>.Failure(new CacheError(CacheErrorCode.ProviderUnavailable,
            "The benchmark intentionally made native quota inspection unavailable."))) : base.GetQuotaAsync(cancellationToken);
    }
}