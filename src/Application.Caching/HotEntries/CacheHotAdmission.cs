// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Maintains finite second-successful-access evidence without retaining decoded values or payload buffers.</summary>
/// <example><code>var admission = new CacheHotAdmission(options, collectionResources, timeProvider);</code></example>
public sealed class CacheHotAdmission(CacheHotAdmissionOptions options, CacheCollectionResources resources, TimeProvider time) : IDisposable
{
    private readonly Lock gate = new();
    private readonly Dictionary<CacheEntryIdentity, Evidence> evidence = [];
    private readonly LinkedList<CacheEntryIdentity> recency = [];
    private bool disposed;

    /// <summary>Gets the number of bounded observations currently retained.</summary>
    public int Count { get { lock (this.gate) { return this.evidence.Count; } } }

    /// <summary>Tests whether capturing the next successful read can reach the configured admission threshold.</summary>
    public bool ShouldCapture(CacheEntryMetadata metadata) => this.HasAccesses(metadata, options.MinimumAccesses - 1);

    /// <summary>Tests whether the current exact version has enough completed successful reads for promotion.</summary>
    public bool IsEligible(CacheEntryMetadata metadata) => this.HasAccesses(metadata, options.MinimumAccesses);

    /// <summary>Records one complete successful read. Negative entries never create evidence.</summary>
    public void Observe(CacheEntryMetadata metadata)
    {
        if (metadata.IsNotFound) { return; }

        lock (this.gate)
        {
            if (this.disposed) { return; }

            var now = time.GetUtcNow();
            if (this.evidence.TryGetValue(metadata.Identity, out var found))
            {
                found.Accesses = found.Version == metadata.Version && now - found.FirstAccessAt <= options.ObservationWindow ?
                    Math.Min(options.MinimumAccesses, found.Accesses + 1) : 1;
                if (found.Accesses == 1) { found.FirstAccessAt = now; }

                found.Version = metadata.Version;
                this.recency.Remove(found.Node);
                this.recency.AddLast(found.Node);
                return;
            }

            while (this.evidence.Count >= options.MaxTrackingEntries && this.recency.First is not null) { this.RemoveFirst(); }

            var capacity = CacheSizePolicy.EstimateIdentityResidentBytes(metadata.Identity, metadata.Version);
            if (!ResourceCharge.TryReserve(capacity, out var charge, resources.Resident, resources.HotResident)) { return; }

            var node = this.recency.AddLast(metadata.Identity);
            this.evidence.Add(metadata.Identity, new(metadata.Version, now, node, charge));
        }
    }

    /// <summary>Forgets evidence for the exact retired version without removing newer concurrent evidence.</summary>
    public void Retire(CacheEntryIdentity identity, CacheEntryVersion version)
    {
        lock (this.gate)
        {
            if (!this.evidence.TryGetValue(identity, out var found) || found.Version != version) { return; }

            this.evidence.Remove(identity);
            this.recency.Remove(found.Node);
            found.Charge.Dispose();
        }
    }

    /// <summary>Releases all observation bookkeeping. Persistent entries are unaffected.</summary>
    public void Dispose()
    {
        lock (this.gate)
        {
            if (this.disposed) { return; }

            this.disposed = true;
            while (this.recency.First is not null) { this.RemoveFirst(); }
        }
    }

    private bool HasAccesses(CacheEntryMetadata metadata, int minimum)
    {
        lock (this.gate)
        {
            return !this.disposed && this.evidence.TryGetValue(metadata.Identity, out var found) && found.Version == metadata.Version &&
                found.Accesses >= minimum && time.GetUtcNow() - found.FirstAccessAt <= options.ObservationWindow;
        }
    }

    private void RemoveFirst()
    {
        var node = this.recency.First;
        var found = this.evidence[node.Value];
        this.evidence.Remove(node.Value);
        this.recency.RemoveFirst();
        found.Charge.Dispose();
    }

    private sealed class Evidence(CacheEntryVersion version, DateTimeOffset firstAccessAt,
        LinkedListNode<CacheEntryIdentity> node, ResourceCharge charge)
    {
        public CacheEntryVersion Version = version;
        public DateTimeOffset FirstAccessAt = firstAccessAt;
        public int Accesses = 1;
        public readonly LinkedListNode<CacheEntryIdentity> Node = node;
        public readonly ResourceCharge Charge = charge;
    }
}