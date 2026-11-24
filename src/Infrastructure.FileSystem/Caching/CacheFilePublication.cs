// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

using System.Text.Json;
using BridgingIT.DevKit.Application.Caching;

/// <summary>Records the publication version and operation receipt needed to reconcile a completed payload write.</summary>
public sealed record CacheNativePublicationReceipt(int Format, CacheEntryIdentity Identity, CacheEntryVersion Version, string OperationId,
    bool IsNotFound, DateTimeOffset PublishedAt)
{
    /// <summary>Builds the receipt index key from an exact generated payload version.</summary>
    public static CacheIndexKey Key(string version)
    {
        if (!Guid.TryParseExact(version, "N", out _)) { throw new CacheTransferException(CacheErrorCode.Validation); }

        return new(new byte[] { 55 }.Concat(Convert.FromHexString(version)).ToArray());
    }
}

/// <summary>Retains a retired payload reference and its quota attribution until physical deletion is proven safe.</summary>
public sealed record CacheNativeRetiredPayload(int Format, CacheEntryIdentity Identity, CachePayloadReference Payload, string CreditId, DateTimeOffset RetiredAt)
{
    /// <summary>Builds the retirement index key for an exact generated payload version.</summary>
    public static CacheIndexKey Key(string version)
    {
        if (!Guid.TryParseExact(version, "N", out _)) { throw new CacheTransferException(CacheErrorCode.Validation); }

        return new(new byte[] { 56 }.Concat(Convert.FromHexString(version)).ToArray());
    }
}