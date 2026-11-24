// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

using BridgingIT.DevKit.Application.Caching;

/// <summary>Persists source ownership, publication fencing, deadlines and the bounded terminal outcome of one source flight.</summary>
public sealed record CacheNativeOwnership(int Format, CacheEntryIdentity Identity, string FlightId, string OwnerToken, string FenceId, string NodeId,
    CacheOwnershipState State, DateTimeOffset Deadline, DateTimeOffset LeaseExpiry, bool Independent, CacheEntryVersion? Version = null,
    CacheErrorCode? Failure = null, bool IsNotFound = false, DateTimeOffset? CompletedAt = null)
{
    /// <summary>Rejects malformed ownership metadata or a record belonging to another exact identity and owner.</summary>
    public void Validate(CacheEntryIdentity identity, string owner)
    {
        if (this.Format != 1 || this.Identity != identity || this.OwnerToken != owner || !Guid.TryParseExact(this.OwnerToken, "N", out _) ||
            !Guid.TryParseExact(this.FlightId, "N", out _) || !Guid.TryParseExact(this.FenceId, "N", out _) || string.IsNullOrEmpty(this.NodeId) ||
            this.NodeId.Length > 128 || !Enum.IsDefined(this.State) || this.State == CacheOwnershipState.Pending || this.LeaseExpiry > this.Deadline ||
            (this.Version.HasValue && !Guid.TryParseExact(this.Version.Value.Value, "N", out _)) || (this.Failure.HasValue && !Enum.IsDefined(this.Failure.Value)))
        {
            throw new CacheTransferException(CacheErrorCode.Corruption);
        }
    }

    /// <summary>Builds the ownership index key from the full canonical identity digest and validated owner token.</summary>
    public static CacheIndexKey Key(CacheEntryIdentity identity, string owner)
    {
        if (!Guid.TryParseExact(owner, "N", out _)) { throw new CacheTransferException(CacheErrorCode.Validation); }

        return new(new byte[] { 43 }.Concat(Convert.FromHexString(identity.Digest)).Concat(Convert.FromHexString(owner)).ToArray());
    }
}

/// <summary>Tracks the logical key epoch, outstanding captures and read-fence sequence across value contracts.</summary>
public sealed record CacheNativeKeyState(int Format, CacheEntryIdentity Identity, long Epoch, int Captures, long ReadFenceSequence = 0)
{
    /// <summary>Verifies canonical logical-key identity and nonnegative counters, including the maximum of 128 outstanding captures.</summary>
    public void Validate(CacheEntryIdentity identity)
    {
        if (this.Format != 1 || this.Identity != LogicalIdentity(identity) || this.Epoch < 0 || this.ReadFenceSequence < 0 || this.Captures is < 0 or > 128)
        {
            throw new CacheTransferException(CacheErrorCode.Corruption);
        }
    }

    /// <summary>Maps a value identity to the shared logical-key identity used to fence every contract version of that key.</summary>
    public static CacheEntryIdentity LogicalIdentity(CacheEntryIdentity identity) => new(identity.Namespace, identity.Collection, "logical-key", identity.Key);

    /// <summary>Builds the shared logical-key fencing index key.</summary>
    public static CacheIndexKey Key(CacheEntryIdentity identity) => new(new byte[] { 40 }.Concat(Convert.FromHexString(LogicalIdentity(identity).Digest)).ToArray());
}

/// <summary>Tracks the publication sequence and outstanding captures for one exact contract-qualified cache identity.</summary>
public sealed record CacheNativeEntryState(int Format, CacheEntryIdentity Identity, long PublicationSequence, int Captures)
{
    /// <summary>Rejects a different exact identity, negative publication sequence or capture count outside zero through 128.</summary>
    public void Validate(CacheEntryIdentity identity)
    {
        if (this.Format != 1 || this.Identity != identity || this.PublicationSequence < 0 || this.Captures is < 0 or > 128)
        {
            throw new CacheTransferException(CacheErrorCode.Corruption);
        }
    }

    /// <summary>Builds the exact-entry publication-state index key.</summary>
    public static CacheIndexKey Key(CacheEntryIdentity identity) => new(new byte[] { 42 }.Concat(Convert.FromHexString(identity.Digest)).ToArray());
}

/// <summary>Captures the root and key epochs, exact publication sequence and deadline authorizing one attempted write.</summary>
public sealed record CacheNativeFence(int Format, string Id, CacheEntryIdentity Identity, long KeyEpoch, long RootEpoch, long PublicationSequence,
    CacheEntryVersion? ObservedVersion, DateTimeOffset Deadline)
{
    /// <summary>Rejects malformed fence IDs, mismatched identities, invalid versions and negative captured counters.</summary>
    public void Validate(CacheEntryIdentity identity, string id)
    {
        if (this.Format != 1 || this.Id != id || !Guid.TryParseExact(this.Id, "N", out _) || this.Identity != identity || this.KeyEpoch < 0 ||
            this.RootEpoch < 0 || this.PublicationSequence < 0 || (this.ObservedVersion.HasValue && !Guid.TryParseExact(this.ObservedVersion.Value.Value, "N", out _)))
        {
            throw new CacheTransferException(CacheErrorCode.Corruption);
        }
    }

    /// <summary>Builds the captured-fence index key using the logical-key digest and generated fence ID.</summary>
    public static CacheIndexKey Key(CacheEntryIdentity identity, string id)
    {
        if (!Guid.TryParseExact(id, "N", out _)) { throw new CacheTransferException(CacheErrorCode.Validation); }

        return new(new byte[] { 41 }.Concat(Convert.FromHexString(CacheNativeKeyState.LogicalIdentity(identity).Digest)).Concat(Convert.FromHexString(id)).ToArray());
    }
}