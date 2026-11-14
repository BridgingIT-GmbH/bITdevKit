// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Owns one binary or decoded representation and its precise retained-resource charge.</summary>
/// <remarks>Instances are created by the hot store only after a complete validated read. Caller buffers are never retained.</remarks>
/// <example><code>var candidate = store.TryAcquire(identity, decoded: false);</code></example>
public sealed class CacheHotRepresentation : IDisposable
{
    private readonly byte[] bytes;
    private readonly ICacheSharedPayloadLease shared;
    private readonly ResourceCharge charge;
    private int disposed;

    /// <summary>Takes ownership of an admitted binary buffer/shared lease or explicitly immutable decoded graph and its retained resource charge.</summary>
    public CacheHotRepresentation(byte[] bytes, ICacheSharedPayloadLease shared, object value, bool decoded,
        long capacityBytes, ResourceCharge charge)
    {
        this.bytes = bytes;
        this.shared = shared;
        this.Value = value;
        this.IsDecoded = decoded;
        this.CapacityBytes = capacityBytes;
        this.charge = charge;
    }

    /// <summary>Gets whether this representation owns an explicitly immutable decoded graph.</summary>
    public bool IsDecoded { get; }

    /// <summary>Gets actual buffer capacity or a conservative approved decoded graph estimate.</summary>
    public long CapacityBytes { get; }

    /// <summary>Gets the immutable shared decoded value; meaningful only when <see cref="IsDecoded"/> is true.</summary>
    public object Value { get; }

    /// <summary>Opens an independent read-only raw cursor without copying retained bytes.</summary>
    public Stream OpenRead()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref this.disposed) != 0, this);
        if (this.IsDecoded) { throw new InvalidOperationException("A decoded representation has no persisted byte cursor."); }

        return this.shared?.OpenRead() ?? new MemoryStream(this.bytes, 0, this.bytes.Length, false, false);
    }

    /// <summary>Releases representation ownership after its entry has retired and its last pin has ended.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) != 0) { return; }

        this.shared?.Dispose();
        this.charge.Dispose();
    }
}