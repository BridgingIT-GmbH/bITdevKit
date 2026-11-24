// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Retains bounded exact-version representations with incremental idle/recency eviction and ownership-aware charges.</summary>
/// <remarks>Acquiring a pin does not authorize reuse. The provider must validate the pin's exact version on every access.</remarks>
/// <example><code>using var pin = store.TryAcquire(identity, decoded: false);</code></example>
public sealed class CacheHotStore : IDisposable
{
    private readonly Lock gate = new();
    private readonly Dictionary<CacheEntryIdentity, CacheHotEntry> entries = [];
    private readonly LinkedList<CacheHotEntry> recency = [];
    private readonly CacheCollectionResources resources;
    private readonly TimeProvider time;
    private readonly CacheObservationSlot observations;
    private bool disposed;
    private bool pressure;

    /// <summary>Creates collection-owned retention. Construct this object only for an effectively enabled hot policy.</summary>
    public CacheHotStore(CacheHotEntryOptions options, CacheCollectionResources resources, TimeProvider time, CacheObservationSlot observations = null)
    {
        this.observations = observations;
        this.Options = options;
        this.resources = resources;
        this.time = time;
        this.Admission = new(options.Admission, resources, time);
        this.Promotions = new(resources, options.MaxEntries);
    }

    /// <summary>Gets the frozen collection policy.</summary>
    public CacheHotEntryOptions Options { get; }

    /// <summary>Gets bounded successful-access evidence.</summary>
    public CacheHotAdmission Admission { get; }

    /// <summary>Gets version-specific promotion coordination, separate from source loading.</summary>
    public CacheHotPromotionCoordinator Promotions { get; }

    /// <summary>Gets indexed entry count. Retired pins remain resident until their owners dispose them.</summary>
    public int Count { get { lock (this.gate) { return this.entries.Count; } } }

    /// <summary>Captures the indexed version before a local mutation, so completion cannot retire a newer concurrent candidate.</summary>
    public CacheEntryVersion? GetVersion(CacheEntryIdentity identity)
    {
        lock (this.gate) { return this.entries.TryGetValue(identity, out var entry) ? entry.Metadata.Version : null; }
    }

    /// <summary>Pins an eligible local candidate without consulting or renewing provider authority.</summary>
    public CacheHotEntryLease TryAcquire(CacheEntryIdentity identity, bool decoded)
    {
        lock (this.gate)
        {
            if (this.disposed) { return null; }

            this.Sweep(8);
            if (!this.entries.TryGetValue(identity, out var entry)) { return null; }

            var representation = decoded ? entry.Decoded : entry.Binary;
            if (representation is null) { return null; }

            entry.AddReference();
            return new(entry, representation);
        }
    }

    /// <summary>Updates recency only after the same exact version passed authoritative validation.</summary>
    public void ObserveValidated(CacheEntryIdentity identity, CacheEntryVersion version)
    {
        lock (this.gate)
        {
            if (!this.entries.TryGetValue(identity, out var entry) || entry.Metadata.Version != version) { return; }

            entry.LastAccessAt = this.time.GetUtcNow();
            this.recency.Remove(entry.Node);
            this.recency.AddLast(entry.Node);
        }
    }

    /// <summary>Declines unknown/oversized graphs without changing a successful ordinary read.</summary>
    public bool TryRetainDecoded<T>(CacheEntryMetadata metadata, CacheValueContract<T> contract, T value)
    {
        if (this.Options.Representation == CacheHotEntryRepresentation.Binary || !this.Admission.IsEligible(metadata) ||
            !CacheDecodedValuePolicy.TryEstimate(contract, value, this.Options.MaxEntryBytes, out var capacity)) { return false; }

        lock (this.gate)
        {
            if (!this.TryPrepare(metadata, capacity, true, out var entry)) { this.observations?.Record(CacheObservationKind.HotAdmissionRejected); return false; }

            if (!ResourceCharge.TryReserve(capacity, out var charge, this.resources.Resident, this.resources.HotResident)) { this.observations?.Record(CacheObservationKind.HotAdmissionRejected); return false; }

            entry.Decoded = new(null, null, value, true, capacity, charge);
            this.observations?.Record(CacheObservationKind.HotPromotion);
            return true;
        }
    }

    /// <summary>Conditionally retires the exact observed version. Existing pinned readers retain their immutable snapshots and charges.</summary>
    public void Retire(CacheEntryIdentity identity, CacheEntryVersion version)
    {
        lock (this.gate)
        {
            if (this.entries.TryGetValue(identity, out var entry) && entry.Metadata.Version == version) { this.Remove(entry); }
        }

        this.Admission.Retire(identity, version);
    }

    /// <summary>Retires locally retained versions covered by one acknowledged invalidation cutoff, preserving later publications.</summary>
    /// <remarks>This visits only the finite local hot index. Provider invalidation and provider metadata queries remain authoritative and independent.</remarks>
    public void RetireMatching(CacheSelection selection, IReadOnlyList<CacheDimensionSchema> schemas, DateTimeOffset cutoff, bool clear = false)
    {
        var evaluator = clear ? null : new CacheSelectionEvaluator(selection, schemas, cutoff);
        lock (this.gate)
        {
            var node = this.recency.First;
            while (node is not null)
            {
                var next = node.Next;
                var entry = node.Value;
                if (entry.Metadata.PublishedAt <= cutoff && (clear || evaluator.Matches(entry.Metadata)))
                {
                    this.Remove(entry);
                    this.Admission.Retire(entry.Metadata.Identity, entry.Metadata.Version);
                }

                node = next;
            }
        }
    }

    /// <summary>Releases indexed entries and bookkeeping while allowing already pinned snapshots to finish.</summary>
    public void Dispose()
    {
        lock (this.gate)
        {
            if (this.disposed) { return; }

            this.disposed = true;
            while (this.recency.First is not null) { this.Remove(this.recency.First.Value, true); }
        }

        this.Admission.Dispose();
    }

    /// <summary>Reserves conservative capture capacity within every applicable retained hot and resident budget.</summary>
    public bool TryReserveCapture(long capacity, bool shared, out ResourceCharge charge)
    {
        lock (this.gate)
        {
            this.Sweep(16);
            charge = null;
            return !this.disposed && capacity >= 0 && capacity <= this.Options.MaxEntryBytes &&
                ResourceCharge.TryReserve(capacity, out charge,
                    shared ? [this.resources.HotResident] : [this.resources.Resident, this.resources.HotResident]);
        }
    }

    /// <summary>Adopts a fully validated exact-version binary representation when admission and all hot capacity limits permit retention.</summary>
    public bool TryRetainBinary(CacheEntryMetadata metadata, byte[] bytes, ICacheSharedPayloadLease shared,
        ResourceCharge transferredCharge = null)
    {
        var capacity = shared?.CapacityBytes ?? (bytes is null ? -1 : bytes.LongLength + 32);
        if (this.Options.Representation == CacheHotEntryRepresentation.Decoded || !this.Admission.IsEligible(metadata)) { return false; }

        lock (this.gate)
        {
            if (!this.TryPrepare(metadata, capacity, false, out var entry)) { this.observations?.Record(CacheObservationKind.HotAdmissionRejected); return false; }

            var charge = transferredCharge;
            if (charge is null && !ResourceCharge.TryReserve(capacity, out charge,
                shared is null ? [this.resources.Resident, this.resources.HotResident] : [this.resources.HotResident])) { this.observations?.Record(CacheObservationKind.HotAdmissionRejected); return false; }

            entry.Binary = new(bytes, shared, null, false, capacity, charge);
            this.observations?.Record(CacheObservationKind.HotPromotion);
            return true;
        }
    }

    private bool TryPrepare(CacheEntryMetadata metadata, long capacity, bool decoded, out CacheHotEntry entry)
    {
        entry = null;
        if (this.disposed || metadata.IsNotFound || capacity < 0 || capacity > this.Options.MaxEntryBytes) { return false; }

        var bookkeepingBytes = CacheSizePolicy.EstimateMetadataResidentBytes(metadata);
        if (bookkeepingBytes > this.Options.MaxEntryBytes - capacity) { return false; }

        this.Sweep(16);
        if (this.entries.TryGetValue(metadata.Identity, out entry))
        {
            // An older in-flight read must never replace a different candidate published concurrently.
            return entry.Metadata.Version == metadata.Version && (decoded ? entry.Decoded : entry.Binary) is null &&
                entry.BinaryBytes + entry.DecodedBytes <= this.Options.MaxEntryBytes - bookkeepingBytes - capacity;
        }

        if (this.entries.Count >= this.Options.MaxEntries && this.recency.First is not null) { this.Remove(this.recency.First.Value, true); }

        if (!ResourceCharge.TryReserve(bookkeepingBytes, out var bookkeeping,
            this.resources.Resident, this.resources.HotResident)) { return false; }

        entry = new(metadata, this.time.GetUtcNow(), bookkeeping, this.observations?.HotFootprint);
        entry.Node = this.recency.AddLast(entry);
        this.entries.Add(metadata.Identity, entry);
        return true;
    }

    private void Sweep(int maximum)
    {
        var now = this.time.GetUtcNow();
        var budget = this.resources.HotResident;
        var lower = budget.Maximum / 10 * 7 + budget.Maximum % 10 * 7 / 10;
        var upper = budget.Maximum / 10 * 9 + budget.Maximum % 10 * 9 / 10;
        this.pressure = this.pressure ? budget.Used > lower : budget.Used > upper;
        for (var index = 0; index < maximum && this.recency.First is not null; index++)
        {
            var first = this.recency.First.Value;
            if (now - first.LastAccessAt <= this.Options.MaxIdleTime && !this.pressure) { break; }

            this.Remove(first, true);
            if (budget.Used <= lower) { this.pressure = false; }
        }
    }

    private void Remove(CacheHotEntry entry, bool eviction = false)
    {
        this.entries.Remove(entry.Metadata.Identity);
        this.recency.Remove(entry.Node);
        if (eviction) { this.observations?.Record(CacheObservationKind.HotEviction); }

        entry.Retire();
    }
}