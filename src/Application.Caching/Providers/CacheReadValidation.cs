// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Describes a read acquisition's representation and expected snapshot.</summary>
/// <param name="OpenPayload">Whether to acquire an independently positioned protected payload stream.</param>
/// <param name="ExpectedVersion">An optional optimistic version acquired earlier.</param>
/// <param name="RequireSeek">Whether seeking is required before the provider consumes payload bytes.</param>
/// <param name="ObserveAccess">Whether this is foreground activity and may renew authoritative sliding expiry.</param>
/// <example><code>var read = await provider.LookupAsync(identity, new(OpenPayload: true));</code></example>
public sealed record CacheReadRequest(bool OpenPayload = false, CacheEntryVersion? ExpectedVersion = null, bool RequireSeek = false,
    bool ObserveAccess = true);

/// <summary>Describes authoritative validity, acquired metadata, and optional owned read protection.</summary>
/// <param name="State">Missing/invalid, valid, or changed-version observation.</param>
/// <param name="Metadata">The same acquired authoritative snapshot, absent on a miss.</param>
/// <param name="Fence">Optional captured source authority; ordinary lookups return a default token without retaining source state.</param>
/// <param name="Handle">An acquired independent stream when requested, absent for markers/misses.</param>
/// <example><code>await using var handle = read.Value.Handle;</code></example>
public readonly record struct CacheReadValidation(
    CacheReadValidationState State, CacheEntryMetadata Metadata, CachePublicationFence Fence,
    ICacheProviderReadHandle Handle = null);

/// <summary>Identifies authoritative lookup/validation outcomes. Provider failures use the outer Result.</summary>
public enum CacheReadValidationState
{
    /// <summary>No logically valid version exists.</summary>
    Missing,

    /// <summary>A valid version, or the expected version, was acquired.</summary>
    Valid,

    /// <summary>The expected version is no longer current.</summary>
    Changed
}

/// <summary>Owns an independently positioned raw persisted snapshot and its reader/quota protection.</summary>
/// <example><code>await using var handle = result.Value.Handle; await handle.Stream.CopyToAsync(destination);</code></example>
public interface ICacheProviderReadHandle : IAsyncDisposable
{
    /// <summary>Gets the raw stored representation. The engine must validate its envelope and integrity.</summary>
    Stream Stream { get; }

    /// <summary>Gets metadata acquired with this exact payload version.</summary>
    CacheEntryMetadata Metadata { get; }
}

/// <summary>Positions one protected raw snapshot at an indexed stored offset for engine-managed checked ranges.</summary>
/// <remarks>This capability does not make a forward-only provider stream generally seekable. The engine checks complete affected frames before exposure.</remarks>
/// <example><code>var positioned = await range.PositionForRangeAsync(storedOffset, cancellationToken);</code></example>
public interface ICacheProviderRangeReadHandle : ICacheProviderReadHandle
{
    /// <summary>Moves this handle's independent raw cursor using an indexed immutable payload position, without scanning preceding payload content.</summary>
    ValueTask<Result> PositionForRangeAsync(long storedOffset, CancellationToken cancellationToken = default);
}