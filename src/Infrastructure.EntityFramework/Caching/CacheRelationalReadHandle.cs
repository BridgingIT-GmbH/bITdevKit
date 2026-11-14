// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;

using BridgingIT.DevKit.Application.Caching;

/// <summary>Owns one immutable relational payload chunk and its resident-memory reservation.</summary>
/// <param name="Offset">The absolute stored-payload byte offset.</param>
/// <param name="Data">The verified bounded immutable chunk bytes.</param>
/// <param name="Memory">The memory charge retained until the chunk is disposed.</param>
/// <example><code>using var chunk = loaded.Value; chunk.Data.CopyTo(destination);</code></example>
public sealed record CacheRelationalChunk(long Offset, byte[] Data, ResourceCharge Memory) : IDisposable
{
    /// <inheritdoc />
    public void Dispose() => this.Memory.Dispose();
}

/// <summary>Protects a relational immutable payload generation for one sequential or explicitly positioned reader.</summary>
/// <param name="metadata">The acquired immutable metadata snapshot.</param>
/// <param name="inline">The optional bounded inline payload bytes.</param>
/// <param name="memory">The resident-memory charge retained for the handle lifetime.</param>
/// <param name="read">Loads and verifies the chunk containing a requested stored-byte offset.</param>
/// <param name="release">Releases the durable reader lease after pending reads have settled.</param>
/// <example><code>await using var handle = acquired.Value; await handle.Stream.CopyToAsync(destination, token);</code></example>
public sealed class CacheRelationalReadHandle(CacheEntryMetadata metadata, byte[] inline, ResourceCharge memory,
    Func<long, CancellationToken, ValueTask<Result<CacheRelationalChunk>>> read, Func<ValueTask> release) : ICacheProviderRangeReadHandle
{
    private readonly NativeReadStream stream = new(metadata.StoredSizeBytes, inline, memory, read, release);

    /// <inheritdoc />
    public CacheEntryMetadata Metadata { get; } = metadata;

    /// <inheritdoc />
    public Stream Stream => this.stream;

    /// <inheritdoc />
    public ValueTask<Result> PositionForRangeAsync(long storedOffset, CancellationToken cancellationToken = default) =>
        this.stream.PositionRangeAsync(storedOffset, cancellationToken);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => this.stream.DisposeAsync();

    private sealed class NativeReadStream(long length, byte[] inline, ResourceCharge memory,
        Func<long, CancellationToken, ValueTask<Result<CacheRelationalChunk>>> read, Func<ValueTask> release) : Stream
    {
        private readonly Lock gate = new();
        private long position;
        private CacheRelationalChunk chunk;
        private bool reading;
        private bool closing;
        private TaskCompletionSource idle;
        private Task disposal;

        /// <inheritdoc />
        public override bool CanRead => !this.closing;

        /// <inheritdoc />
        public override bool CanWrite => false;

        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <inheritdoc />
        public override long Length => throw new NotSupportedException();

        /// <inheritdoc />
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        /// <inheritdoc />
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            lock (this.gate)
            {
                ObjectDisposedException.ThrowIf(this.closing, this);
                if (this.reading) { throw new InvalidOperationException("A protected cache stream has one independent position."); }

                this.reading = true;
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (buffer.IsEmpty || this.position >= length) { return 0; }

                if (inline is not null)
                {
                    var count = (int)Math.Min(buffer.Length, length - this.position);
                    inline.AsMemory((int)this.position, count).CopyTo(buffer);
                    this.position += count;
                    return count;
                }

                if (this.chunk is null || this.position < this.chunk.Offset || this.position >= this.chunk.Offset + this.chunk.Data.Length)
                {
                    this.chunk?.Dispose();
                    this.chunk = null;
                    var loaded = await read(this.position, cancellationToken).ConfigureAwait(false);
                    if (loaded.IsFailure) { throw new CacheTransferException(loaded.Errors.OfType<CacheError>().FirstOrDefault()?.Code ?? CacheErrorCode.ProviderUnavailable); }

                    this.chunk = loaded.Value;
                }

                var offset = checked((int)(this.position - this.chunk.Offset));
                var available = (int)Math.Min(Math.Min(buffer.Length, this.chunk.Data.Length - offset), length - this.position);
                this.chunk.Data.AsMemory(offset, available).CopyTo(buffer);
                this.position += available;
                return available;
            }
            finally
            {
                lock (this.gate)
                {
                    this.reading = false;
                    this.idle?.TrySetResult();
                }
            }
        }

        /// <summary>Positions a protected partial range only while the native reader is idle and the offset is valid.</summary>
        /// <param name="offset">The logical selected starting byte, within the immutable snapshot length.</param>
        /// <param name="cancellationToken">The cancellation token checked before changing the protected cursor.</param>
        /// <returns>Success, or a validation failure if another read is active or the offset is outside the snapshot.</returns>
        public ValueTask<Result> PositionRangeAsync(long offset, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (this.gate)
            {
                ObjectDisposedException.ThrowIf(this.closing, this);
                if (this.reading || offset < 0 || offset > length)
                {
                    return ValueTask.FromResult(Result.Failure(new CacheError(CacheErrorCode.Validation, "The protected range position is invalid or busy.")));
                }

                this.position = offset;
                return ValueTask.FromResult(Result.Success());
            }
        }

        /// <inheritdoc />
        public override ValueTask DisposeAsync()
        {
            lock (this.gate)
            {
                if (this.disposal is not null) { return new(this.disposal); }

                this.closing = true;
                var wait = this.reading ? (this.idle = new(TaskCreationOptions.RunContinuationsAsynchronously)).Task : Task.CompletedTask;
                this.disposal = this.DisposeCoreAsync(wait);
                return new(this.disposal);
            }
        }

        private async Task DisposeCoreAsync(Task wait)
        {
            await wait.ConfigureAwait(false);
            try { await release().ConfigureAwait(false); }
            finally
            {
                this.chunk?.Dispose();
                memory.Dispose();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { this.DisposeAsync().AsTask().GetAwaiter().GetResult(); }

            base.Dispose(disposing);
        }

        /// <inheritdoc />
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => this.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count) => this.ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

        /// <inheritdoc />
        public override void Flush() { }

        /// <inheritdoc />
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}