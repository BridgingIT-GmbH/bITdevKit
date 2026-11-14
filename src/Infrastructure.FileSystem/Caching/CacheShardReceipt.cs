// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

using BridgingIT.DevKit.Application.Caching;

/// <summary>Identifies a durable shard publication receipt awaiting confirmation, confirmed as current, or proven aborted.</summary>
public enum CacheShardReceiptState
{
    /// <summary>The exact intended manifest generation is durable but not yet confirmed.</summary>
    Prepared,

    /// <summary>The exact authoritative manifest generation and sequence are confirmed.</summary>
    Committed,

    /// <summary>The intended publication is proven not to have committed.</summary>
    Aborted
}

// Always durable, including Buffered data profiles. A fresh receipt prevents an older but internally
// readable shard manifest from masquerading as the current generation after data/namespace loss.
/// <summary>Persists an always-durable shard publication boundary so older readable Buffered content can never be adopted as current.</summary>
/// <example><code>receipt.ValidateScope(storeId, collectionIdentity, shard); receipt.Verify(manifest);</code></example>
public sealed record CacheShardReceipt(int Format, string StoreId, string CollectionIdentity, int Shard, string CreditId,
    string Generation, long Sequence, CacheShardReceiptState State)
{
    /// <summary>Checks the receipt against the exact identity&apos;s store, logical collection and fixed shard.</summary>
    public void Validate(string storeId, CacheEntryIdentity identity) => this.ValidateScope(storeId, identity.CollectionIdentity, CacheStoreRoot.Shard(identity));

    /// <summary>Checks the durable receipt&apos;s scope, generated identities, sequence and lifecycle state against the requested shard.</summary>
    public void ValidateScope(string storeId, string collectionIdentity, int shard)
    {
        if (this.Format != 1 || this.StoreId != storeId || this.CollectionIdentity != collectionIdentity || this.Shard != shard || shard is < 0 or > 255 ||
            !Guid.TryParseExact(this.CreditId, "N", out _) || !Guid.TryParseExact(this.Generation, "N", out _) || this.Sequence < 0 || !Enum.IsDefined(this.State))
        {
            throw new CacheTransferException(CacheErrorCode.Corruption);
        }
    }

    /// <summary>Requires a committed receipt matching the manifest&apos;s exact generation, sequence and operation; uncertainty reports ProviderUnavailable.</summary>
    public void Verify(CacheManifest manifest)
    {
        if (this.State != CacheShardReceiptState.Committed || this.StoreId != manifest.StoreId || this.CollectionIdentity != manifest.CollectionIdentity ||
            this.Shard != manifest.Shard || this.Generation != manifest.Generation || this.Sequence != manifest.Sequence || this.CreditId != manifest.OperationId)
        {
            throw new CacheTransferException(CacheErrorCode.ProviderUnavailable);
        }
    }
}