// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

using System.Security.Cryptography;
using System.Text;
using BridgingIT.DevKit.Application.Caching;

/// <summary>Identifies the exact source flight and owner selected for a canonical cache identity.</summary>
public sealed record CacheNativeFlightPointer(int Format, CacheEntryIdentity Identity, string FlightId, string OwnerToken);

/// <summary>Persists one follower&apos;s interest in a specific source flight, bounded by that follower&apos;s deadline.</summary>
public sealed record CacheNativeInterest(int Format, CacheEntryIdentity Identity, string FlightId, string InterestId, DateTimeOffset Deadline)
{
    /// <summary>Rejects malformed interest IDs or an identity and flight that differ from the checked owner record.</summary>
    public void Validate(CacheNativeOwnership owner, string interestId)
    {
        if (this.Format != 1 || this.Identity != owner.Identity || this.FlightId != owner.FlightId || this.InterestId != interestId ||
            string.IsNullOrEmpty(this.InterestId) || this.InterestId.Length > 128 || this.Deadline > owner.Deadline)
        {
            throw new CacheTransferException(CacheErrorCode.Corruption);
        }
    }

    /// <summary>Builds the index range containing only interests for the supplied generated flight ID.</summary>
    public static CacheIndexRange Range(string flightId)
    {
        if (!Guid.TryParseExact(flightId, "N", out _)) { throw new CacheTransferException(CacheErrorCode.Validation); }

        return CacheMetadataIndex.PrefixRange(new byte[] { 47 }.Concat(Convert.FromHexString(flightId)).ToArray());
    }

    /// <summary>Builds the fixed-width index key for a validated flight and interest pair.</summary>
    public static CacheIndexKey Key(string flightId, string interestId)
    {
        _ = CacheIdentityNormalizer.Name(interestId);
        var prefix = Range(flightId).Lower.Bytes[..17].ToArray();
        return new(prefix.Concat(SHA256.HashData(Encoding.UTF8.GetBytes(interestId))).ToArray());
    }
}