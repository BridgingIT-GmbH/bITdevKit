// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Owns retained representations for one confirmed immutable provider version.</summary>
/// <example><code>using var pin = store.TryAcquire(identity, decoded: false);</code></example>
public sealed class CacheHotEntry
{
    private readonly ResourceCharge charge;
    private readonly Lock gate = new();
    private readonly CacheHotFootprint footprint;
    private CacheHotRepresentation binary;
    private CacheHotRepresentation decoded;
    private int references = 1;
    private bool retired;

    /// <summary>Takes ownership of a confirmed entry&apos;s base retained charge and initializes its local access observation.</summary>
    /// <param name="metadata">The exact immutable provider publication metadata.</param>
    /// <param name="now">The initial local access observation.</param>
    /// <param name="charge">The owned retained bookkeeping charge.</param>
    /// <param name="footprint">Optional predeclared lifecycle capacity accounting.</param>
    public CacheHotEntry(CacheEntryMetadata metadata, DateTimeOffset now, ResourceCharge charge, CacheHotFootprint footprint = null)
    {
        this.Metadata = metadata;
        this.LastAccessAt = now;
        this.charge = charge;
        this.footprint = footprint;
    }

    /// <summary>Gets the exact immutable persisted identity, entry version, and format used at promotion.</summary>
    public CacheEntryMetadata Metadata { get; }

    /// <summary>Gets the most recent successful local access observation.</summary>
    public DateTimeOffset LastAccessAt { get; set; }

    /// <summary>Gets retained binary capacity, or zero when no binary representation is present.</summary>
    public long BinaryBytes => this.Binary?.CapacityBytes ?? 0;

    /// <summary>Gets retained decoded graph estimate, or zero when no decoded representation is present.</summary>
    public long DecodedBytes => this.Decoded?.CapacityBytes ?? 0;

    /// <summary>Gets or sets the retained binary representation owned by this entry, if present.</summary>
    public CacheHotRepresentation Binary
    {
        get => Volatile.Read(ref this.binary);
        set
        {
            lock (this.gate)
            {
                var delta = (value?.CapacityBytes ?? 0) - (this.binary?.CapacityBytes ?? 0);
                this.binary = value;
                this.footprint?.ChangeBinary(delta);
                if (this.references > (this.retired ? 0 : 1)) { this.footprint?.ChangePinned(delta); }
            }
        }
    }

    /// <summary>Gets or sets the explicitly immutable decoded representation owned by this entry, if present.</summary>
    public CacheHotRepresentation Decoded
    {
        get => Volatile.Read(ref this.decoded);
        set
        {
            lock (this.gate)
            {
                var delta = (value?.CapacityBytes ?? 0) - (this.decoded?.CapacityBytes ?? 0);
                this.decoded = value;
                this.footprint?.ChangeDecoded(delta);
                if (this.references > (this.retired ? 0 : 1)) { this.footprint?.ChangePinned(delta); }
            }
        }
    }

    /// <summary>Gets or sets this entry&apos;s node in the hot store&apos;s bounded recency list.</summary>
    public LinkedListNode<CacheHotEntry> Node { get; set; }

    /// <summary>Pins this live hot entry until a matching release, preserving representation charges after index retirement.</summary>
    public void AddReference()
    {
        lock (this.gate)
        {
            ObjectDisposedException.ThrowIf(this.references == 0, this);
            if (!this.retired && this.references == 1) { this.footprint?.ChangePinned(this.BinaryBytes + this.DecodedBytes); }

            this.references++;
        }
    }

    /// <summary>Releases the hot index's owner once while preserving every external immutable pin and capacity charge.</summary>
    /// <example><code>entry.Retire(); // existing leases retain the same representation until disposal</code></example>
    public void Retire()
    {
        bool release;
        lock (this.gate)
        {
            if (this.retired) { return; }

            this.retired = true;
            release = --this.references == 0;
        }

        if (release) { this.ReleaseRepresentations(); }
    }

    /// <summary>Releases one pin and disposes both representations and their base charge after the last reference ends.</summary>
    public void Release()
    {
        bool release;
        lock (this.gate)
        {
            if (this.references == 0) { return; }

            var pinnedBefore = this.references > (this.retired ? 0 : 1);
            this.references--;
            if (pinnedBefore && this.references == (this.retired ? 0 : 1)) { this.footprint?.ChangePinned(-this.BinaryBytes - this.DecodedBytes); }

            release = this.references == 0;
            if (release) { this.retired = true; }
        }

        if (release) { this.ReleaseRepresentations(); }
    }

    private void ReleaseRepresentations()
    {
        try
        {
            this.Binary?.Dispose();
            this.footprint?.ChangeBinary(-this.BinaryBytes);
        }
        finally
        {
            try
            {
                this.Decoded?.Dispose();
                this.footprint?.ChangeDecoded(-this.DecodedBytes);
            }
            finally { this.charge.Dispose(); }
        }
    }
}

/// <summary>Pins one immutable local representation while authoritative validation and delivery finish.</summary>
/// <example><code>using var pin = store.TryAcquire(identity, decoded: true);</code></example>
public sealed class CacheHotEntryLease : IDisposable
{
    private CacheHotEntry entry;

    /// <summary>Binds an already pinned hot entry to one selected retained representation; disposal releases that pin.</summary>
    public CacheHotEntryLease(CacheHotEntry entry, CacheHotRepresentation representation)
    {
        this.entry = entry;
        this.Representation = representation;
    }

    /// <summary>Gets the exact version which the provider must validate before reuse.</summary>
    public CacheEntryMetadata Metadata => this.entry?.Metadata ?? throw new ObjectDisposedException(nameof(CacheHotEntryLease));

    /// <summary>Gets the pinned representation. It remains charged even if the store index evicts the entry.</summary>
    public CacheHotRepresentation Representation { get; }

    /// <summary>Ends this pin without changing persistent provider data.</summary>
    public void Dispose() => Interlocked.Exchange(ref this.entry, null)?.Release();
}