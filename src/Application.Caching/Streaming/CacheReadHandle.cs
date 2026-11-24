// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Owns one logical immutable read snapshot, its transfer limits, and terminal completion observation.</summary>
/// <example><code>await using var handle = lookup.Value; await handle.Stream.CopyToAsync(destination); var terminal = await handle.WaitForCompletionAsync();</code></example>
public sealed class CacheReadHandle : ICacheReadHandle
{
    private readonly CacheReadStream stream;
    private readonly Func<Action, CancellationToken, ValueTask<Result>> validateWhole;

    /// <summary>Associates one protected logical stream with metadata and optional explicit whole-entry validation.</summary>
    /// <param name="stream">The stream owning transfer cleanup and terminal observation.</param>
    /// <param name="metadata">Metadata for the same acquired immutable snapshot.</param>
    /// <param name="validateWhole">An optional verifier capable of checking the representation from its start.</param>
    /// <param name="range">The acquired logical range, or null for a full or seekable handle.</param>
    public CacheReadHandle(CacheReadStream stream, CacheEntryMetadata metadata,
        Func<Action, CancellationToken, ValueTask<Result>> validateWhole = null, CacheReadRange? range = null)
    {
        this.stream = stream;
        this.Metadata = metadata;
        this.validateWhole = validateWhole;
        this.Range = range;
    }

    /// <inheritdoc />
    public Stream Stream => this.stream;

    /// <inheritdoc />
    public CacheEntryMetadata Metadata { get; }

    /// <inheritdoc />
    public CacheReadRange? Range { get; }

    /// <inheritdoc />
    public CacheReadIntegrityScope IntegrityScope => this.stream.IsPartial ? CacheReadIntegrityScope.AffectedStoredFrames : CacheReadIntegrityScope.FullEntry;

    /// <inheritdoc />
    public ValueTask<Result<CacheReadCompletion>> WaitForCompletionAsync(CancellationToken cancellationToken = default) =>
        this.stream.WaitForCompletionAsync(cancellationToken);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => this.stream.DisposeAsync();

    /// <summary>Attaches the first optional transfer observation to the owned logical stream.</summary>
    /// <param name="session">The bounded observation session, or null.</param>
    public void Observe(CacheTransferObservationSession session) => this.stream.Observe(session);

    /// <summary>Runs the acquired representation verifier without claiming that partial consumption already validated the whole entry.</summary>
    /// <param name="cancellationToken">Cancellation forwarded to the verifier and transfer deadline scope.</param>
    /// <returns>The verification result, or Unsupported when no start-to-end verifier was supplied.</returns>
    public ValueTask<Result> ValidateWholeAsync(CancellationToken cancellationToken) => this.validateWhole is null ?
        ValueTask.FromResult(Result.Failure(new CacheError(CacheErrorCode.Unsupported, "The acquired representation cannot be validated from its start."))) :
        this.stream.ValidateWholeAsync(this.validateWhole, cancellationToken);
}