// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System.Collections.Concurrent;

/// <summary>Identifies one dimension name in a store and container.</summary>
/// <param name="Store">The identity of the database and mapped dimension name table.</param>
/// <param name="Scope">The blob container.</param>
/// <param name="Name">The canonical dimension name.</param>
/// <example><code>var key = new BlobDimensionNameKey(storeIdentity, "reports", "YEAR");</code></example>
public readonly record struct BlobDimensionNameKey(string Store, string Scope, string Name);

/// <summary>Caches committed blob dimension name IDs and limits concurrent name registrations.</summary>
/// <example><code>var cache = new BlobDimensionNameCache();</code></example>
public sealed class BlobDimensionNameCache
{
    private readonly ConcurrentDictionary<BlobDimensionNameKey, long> entries = new();
    private readonly Queue<BlobDimensionNameKey> eviction = new();
    private readonly object publication = new();
    private long generation;

    /// <summary>Gets the semaphore that admits up to four concurrent registration sessions.</summary>
    /// <example><code>var available = cache.RegistrationAdmission.CurrentCount;</code></example>
    public SemaphoreSlim RegistrationAdmission { get; } = new(4, 4);

    /// <summary>Gets the current number of cached name IDs.</summary>
    /// <example><code>var count = cache.Count;</code></example>
    public int Count => this.entries.Count;

    /// <summary>Gets the generation that identifies sessions started since the last cache clear.</summary>
    /// <example><code>var generation = cache.Generation;</code></example>
    public long Generation => Interlocked.Read(ref this.generation);

    /// <summary>Looks up a committed name ID in the cache.</summary>
    /// <param name="key">The store, container, and canonical dimension name to look up.</param>
    /// <param name="id">The cached dictionary ID when the key is found.</param>
    /// <returns>True when the cache contains the key.</returns>
    /// <example><code>var found = cache.TryGet(key, out var nameId);</code></example>
    public bool TryGet(BlobDimensionNameKey key, out long id) => this.entries.TryGetValue(key, out id);

    /// <summary>Caches the session's pending name IDs after commit and evicts older entries as needed.</summary>
    /// <param name="session">The session whose name IDs are now committed.</param>
    /// <remarks>Publication is skipped inside an ambient transaction or after the cache generation has changed.</remarks>
    /// <example><code>cache.Publish(session);</code></example>
    public void Publish(BlobDimensionMutationSession session)
    {
        if (System.Transactions.Transaction.Current is not null) return;
        lock (this.publication)
        {
            if (session.Generation != this.generation) return;
            foreach (var entry in session.Pending)
            {
                if (this.entries.ContainsKey(entry.Key)) continue;
                while (this.entries.Count >= 4096 && this.eviction.TryDequeue(out var oldest)) this.entries.TryRemove(oldest, out _);
                if (this.entries.TryAdd(entry.Key, entry.Value)) this.eviction.Enqueue(entry.Key);
            }
        }
    }

    /// <summary>Clears the cached names and increments the generation so older sessions cannot repopulate the cache.</summary>
    /// <example><code>cache.Clear();</code></example>
    public void Clear()
    {
        lock (this.publication)
        {
            this.entries.Clear();
            this.eviction.Clear();
            this.generation++;
        }
    }
}

/// <summary>Tracks dimension names, registrations, and value rows for a single blob mutation or batch.</summary>
/// <param name="cache">The name cache that provides the current generation and registration admission.</param>
/// <example><code>await using var session = new BlobDimensionMutationSession(cache);</code></example>
public sealed class BlobDimensionMutationSession(BlobDimensionNameCache cache) : IAsyncDisposable
{
    private bool admitted;

    /// <summary>Gets the name cache generation captured when the session started.</summary>
    /// <example><code>var generation = session.Generation;</code></example>
    public long Generation { get; } = cache.Generation;

    /// <summary>Gets or sets the number of new name rows registered in this session.</summary>
    /// <example><code>var registered = session.NewRegistrations;</code></example>
    public int NewRegistrations { get; set; }

    /// <summary>Gets or sets whether the resolver has already handled publication for this session.</summary>
    /// <example><code>var published = session.Published;</code></example>
    public bool Published { get; set; }

    /// <summary>Gets or sets the store identity computed during the first name lookup.</summary>
    /// <example><code>var store = session.StoreIdentity;</code></example>
    public string StoreIdentity { get; set; }

    /// <summary>Gets the name IDs already resolved during the session.</summary>
    /// <example><code>var found = session.Resolved.TryGetValue(key, out var nameId);</code></example>
    public Dictionary<BlobDimensionNameKey, long> Resolved { get; } = [];

    /// <summary>Gets the name IDs queued for publication to the shared cache after commit.</summary>
    /// <example><code>var count = session.Pending.Count;</code></example>
    public Dictionary<BlobDimensionNameKey, long> Pending { get; } = [];

    /// <summary>Gets the dimension rows tracked during the mutation, keyed by blob ID and name ID.</summary>
    /// <example><code>var found = session.Rows.TryGetValue((blob.Id, nameId), out var row);</code></example>
    public Dictionary<(Guid Owner, long Name), StorageBlobDimension> Rows { get; } = [];

    /// <summary>Acquires a registration slot for the session if it does not already hold one.</summary>
    /// <param name="token">Cancels waiting for an available registration slot.</param>
    /// <returns>A task that completes when the session holds a slot.</returns>
    /// <example><code>await session.AdmitAsync(cancellationToken);</code></example>
    public async Task AdmitAsync(CancellationToken token)
    {
        if (this.admitted) return;
        await cache.RegistrationAdmission.WaitAsync(token).ConfigureAwait(false);
        this.admitted = true;
    }

    /// <summary>Releases the session's registration slot if it was acquired.</summary>
    /// <returns>A completed task after the slot has been released.</returns>
    /// <example><code>await session.DisposeAsync();</code></example>
    public ValueTask DisposeAsync()
    {
        if (this.admitted)
        {
            this.admitted = false;
            cache.RegistrationAdmission.Release();
        }

        return ValueTask.CompletedTask;
    }
}
