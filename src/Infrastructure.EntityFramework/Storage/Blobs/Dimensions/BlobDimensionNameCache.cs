// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System.Collections.Concurrent;

internal readonly record struct BlobDimensionNameKey(string Store, string Scope, string Name);

internal sealed class BlobDimensionNameCache
{
    private readonly ConcurrentDictionary<BlobDimensionNameKey, long> entries = new();
    private readonly Queue<BlobDimensionNameKey> eviction = new();
    private readonly object publication = new();
    private long generation;

    public SemaphoreSlim RegistrationAdmission { get; } = new(4, 4);

    public int Count => this.entries.Count;

    public long Generation => Interlocked.Read(ref this.generation);

    public bool TryGet(BlobDimensionNameKey key, out long id) => this.entries.TryGetValue(key, out id);

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

internal sealed class BlobDimensionMutationSession(BlobDimensionNameCache cache) : IAsyncDisposable
{
    private bool admitted;

    public long Generation { get; } = cache.Generation;

    public int NewRegistrations { get; set; }

    public bool Published { get; set; }

    public string StoreIdentity { get; set; }

    public Dictionary<BlobDimensionNameKey, long> Resolved { get; } = [];

    public Dictionary<BlobDimensionNameKey, long> Pending { get; } = [];

    public Dictionary<(string Owner, long Name), StorageBlobDimension> Rows { get; } = [];

    public async Task AdmitAsync(CancellationToken token)
    {
        if (this.admitted) return;
        await cache.RegistrationAdmission.WaitAsync(token).ConfigureAwait(false);
        this.admitted = true;
    }

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
