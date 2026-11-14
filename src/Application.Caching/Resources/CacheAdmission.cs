// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Maps shared hierarchical resource admission to cache capacity and coordination outcomes.</summary>
/// <remarks>Queueing, fairness and occupied lease ownership are provided by Common resource admission.</remarks>
/// <example><code>using var lease = (await admission.AcquireAsync(TimeSpan.FromSeconds(5), cancellationToken)).Value;</code></example>
public sealed class CacheAdmission
{
    private readonly ResourceAdmission admission;

    /// <summary>Creates a cache admission group sharing its parent's clock and root queue.</summary>
    /// <example><code>var group = new CacheAdmission("sources", 4, 16, processSources);</code></example>
    public CacheAdmission(string name, int maximumActive, int maximumQueued, CacheAdmission parent = null, TimeProvider timeProvider = null)
    {
        this.admission = new(name, maximumActive, maximumQueued, parent?.admission, timeProvider);
    }

    /// <summary>Gets the stable resource category.</summary>
    public string Name => this.admission.Name;

    /// <summary>Gets the occupied-operation ceiling.</summary>
    public int MaximumActive => this.admission.MaximumActive;

    /// <summary>Gets the finite queue ceiling.</summary>
    public int MaximumQueued => this.admission.MaximumQueued;

    /// <summary>Gets actually occupied work, including work which ignores cancellation.</summary>
    public int Active => this.admission.Active;

    /// <summary>Gets queued admissions for this group and its descendants.</summary>
    public int Queued => this.admission.Queued;

    /// <summary>Attempts immediate admission without queueing.</summary>
    /// <example><code>if (admission.TryAcquire(out var owned)) { using (owned) { Execute(); } }</code></example>
    public bool TryAcquire(out CacheAdmissionLease lease)
    {
        if (!this.admission.TryAcquire(out var owned))
        {
            lease = null;
            return false;
        }

        lease = CacheAdmissionLease.FromShared(owned);
        return true;
    }

    /// <summary>Acquires bounded capacity with cache-specific failure classification; caller cancellation propagates.</summary>
    /// <example><code>var result = await admission.AcquireAsync(TimeSpan.FromSeconds(5), cancellationToken);</code></example>
    public async ValueTask<Result<CacheAdmissionLease>> AcquireAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var result = await this.admission.AcquireAsync(timeout, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess) { return Result<CacheAdmissionLease>.Success(CacheAdmissionLease.FromShared(result.Value)); }

        var error = result.Errors.First();
        return Result<CacheAdmissionLease>.Failure(new CacheError(error is QuotaExceededError ? CacheErrorCode.Capacity :
            CacheErrorCode.CoordinationTimeout, error.Message));
    }

    /// <summary>Returns exactly one caller-owned occupied unit after actual work ends.</summary>
    /// <example><code>admission.Release();</code></example>
    public void Release() => this.admission.Release();
}

/// <summary>Owns cache admission until execution or transfer actually terminates.</summary>
/// <example><code>using var lease = result.Value;</code></example>
public sealed class CacheAdmissionLease : IDisposable
{
    private CacheAdmission owner;
    private ResourceAdmissionLease shared;

    private CacheAdmissionLease() { }

    /// <summary>Takes ownership of one already admitted raw cache unit.</summary>
    /// <example><code>using var owned = new CacheAdmissionLease(alreadyAdmittedGroup);</code></example>
    public CacheAdmissionLease(CacheAdmission owner) => this.owner = owner;

    /// <summary>Takes ownership of an already acquired shared admission lease without charging again.</summary>
    /// <param name="shared">The shared lease whose existing occupied unit is transferred to this adapter.</param>
    /// <returns>The caller-owned cache lease wrapping the existing admission.</returns>
    /// <example><code>using var owned = CacheAdmissionLease.FromShared(sharedLease);</code></example>
    public static CacheAdmissionLease FromShared(ResourceAdmissionLease shared) => new() { shared = shared };

    /// <summary>Returns occupied work once; caller cancellation alone does not release it.</summary>
    /// <example><code>lease.Dispose();</code></example>
    public void Dispose()
    {
        Interlocked.Exchange(ref this.shared, null)?.Dispose();
        Interlocked.Exchange(ref this.owner, null)?.Release();
    }
}