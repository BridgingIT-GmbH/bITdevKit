// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Owns an immutable read snapshot and retains its entry reference and reader admission until actual disposal.</summary>
/// <param name="stream">The independently positioned immutable stream.</param>
/// <param name="metadata">The immutable acquired publication metadata.</param>
/// <param name="entry">The pinned generation released after actual disposal.</param>
/// <param name="reader">Reader admission retained until actual disposal.</param>
/// <example><code>await using var handle = acquired; await handle.Stream.CopyToAsync(destination);</code></example>
public sealed class InMemoryCacheReadHandle(Stream stream, CacheEntryMetadata metadata,
    InMemoryCacheEntry entry, CacheAdmissionLease reader) : ICacheProviderRangeReadHandle, ICacheProviderSharedReadHandle
{
    private int disposed;

    /// <inheritdoc />
    public Stream Stream { get; } = stream;

    /// <inheritdoc />
    public CacheEntryMetadata Metadata { get; } = metadata;

    /// <inheritdoc />
    public ICacheSharedPayloadLease RetainSharedPayload()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref this.disposed) != 0, this);
        return entry.RetainPayload();
    }

    /// <inheritdoc />
    public ValueTask<Result> PositionForRangeAsync(long storedOffset, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Volatile.Read(ref this.disposed) != 0 || storedOffset < 0 || storedOffset >= this.Metadata.StoredSizeBytes)
        {
            return ValueTask.FromResult(Result.Failure(new CacheError(CacheErrorCode.Validation, "The protected stored range position is invalid.")));
        }

        this.Stream.Seek(storedOffset, SeekOrigin.Begin);
        return ValueTask.FromResult(Result.Success());
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) != 0)
        {
            return;
        }

        try
        {
            await this.Stream.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            entry.Release();
            reader.Dispose();
        }
    }
}