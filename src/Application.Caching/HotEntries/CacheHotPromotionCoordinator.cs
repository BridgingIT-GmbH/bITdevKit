// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Coalesces exact-version decode/promotion ownership independently of source-loading coordination.</summary>
/// <example><code>var active = store.Promotions.Count;</code></example>
public sealed class CacheHotPromotionCoordinator(CacheCollectionResources resources, int maximum)
{
    private readonly Lock gate = new();
    private readonly Dictionary<(CacheEntryIdentity Identity, CacheEntryVersion Version, Type Kind), object> active = [];

    /// <summary>Gets bounded active version-specific operations.</summary>
    public int Count { get { lock (this.gate) { return this.active.Count; } } }

    /// <summary>Joins or starts one shared decode. Rejection declines coalescing and leaves normal reading available.</summary>
    /// <remarks>The owner factory must finish independently of a single observer's cancellation. Its acquired read handle owns the transfer deadline.</remarks>
    public bool TryRun<T>(CacheEntryIdentity identity, CacheEntryVersion version, Func<Task<T>> factory,
        out Task<T> operation, out bool ownsOperation)
    {
        TaskCompletionSource<T> completion;
        ResourceCharge charge;
        var key = (identity, version, typeof(T));
        lock (this.gate)
        {
            if (this.active.TryGetValue(key, out var found))
            {
                operation = ((TaskCompletionSource<T>)found).Task;
                ownsOperation = false;
                return true;
            }

            if (this.active.Count >= maximum || !ResourceCharge.TryReserve(CacheSizePolicy.EstimateIdentityResidentBytes(identity, version),
                out charge, resources.Resident, resources.HotResident))
            {
                operation = null;
                ownsOperation = false;
                return false;
            }

            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            this.active.Add(key, completion);
            operation = completion.Task;
            ownsOperation = true;
        }

        _ = this.ExecuteAsync(key, factory, completion, charge);
        return true;
    }

    /// <summary>Admits at most one concurrent promotion for this exact entry version, returning null when its bounded allowance is unavailable.</summary>
    public IDisposable TryCapture(CacheEntryIdentity identity, CacheEntryVersion version)
    {
        var key = (identity, version, typeof(CacheHotRead));
        lock (this.gate)
        {
            if (this.active.ContainsKey(key) || this.active.Count >= maximum ||
                !ResourceCharge.TryReserve(CacheSizePolicy.EstimateIdentityResidentBytes(identity, version), out var charge, resources.Resident, resources.HotResident)) { return null; }

            var lease = new CaptureLease(this, key, charge);
            this.active.Add(key, lease);
            return lease;
        }
    }

    private async Task ExecuteAsync<T>((CacheEntryIdentity Identity, CacheEntryVersion Version, Type Kind) key,
        Func<Task<T>> factory, TaskCompletionSource<T> completion, ResourceCharge charge)
    {
        try { completion.TrySetResult(await factory().ConfigureAwait(false)); }
        catch (OperationCanceledException exception) { completion.TrySetCanceled(exception.CancellationToken); }
        catch (Exception exception) { completion.TrySetException(exception); }
        finally
        {
            lock (this.gate) { this.active.Remove(key); }

            charge.Dispose();
        }
    }

    private sealed class CaptureLease(CacheHotPromotionCoordinator owner,
        (CacheEntryIdentity Identity, CacheEntryVersion Version, Type Kind) key, ResourceCharge charge) : IDisposable
    {
        private int disposed;

        /// <inheritdoc />
        public void Dispose()
        {
            if (Interlocked.Exchange(ref this.disposed, 1) != 0) { return; }

            lock (owner.gate) { owner.active.Remove(key); }

            charge.Dispose();
        }
    }
}