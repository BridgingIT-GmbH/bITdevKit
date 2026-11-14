// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Describes bounded same-store staging and atomic publication preconditions.</summary>
/// <param name="Identity">Canonical entry identity.</param>
/// <param name="Fence">Authority captured before cache-managed source work, or before direct staging.</param>
/// <param name="Condition">Optional expected-version/create-only condition.</param>
/// <param name="MaximumStoredBytes">An upper bound charged as staging grows.</param>
/// <param name="Owner">Confirmed shared ownership when required.</param>
/// <param name="OperationId">Stable operation identity for uncertain-acknowledgement reconciliation.</param>
/// <param name="Deadline">Optional fixed owned-work deadline, checked again at the atomic commit boundary.</param>
/// <example><code>await using var staging = (await provider.BeginWriteAsync(request)).Value;</code></example>
public sealed record CacheProviderWriteRequest(CacheEntryIdentity Identity, CachePublicationFence Fence,
    CacheWriteCondition Condition, long MaximumStoredBytes, CacheOwnerToken? Owner = null, string OperationId = null,
    DateTimeOffset? Deadline = null);

/// <summary>Owns bounded same-store staging. Disposal aborts uncommitted work without releasing uncertain committed quota.</summary>
/// <example><code>await codec.EncodeAsync(value, session.Stream); var outcome = await session.PublishAsync(metadata);</code></example>
public interface ICacheProviderWriteSession : IAsyncDisposable
{
    /// <summary>Gets the writable provider-owned staging stream; it is never a caller-owned pooled buffer.</summary>
    Stream Stream { get; }

    /// <summary>Gets the stable operation identity used for reconciliation.</summary>
    string OperationId { get; }

    /// <summary>Gets the store-issued unique staged version. Published metadata/envelopes must use this exact token.</summary>
    CacheEntryVersion Version { get; }

    /// <summary>Opens an independent protected staging reader after a flush, for bounded same-store validation/compression without repeating source input or encoding.</summary>
    ValueTask<Result<Stream>> OpenStagedReadAsync(CancellationToken cancellationToken = default);

    /// <summary>Atomically verifies identity, fence, owner, limits, and condition and publishes the complete staged representation.</summary>
    ValueTask<Result<CacheWriteOutcome>> PublishAsync(CacheEntryMetadata metadata, CancellationToken cancellationToken = default);
}