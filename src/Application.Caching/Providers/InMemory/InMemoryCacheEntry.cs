// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Owns one immutable publication and its payload, reader references, count and metadata charges.</summary>
/// <example><code>entry.AddReference(); try { Use(entry.Metadata); } finally { entry.Release(); }</code></example>
public sealed class InMemoryCacheEntry
{
    private readonly InMemoryCachePayload payload;
    private readonly IDisposable metadataCharge;
    private ResourceReservation currentCount;
    private CacheEntryMetadata metadata;
    private int references = 1;
    private int retired;
    private long lastAccessTicks;
    private int foregroundAccess;

    /// <summary>Owns one immutable publication and its payload, reader references, count and metadata charges.</summary>
    /// <param name="metadata">The immutable acquired publication metadata.</param>
    /// <param name="payload">The owned bounded chunked payload.</param>
    /// <param name="metadataCharge">The retained metadata/index reservation.</param>
    /// <param name="currentCount">The owned current-entry count reservation.</param>
    /// <param name="publicationEpoch">The captured collection publication epoch.</param>
    /// <param name="metadataBytes">The conservatively retained metadata byte capacity.</param>
    /// <param name="publicationSequence">The assigned collection publication sequence.</param>
    public InMemoryCacheEntry(CacheEntryMetadata metadata, InMemoryCachePayload payload, IDisposable metadataCharge,
        ResourceReservation currentCount, long publicationEpoch, long metadataBytes, long publicationSequence)
    {
        this.metadata = metadata;
        this.payload = payload;
        this.metadataCharge = metadataCharge;
        this.currentCount = currentCount;
        this.PublicationEpoch = publicationEpoch;
        this.MetadataBytes = metadataBytes;
        this.PublicationSequence = publicationSequence;
        this.SortKey = metadata.Identity.Key + "\0" + metadata.Identity.ContractVersion + "\0" + metadata.Version.Value;
        this.lastAccessTicks = metadata.LastAccessAt.UtcTicks;
        this.foregroundAccess = metadata.HasForegroundAccess ? 1 : 0;
    }

    /// <summary>Gets the atomically replaceable metadata snapshot for this same immutable payload generation.</summary>
    public CacheEntryMetadata Metadata => Volatile.Read(ref this.metadata);

    /// <summary>Gets the ordinal composite key separating logical keys, contracts and payload generations.</summary>
    public string SortKey { get; }

    /// <summary>Gets the collection epoch captured when this generation became current.</summary>
    public long PublicationEpoch { get; }

    /// <summary>Gets the conservatively retained metadata/index byte charge.</summary>
    public long MetadataBytes { get; }

    /// <summary>Gets the collection publication sequence used by fixed-cutoff invalidation.</summary>
    public long PublicationSequence { get; }

    /// <summary>Gets the latest foreground access observation in UTC ticks.</summary>
    public long LastAccessTicks => Interlocked.Read(ref this.lastAccessTicks);

    /// <summary>Gets whether this generation observed successful foreground use.</summary>
    public bool HasForegroundAccess => Volatile.Read(ref this.foregroundAccess) != 0;

    /// <summary>Gets whether references beyond the current-entry owner still pin this generation.</summary>
    public bool HasReaders => Volatile.Read(ref this.references) > 1;

    /// <summary>Marks foreground use and advances the retained observation without moving it backward.</summary>
    public void ObserveAccess(DateTimeOffset now)
    {
        Volatile.Write(ref this.foregroundAccess, 1);
        var ticks = now.UtcTicks;
        var old = Interlocked.Read(ref this.lastAccessTicks);
        if (ticks > old)
        {
            Interlocked.CompareExchange(ref this.lastAccessTicks, ticks, old);
        }
    }

    /// <summary>Atomically replaces bookkeeping metadata for the same retained payload generation.</summary>
    public void UpdateMetadata(CacheEntryMetadata value) => Volatile.Write(ref this.metadata, value);

    /// <summary>Transfers current-entry count ownership to a replacement without releasing capacity.</summary>
    public ResourceReservation TakeCount() => Interlocked.Exchange(ref this.currentCount, null);

    /// <summary>Attaches exactly one transferred count reservation, rejecting null or duplicate ownership.</summary>
    public void AttachCount(ResourceReservation reservation)
    {
        if (reservation is null || Interlocked.CompareExchange(ref this.currentCount, reservation, null) is not null)
        {
            throw new InvalidOperationException("A current cache entry must own exactly one count reservation.");
        }
    }

    /// <summary>Pins a live generation, rejecting references after final reclamation.</summary>
    public void AddReference()
    {
        while (true)
        {
            var current = Volatile.Read(ref this.references);
            ObjectDisposedException.ThrowIf(current == 0, this);
            if (Interlocked.CompareExchange(ref this.references, current + 1, current) == current)
            {
                return;
            }
        }
    }

    /// <summary>Once-only releases current-entry count and ownership while preserving existing readers.</summary>
    public void Retire()
    {
        if (Interlocked.Exchange(ref this.retired, 1) != 0)
        {
            return;
        }

        Interlocked.Exchange(ref this.currentCount, null)?.Dispose();
        this.Release();
    }

    /// <summary>Releases one reference and reclaims payload/metadata after the final owner ends.</summary>
    public void Release()
    {
        if (Interlocked.Decrement(ref this.references) != 0)
        {
            return;
        }

        this.payload?.Release();
        this.metadataCharge.Dispose();
    }

    /// <summary>Pins an independently positioned immutable payload stream.</summary>
    public Stream OpenPayload() => this.payload.OpenRead();

    /// <summary>Acquires an independent shared-memory lease on this generation's payload.</summary>
    public ICacheSharedPayloadLease RetainPayload() => new InMemoryCacheSharedPayloadLease(this.payload);
}