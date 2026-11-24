// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

using System.Text.Json;
using BridgingIT.DevKit.Application.Caching;

/// <summary>Reads and writes bounded JSON values inside checked shard index transactions.</summary>
/// <example><code>using var record = await CacheNativeRecords.ReadAsync&lt;CacheNativeEntry&gt;(transaction, key, transient, resident, token);</code></example>
public static class CacheNativeRecords
{
    private static readonly JsonSerializerOptions Json = new() { MaxDepth = 32 };

    /// <summary>Reads one non-tombstoned record, checks its byte limit, and transfers decoded-value memory admission to the returned document.</summary>
    /// <typeparam name="T">The decoded checked metadata type.</typeparam>
    public static async ValueTask<CacheMetadataDocument<T>> ReadAsync<T>(CacheShardTransaction transaction, CacheIndexKey key,
        ResourceBudget transient, ResourceBudget resident, CancellationToken cancellationToken) where T : class
    {
        using var stored = await transaction.ReadAsync(key, cancellationToken).ConfigureAwait(false);
        if (stored is null || stored.Deleted) { return null; }

        if (stored.Bytes.Length is <= 0 or > FileSystemCacheOptions.MaximumMetadataBytes) { throw new CacheTransferException(CacheErrorCode.Corruption); }

        if (!ResourceCharge.TryReserve(33L * stored.Bytes.Length + 65536, out var memory, transient, resident)) { throw new CacheTransferException(CacheErrorCode.Capacity); }

        try
        {
            var value = JsonSerializer.Deserialize<T>(stored.Bytes.Span, Json) ?? throw new CacheTransferException(CacheErrorCode.Corruption);
            return new(value, stored.Bytes.Length, memory);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or FormatException or OverflowException)
        {
            memory.Dispose();
            throw new CacheTransferException(CacheErrorCode.Corruption);
        }
        catch { memory.Dispose(); throw; }
    }

    /// <summary>Serializes a value into a bounded buffer and stages its bytes in the shard transaction.</summary>
    /// <typeparam name="T">The decoded checked metadata type.</typeparam>
    public static void Set<T>(CacheShardTransaction transaction, CacheIndexKey key, T value, ResourceBudget transient,
        ResourceBudget resident, int maximumBytes = 128 * (int)CacheDefaults.KiB)
    {
        if (maximumBytes is <= 0 or > 131072) { throw new CacheTransferException(CacheErrorCode.Validation); }

        if (!ResourceCharge.TryReserve(3L * maximumBytes, out var memory, transient, resident)) { throw new CacheTransferException(CacheErrorCode.Capacity); }

        using var scratch = memory;
        using var buffer = new CacheMetadataBuffer(maximumBytes);
        JsonSerializer.Serialize(buffer, value, Json);
        if (buffer.Length > maximumBytes) { throw new CacheTransferException(CacheErrorCode.Capacity); }

        transaction.Set(key, buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length)));
    }
}

/// <summary>Persists full entry metadata, an optional immutable payload reference and its publication quota attribution.</summary>
/// <example><code>entry.Validate(storeId, identity); var version = entry.Metadata.Version;</code></example>
public sealed record CacheNativeEntry(int Format, CacheEntryMetadata Metadata, CachePayloadReference Payload,
    string CreditId, string PublicationGeneration, long PublicationSequence, long KeyEpoch, long RootEpoch, DateTimeOffset PurgeAfter,
    DateTimeOffset? RefreshDue = null, DateTimeOffset? RefreshBackoff = null)
{
    /// <summary>Checks store and exact-entry identity, generated payload naming and agreement between the payload descriptor and entry metadata.</summary>
    public void Validate(string storeId, CacheEntryIdentity? expected = null)
    {
        if (this.Format != 1 || this.Metadata is null || !this.Metadata.Identity.IsValid || (expected.HasValue && this.Metadata.Identity != expected.Value) ||
            !Guid.TryParseExact(this.Metadata.Version.Value, "N", out _) || !Guid.TryParseExact(this.CreditId, "N", out _) ||
            !Guid.TryParseExact(this.PublicationGeneration, "N", out _) || this.PublicationSequence <= 0 || this.KeyEpoch < 0 || this.RootEpoch < 0 ||
            this.Metadata.Dimensions is null || this.Metadata.Dimensions.Count > 16 || this.Metadata.Dimensions.Any(d => d is null) ||
            this.Metadata.ExpiresAt < this.Metadata.PublishedAt || this.Metadata.AbsoluteExpiresAt < this.Metadata.ExpiresAt || this.PurgeAfter < this.Metadata.ExpiresAt ||
            this.Metadata.StoredSizeBytes < 0 || this.Metadata.EncodedSizeBytes < 0 || this.Metadata.LogicalSizeBytes < 0 ||
            (this.Metadata.IsNotFound && (this.Payload is not null || this.Metadata.StoredSizeBytes != 0)) ||
            (!this.Metadata.IsNotFound && (this.Payload is null || this.Payload.Version != this.Metadata.Version.Value ||
                this.Payload.Length != this.Metadata.StoredSizeBytes || this.Payload.Hash != this.Metadata.StoredHash)))
        {
            throw new CacheTransferException(CacheErrorCode.Corruption);
        }

        this.Payload?.Validate(storeId);
    }
}