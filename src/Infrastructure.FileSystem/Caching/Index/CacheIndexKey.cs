// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

using BridgingIT.DevKit.Application.Caching;

/// <summary>Stores one canonical 192-byte composite index key with ordinal byte ordering and zero padding.</summary>
/// <example><code>var key = new CacheIndexKey(new byte[] { CacheMetadataIndex.Entry, 1 }); var next = key.After();</code></example>
public sealed class CacheIndexKey : IComparable<CacheIndexKey>, IEquatable<CacheIndexKey>
{
    /// <summary>The fixed byte width of every format-one composite index key.</summary>
    public const int Size = 192;
    private readonly byte[] bytes;

    // Format-one composite keys have fixed fields per kind and canonical zero padding.
    /// <summary>Copies a nonempty key of at most 192 bytes into zero-padded storage and rejects a zero kind prefix.</summary>
    public CacheIndexKey(ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty || value.Length > Size || value[0] == 0) { throw new CacheTransferException(CacheErrorCode.Validation); }

        this.bytes = new byte[Size];
        value.CopyTo(this.bytes);
    }

    /// <summary>Gets the canonical padded bytes without exposing a writable view.</summary>
    public ReadOnlySpan<byte> Bytes => this.bytes;

    /// <summary>Tests the canonical bytes against an index-kind or composite prefix.</summary>
    public bool StartsWith(ReadOnlySpan<byte> prefix) => this.bytes.AsSpan().StartsWith(prefix);

    /// <summary>Returns the next lexicographic 192-byte key, or null when the key has no successor.</summary>
    public CacheIndexKey After()
    {
        var next = this.bytes.ToArray();
        for (var index = next.Length - 1; index >= 0; index--)
        {
            if (++next[index] != 0) { return new(next); }
        }

        return null;
    }

    /// <summary>Compares canonical key bytes lexicographically; every key sorts after null.</summary>
    public int CompareTo(CacheIndexKey other) => other is null ? 1 : this.bytes.AsSpan().SequenceCompareTo(other.bytes);

    /// <summary>Tests all canonical padded bytes for equality.</summary>
    public bool Equals(CacheIndexKey other) => other is not null && this.bytes.AsSpan().SequenceEqual(other.bytes);

    /// <inheritdoc />
    public override bool Equals(object obj) => obj is CacheIndexKey other && this.Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(BitConverter.ToInt64(this.bytes, 0), BitConverter.ToInt64(this.bytes, 32), BitConverter.ToInt64(this.bytes, 64));
}

/// <summary>Represents a sorted key mutation, its publication sequence, and either bounded record bytes or a tombstone.</summary>
public sealed record CacheSegmentRow(CacheIndexKey Key, long Sequence, ReadOnlyMemory<byte> Value, bool Deleted = false);

/// <summary>Locates a hashed metadata record in one immutable segment, including tombstone and sequence information.</summary>
public sealed record CacheRecordPosition(CacheIndexKey Key, long Sequence, long Offset, int Length, byte[] Hash, bool Deleted);