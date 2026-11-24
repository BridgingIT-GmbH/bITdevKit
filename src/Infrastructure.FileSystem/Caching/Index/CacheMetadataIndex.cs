// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BridgingIT.DevKit.Application.Caching;

/// <summary>Builds format-one composite metadata keys and ordered ranges from canonical identities and typed dimensions.</summary>
/// <example><code>var entryKey = CacheMetadataIndex.Primary(identity);</code></example>
public static class CacheMetadataIndex
{
    /// <summary>The format-one composite-key kind prefix for primary-entry records.</summary>
    public const byte Entry = 16;

    /// <summary>The format-one composite-key kind prefix for logical-key candidates across contract versions.</summary>
    public const byte LogicalKey = 17;

    /// <summary>The format-one composite-key kind prefix for ordinal normalized-key prefix candidates.</summary>
    public const byte KeyPrefix = 18;

    /// <summary>The format-one composite-key kind prefix for publication-time candidates.</summary>
    public const byte Published = 19;

    /// <summary>The format-one composite-key kind prefix for logical expiry candidates.</summary>
    public const byte Expiry = 20;

    /// <summary>The format-one composite-key kind prefix for physical purge-after candidates.</summary>
    public const byte Purge = 21;

    /// <summary>The format-one composite-key kind prefix for stored-byte-size candidates.</summary>
    public const byte StoredSize = 22;

    /// <summary>The format-one composite-key kind prefix for sampled last-access candidates.</summary>
    public const byte Access = 23;

    /// <summary>The format-one composite-key kind prefix for eviction-priority candidates.</summary>
    public const byte Priority = 24;

    /// <summary>The format-one composite-key kind prefix for scheduled refresh candidates.</summary>
    public const byte Refresh = 25;

    /// <summary>The format-one composite-key kind prefix for declared dimension presence candidates.</summary>
    public const byte Present = 32;

    /// <summary>The format-one composite-key kind prefix for typed scalar dimension candidates.</summary>
    public const byte Scalar = 33;

    /// <summary>The format-one composite-key kind prefix for typed set-member candidates.</summary>
    public const byte Member = 34;

    /// <summary>The format-one composite-key kind prefix for half-open interval-start candidates.</summary>
    public const byte Interval = 35;
    private const int PrefixBytes = 96;

    /// <summary>Builds the primary-entry key from an exact identity or its SHA-256 digest.</summary>
    public static CacheIndexKey Primary(CacheEntryIdentity identity) => Primary(Convert.FromHexString(identity.Digest));

    /// <summary>Builds the primary-entry key from an exact identity or its SHA-256 digest.</summary>
    public static CacheIndexKey Primary(ReadOnlySpan<byte> digest)
    {
        if (digest.Length != 32) { throw new CacheTransferException(CacheErrorCode.Validation); }

        Span<byte> key = stackalloc byte[33];
        key[0] = Entry;
        digest.CopyTo(key[1..]);
        return new(key);
    }

    /// <summary>Encodes an exact identity digest and immutable payload version for a secondary-index candidate.</summary>
    public static byte[] Pointer(CacheEntryMetadata metadata)
    {
        var value = new byte[48];
        Convert.FromHexString(metadata.Identity.Digest).CopyTo(value, 0);
        if (!Guid.TryParseExact(metadata.Version.Value, "N", out var version)) { throw new CacheTransferException(CacheErrorCode.Contract); }

        version.TryWriteBytes(value.AsSpan(32), bigEndian: true, out _);
        return value;
    }

    // The primary record retains full canonical identity/dimensions; a digest hit is only a candidate.
    // Side records are fixed pointers, avoiding one complete metadata copy per dimension member.
    /// <summary>Enumerates secondary metadata keys for one entry, including expiry, access and declared dimension values.</summary>
    public static IEnumerable<CacheIndexKey> Keys(CacheEntryMetadata metadata, DateTimeOffset purgeAfter, DateTimeOffset? refreshDue = null)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        var pointer = Pointer(metadata);
        yield return Suffix(LogicalPrefix(metadata.Identity), pointer);
        var prefix = new byte[1 + PrefixBytes];
        prefix[0] = KeyPrefix;
        var text = Encoding.UTF8.GetBytes(metadata.Identity.Key);
        text.AsSpan(0, Math.Min(PrefixBytes, text.Length)).CopyTo(prefix.AsSpan(1));
        yield return Suffix(prefix, pointer);
        yield return Suffix(OrderedPrefix(Published, metadata.PublishedAt.UtcTicks), pointer);
        yield return Suffix(OrderedPrefix(Expiry, metadata.ExpiresAt.UtcTicks), pointer);
        yield return Suffix(OrderedPrefix(Purge, purgeAfter.UtcTicks), pointer);
        yield return Suffix(OrderedPrefix(StoredSize, metadata.StoredSizeBytes), pointer);
        yield return Suffix(OrderedPrefix(Access, metadata.LastAccessAt.UtcTicks), pointer);
        var priority = new byte[10];
        priority[0] = Priority;
        priority[1] = (byte)metadata.Priority;
        Ordered(metadata.LastAccessAt.UtcTicks, priority.AsSpan(2));
        yield return Suffix(priority, pointer);
        if (refreshDue.HasValue) { yield return Suffix(OrderedPrefix(Refresh, refreshDue.Value.UtcTicks), pointer); }

        foreach (var dimension in metadata.Dimensions)
        {
            yield return Suffix(DimensionPrefix(Present, dimension.Name, dimension.Kind, dimension.Form), pointer);
            if (dimension.Form == CacheDimensionForm.Interval)
            {
                var lower = long.Parse(dimension.Values[0].CanonicalValue, CultureInfo.InvariantCulture);
                var bounds = DimensionPrefix(Interval, dimension.Name, dimension.Kind, dimension.Form);
                var key = new byte[bounds.Length + 8];
                bounds.CopyTo(key, 0);
                Ordered(lower, key.AsSpan(bounds.Length));
                yield return Suffix(key, pointer);
            }
            else
            {
                foreach (var value in dimension.Values)
                {
                    yield return Suffix(ValuePrefix(dimension.Form == CacheDimensionForm.Set ? Member : Scalar, dimension.Name, dimension.Form, value), pointer);
                }
            }
        }
    }

    /// <summary>Builds the logical-key digest prefix used to locate all contract versions of one key.</summary>
    public static byte[] LogicalPrefix(CacheEntryIdentity identity)
    {
        var prefix = new byte[33];
        prefix[0] = LogicalKey;
        SHA256.HashData(Encoding.UTF8.GetBytes(new CacheEntryIdentity(identity.Namespace, identity.Collection, "logical-key", identity.Key).Canonical), prefix.AsSpan(1));
        return prefix;
    }

    /// <summary>Builds an inclusive lower and exclusive upper bound for a nonempty composite prefix.</summary>
    public static CacheIndexRange PrefixRange(ReadOnlySpan<byte> prefix)
    {
        if (prefix.IsEmpty || prefix[0] == 0 || prefix.Length > CacheIndexKey.Size) { throw new CacheTransferException(CacheErrorCode.Validation); }

        var upper = prefix.ToArray();
        for (var index = upper.Length - 1; index >= 0; index--)
        {
            if (++upper[index] != 0) { return new(new(prefix), new(upper.AsSpan(0, index + 1))); }
        }

        return new(new(prefix), null);
    }

    /// <summary>Builds the ordered key-prefix candidate range for a normalized key string.</summary>
    public static CacheIndexRange KeyPrefixRange(string key)
    {
        var text = Encoding.UTF8.GetBytes(CacheIdentityNormalizer.Key(key));
        var prefix = new byte[1 + Math.Min(PrefixBytes, text.Length)];
        prefix[0] = KeyPrefix;
        text.AsSpan(0, prefix.Length - 1).CopyTo(prefix.AsSpan(1));
        return PrefixRange(prefix);
    }

    /// <summary>Builds the exact typed scalar or set-member candidate range for a declared dimension value.</summary>
    public static CacheIndexRange ValueRange(string name, CacheDimensionForm form, CacheDimensionValue value) =>
        PrefixRange(ValuePrefix(form == CacheDimensionForm.Set ? Member : Scalar, name, form, value));

    /// <summary>Builds the candidate range for a dimension&apos;s exact name, kind and form.</summary>
    public static CacheIndexRange PresenceRange(string name, CacheDimensionKind kind, CacheDimensionForm form) =>
        PrefixRange(DimensionPrefix(Present, name, kind, form));

    /// <summary>Builds the candidate range for a normalized dimension name across kinds and forms.</summary>
    public static CacheIndexRange PresenceNameRange(string name) => PrefixRange(DimensionPrefix(Present, name, default, default).AsSpan(0, 33));

    /// <summary>Builds a numeric secondary-index range with inclusive lower and exclusive upper bounds.</summary>
    public static CacheIndexRange OrderedRange(byte kind, long lower, long upper)
    {
        if (kind is not (Published or Expiry or Purge or StoredSize or Access or Refresh) || lower >= upper) { throw new CacheTransferException(CacheErrorCode.Validation); }

        return new(new(OrderedPrefix(kind, lower)), new(OrderedPrefix(kind, upper)));
    }

    // start < requested upper is indexable; the authoritative half-open end > lower check is residual.
    /// <summary>Builds the interval-start candidate range below an exclusive upper endpoint; callers must still test overlap against full metadata.</summary>
    public static CacheIndexRange IntervalRange(string name, CacheDimensionKind kind, long upper)
    {
        if (kind is not (CacheDimensionKind.Integer or CacheDimensionKind.Timestamp)) { throw new CacheTransferException(CacheErrorCode.Validation); }

        var prefix = DimensionPrefix(Interval, name, kind, CacheDimensionForm.Interval);
        var end = new byte[prefix.Length + 8];
        prefix.CopyTo(end, 0);
        Ordered(upper, end.AsSpan(prefix.Length));
        return new(new(prefix), new(end));
    }

    private static byte[] ValuePrefix(byte kind, string name, CacheDimensionForm form, CacheDimensionValue value)
    {
        var basis = DimensionPrefix(kind, name, value.Kind, form);
        var prefix = new byte[basis.Length + 33];
        basis.CopyTo(prefix, 0);
        prefix[basis.Length] = value.IsNull ? (byte)0 : (byte)1;
        if (!value.IsNull) { SHA256.HashData(Encoding.UTF8.GetBytes(value.CanonicalValue), prefix.AsSpan(basis.Length + 1)); }

        return prefix;
    }

    private static byte[] DimensionPrefix(byte kind, string name, CacheDimensionKind valueKind, CacheDimensionForm form)
    {
        var prefix = new byte[35];
        prefix[0] = kind;
        SHA256.HashData(Encoding.UTF8.GetBytes(CacheIdentityNormalizer.Name(name)), prefix.AsSpan(1, 32));
        prefix[33] = (byte)valueKind;
        prefix[34] = (byte)form;
        return prefix;
    }

    private static byte[] OrderedPrefix(byte kind, long value)
    {
        var prefix = new byte[9];
        prefix[0] = kind;
        Ordered(value, prefix.AsSpan(1));
        return prefix;
    }

    private static void Ordered(long value, Span<byte> destination) => BinaryPrimitives.WriteUInt64BigEndian(destination, unchecked((ulong)(value ^ long.MinValue)));

    private static CacheIndexKey Suffix(ReadOnlySpan<byte> prefix, ReadOnlySpan<byte> pointer)
    {
        Span<byte> bytes = stackalloc byte[CacheIndexKey.Size];
        bytes.Clear();
        prefix.CopyTo(bytes);
        pointer.CopyTo(bytes[prefix.Length..]);
        return new(bytes);
    }
}

/// <summary>Defines an inclusive lower and optional exclusive upper bound in composite-key order.</summary>
/// <example><code>var candidates = CacheMetadataIndex.PrefixRange(prefix); var matches = candidates.Contains(key);</code></example>
public sealed record CacheIndexRange(CacheIndexKey Lower, CacheIndexKey Upper)
{
    /// <summary>Tests whether a key falls within the inclusive lower and exclusive upper bounds.</summary>
    public bool Contains(CacheIndexKey key) => key.CompareTo(this.Lower) >= 0 && (this.Upper is null || key.CompareTo(this.Upper) < 0);
}