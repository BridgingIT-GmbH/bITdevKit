// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Captures bounded owned persisted bytes during an ordinary read and promotes only after complete logical and stored validation.</summary>
/// <remarks>Incomplete, ranged, seeked, oversized, or failed reads never populate retention.</remarks>
/// <example><code>var retained = store.Count;</code></example>
public sealed class CacheHotRead : ICacheProviderReadHandle
{
    private readonly ICacheProviderReadHandle acquired;
    private readonly CacheHotStore store;
    private readonly CaptureStream capture;
    private readonly IDisposable promotion;
    private readonly ResourceReservation transient;
    private ICacheSharedPayloadLease shared;
    private ResourceCharge memory;
    private int disposed;

    private CacheHotRead(ICacheProviderReadHandle acquired, CacheHotStore store, IDisposable promotion,
        ICacheSharedPayloadLease shared, ResourceCharge memory, ResourceReservation transient, byte[] bytes)
    {
        this.acquired = acquired;
        this.store = store;
        this.promotion = promotion;
        this.shared = shared;
        this.memory = memory;
        this.transient = transient;
        this.capture = bytes is null ? null : new(acquired.Stream, bytes);
    }

    /// <inheritdoc />
    public Stream Stream => this.capture ?? this.acquired.Stream;

    /// <inheritdoc />
    public CacheEntryMetadata Metadata => this.acquired.Metadata;

    /// <summary>Attempts bounded second-access capture from an acquired snapshot; unsupported, oversized or unadmitted candidates return null.</summary>
    public static CacheHotRead TryCreate(ICacheProviderReadHandle acquired, CacheHotStore store, ResourceBudget transientBudget)
    {
        var metadata = acquired.Metadata;
        if (store.Options.Representation == CacheHotEntryRepresentation.Decoded || metadata.IsNotFound ||
            metadata.StoredSizeBytes < 0 || metadata.StoredSizeBytes > Math.Min(int.MaxValue - 32L, store.Options.MaxEntryBytes - 32L) ||
            !store.Admission.ShouldCapture(metadata)) { return null; }

        var promotion = store.Promotions.TryCapture(metadata.Identity, metadata.Version);
        if (promotion is null) { return null; }

        ICacheSharedPayloadLease shared = null;
        ResourceCharge memory = null;
        ResourceReservation transient = null;
        try
        {
            shared = (acquired as ICacheProviderSharedReadHandle)?.RetainSharedPayload();
            var capacity = shared?.CapacityBytes ?? metadata.StoredSizeBytes + 32;
            if (!store.TryReserveCapture(capacity, shared is not null, out memory)) { return null; }

            if (shared is null && !transientBudget.TryReserve(capacity, out transient)) { return null; }

            var created = new CacheHotRead(acquired, store, promotion, shared, memory, transient,
                shared is null ? GC.AllocateUninitializedArray<byte>((int)metadata.StoredSizeBytes) : null);
            promotion = null;
            shared = null;
            memory = null;
            transient = null;
            return created;
        }
        finally
        {
            promotion?.Dispose();
            shared?.Dispose();
            memory?.Dispose();
            transient?.Dispose();
        }
    }

    /// <summary>Promotes a captured representation only after the complete logical and stored payload has been validated.</summary>
    public void Validated()
    {
        if (this.capture is not null && !this.capture.Complete) { return; }

        if (this.store.TryRetainBinary(this.Metadata, this.capture?.Bytes, this.shared, this.memory))
        {
            this.shared = null;
            this.memory = null;
        }

        this.transient?.Dispose();
    }

    /// <summary>Releases unpromoted capture capacity and the ordinary protected provider handle.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) != 0) { return; }

        try { await this.acquired.DisposeAsync().ConfigureAwait(false); }
        finally
        {
            this.shared?.Dispose();
            this.memory?.Dispose();
            this.transient?.Dispose();
            this.promotion.Dispose();
        }
    }

    private sealed class CaptureStream(Stream inner, byte[] bytes) : Stream
    {
        private int written;
        private bool seeked;

        public byte[] Bytes => bytes;

        public bool Complete => !this.seeked && this.written == bytes.Length;

        /// <inheritdoc />
        public override bool CanRead => inner.CanRead;

        /// <inheritdoc />
        public override bool CanSeek => inner.CanSeek;

        /// <inheritdoc />
        public override bool CanWrite => false;

        /// <inheritdoc />
        public override long Length => inner.Length;

        /// <inheritdoc />
        public override long Position { get => inner.Position; set { this.seeked = true; inner.Position = value; } }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count) => this.Read(buffer.AsSpan(offset, count));

        /// <inheritdoc />
        public override int Read(Span<byte> buffer)
        {
            var count = inner.Read(buffer);
            this.Capture(buffer[..count]);
            return count;
        }

        /// <inheritdoc />
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var count = await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            this.Capture(buffer.Span[..count]);
            return count;
        }

        /// <inheritdoc />
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            this.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) { this.seeked = true; return inner.Seek(offset, origin); }

        /// <inheritdoc />
        public override void Flush() { }

        /// <inheritdoc />
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        private void Capture(ReadOnlySpan<byte> read)
        {
            if (this.seeked) { return; }

            if (read.Length > bytes.Length - this.written) { this.seeked = true; return; }

            read.CopyTo(bytes.AsSpan(this.written));
            this.written += read.Length;
        }
    }
}

/// <summary>Owns an independently positioned raw hot snapshot and its pinned resident charge.</summary>
/// <example><code>await using var handle = new CacheHotProviderReadHandle(pin, validatedMetadata);</code></example>
public sealed class CacheHotProviderReadHandle : ICacheProviderRangeReadHandle
{
    private CacheHotEntryLease pin;

    /// <summary>Opens raw immutable bytes after authoritative validation of this pin's exact entry version.</summary>
    public CacheHotProviderReadHandle(CacheHotEntryLease pin, CacheEntryMetadata metadata)
    {
        this.pin = pin;
        this.Metadata = metadata;
        this.Stream = pin.Representation.OpenRead();
    }

    /// <inheritdoc />
    public Stream Stream { get; }

    /// <inheritdoc />
    public CacheEntryMetadata Metadata { get; }

    /// <inheritdoc />
    public ValueTask<Result> PositionForRangeAsync(long storedOffset, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (storedOffset < 0 || storedOffset >= this.Metadata.StoredSizeBytes)
        {
            return ValueTask.FromResult(Result.Failure(new CacheError(CacheErrorCode.Validation, "The hot snapshot range is outside its stored representation.")));
        }

        this.Stream.Seek(storedOffset, SeekOrigin.Begin);
        return ValueTask.FromResult(Result.Success());
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        var owned = Interlocked.Exchange(ref this.pin, null);
        if (owned is null) { return; }

        try { await this.Stream.DisposeAsync().ConfigureAwait(false); }
        finally { owned.Dispose(); }
    }
}