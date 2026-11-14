// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using System.Numerics;

/// <summary>Owns incrementally charged bounded payload chunks and reference-counted immutable reader snapshots.</summary>
/// <example><code>await using var snapshot = payload.OpenRead();</code></example>
public sealed class InMemoryCachePayload
{
    private readonly ResourceBudget resident;
    private readonly ResourceReservation storage;
    private readonly ResourceReservation bookkeeping;
    private readonly ResourceReservation transient;
    private readonly long maximum;
    private readonly List<Chunk> chunks = [];
    private ChunkView[] frozen;
    private int references = 1;
    private long length;
    private bool writerClosed;
    private bool failed;

    private InMemoryCachePayload(ResourceBudget resident, ResourceReservation storage, ResourceReservation bookkeeping, ResourceReservation transient, long maximum)
    {
        this.resident = resident;
        this.storage = storage;
        this.bookkeeping = bookkeeping;
        this.transient = transient;
        this.maximum = maximum;
        this.Writer = new PayloadWriter(this);
    }

    /// <summary>Gets append-only provider-owned staging; a producer must not close it before publication.</summary>
    public Stream Writer { get; }

    /// <summary>Gets the populated stored bytes appended so far.</summary>
    public long Length => this.length;

    /// <summary>Gets native stored-byte reservations retained by this payload.</summary>
    public long StorageCharge => this.storage.Amount;

    /// <summary>Gets retained chunk capacity and payload bookkeeping bytes.</summary>
    public long ResidentCapacity => this.bookkeeping.Amount + this.chunks.Sum(chunk => chunk.Reservation.Amount);

    /// <summary>Gets the number of allocated bounded payload chunks.</summary>
    public int ChunkCount => this.chunks.Count;

    /// <summary>Gets whether the producer closed its staging destination.</summary>
    public bool WriterClosed => this.writerClosed;

    /// <summary>Reserves initial resident/storage/transient state before payload allocation; failure releases admitted charges.</summary>
    public static bool TryCreate(ResourceBudget resident, ResourceBudget storage, ResourceBudget transientBudget,
        long maximum, out InMemoryCachePayload payload)
    {
        payload = null;
        if (maximum <= 0 || !resident.TryReserve(1024, out var bookkeeping))
        {
            return false;
        }

        if (!storage.TryReserve(0, out var stored))
        {
            bookkeeping.Dispose();
            return false;
        }

        if (!transientBudget.TryReserve(1024, out var transient))
        {
            bookkeeping.Dispose();
            stored.Dispose();
            return false;
        }

        payload = new(resident, stored, bookkeeping, transient, maximum);
        return true;
    }

    /// <summary>Releases staging-only transient bookkeeping after confirmed retained publication.</summary>
    public void MarkPublished() => this.transient.Dispose();

    /// <summary>Retains an additional payload reference until a matching release.</summary>
    public void AddReference() => Interlocked.Increment(ref this.references);

    /// <summary>Computes SHA-256 over populated stored bytes, excluding spare chunk capacity.</summary>
    public string StoredHash()
    {
        using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        foreach (var chunk in this.chunks)
        {
            hash.AppendData(chunk.Buffer.AsSpan(0, chunk.Count));
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    /// <summary>Freezes immutable chunk views, rejecting failed or prematurely closed staging.</summary>
    public void Freeze()
    {
        ObjectDisposedException.ThrowIf(this.references == 0, this);
        if (this.writerClosed || this.failed)
        {
            throw new InvalidOperationException("The cache-owned staging destination was closed by its producer.");
        }

        this.frozen ??= this.chunks.Select(c => new ChunkView(c.Start, c.Buffer.AsMemory(0, c.Count))).ToArray();
    }

    /// <summary>Pins current chunk views and opens an independent seekable snapshot, reserving staging-view metadata as needed.</summary>
    public Stream OpenRead()
    {
        ObjectDisposedException.ThrowIf(this.references == 0, this);
        ResourceReservation viewsReservation = null;
        if (this.frozen is null && !this.resident.TryReserve(64 + this.chunks.Count * 32L, out viewsReservation))
        {
            throw new CacheCapacityException("Staging snapshot bookkeeping exceeded resident capacity.");
        }

        var views = this.frozen ?? this.chunks.Select(c => new ChunkView(c.Start, c.Buffer.AsMemory(0, c.Count))).ToArray();
        Interlocked.Increment(ref this.references);
        return new PayloadReader(this, views, this.length, viewsReservation);
    }

    /// <summary>Releases one reference and returns every payload charge after the final owner ends.</summary>
    public void Release()
    {
        if (Interlocked.Decrement(ref this.references) != 0)
        {
            return;
        }

        foreach (var chunk in this.chunks)
        {
            chunk.Reservation.Dispose();
        }

        this.chunks.Clear();
        this.chunks.TrimExcess();
        this.frozen = null;

        this.storage.Dispose();
        this.bookkeeping.Dispose();
        this.transient.Dispose();
    }

    private void Append(ReadOnlySpan<byte> bytes)
    {
        ObjectDisposedException.ThrowIf(this.writerClosed || this.references == 0, this);
        if (this.failed)
        {
            throw new InvalidOperationException("A failed cache staging stream cannot be reused or published.");
        }

        if (this.frozen is not null)
        {
            throw new InvalidOperationException("Published cache payloads are immutable.");
        }

        if (bytes.Length > this.maximum - this.length || !this.storage.TryGrow(bytes.Length))
        {
            this.failed = true;
            throw new CacheCapacityException("The cache entry or storage capacity was exceeded.");
        }

        while (!bytes.IsEmpty)
        {
            var chunk = this.chunks.Count == 0 ? null : this.chunks[^1];
            if (chunk is null || chunk.Count == chunk.Buffer.Length)
            {
                var capacity = (int)Math.Min(65536, Math.Max(256, BitOperations.RoundUpToPowerOf2((uint)Math.Min(65536, bytes.Length))));
                if (!this.transient.TryGrow(capacity + 384L) || !this.resident.TryReserve(capacity + 384L, out var reservation))
                {
                    this.failed = true;
                    throw new CacheCapacityException("Cache-owned payload capacity exceeded its resident budget.");
                }

                try
                {
                    chunk = new(GC.AllocateUninitializedArray<byte>(capacity), this.length, reservation);
                    this.chunks.Add(chunk);
                }
                catch
                {
                    this.failed = true;
                    reservation.Dispose();
                    throw;
                }
            }

            var count = Math.Min(bytes.Length, chunk.Buffer.Length - chunk.Count);
            bytes[..count].CopyTo(chunk.Buffer.AsSpan(chunk.Count));
            chunk.Count += count;
            this.length += count;
            bytes = bytes[count..];
        }
    }

    private sealed class Chunk(byte[] buffer, long start, ResourceReservation reservation)
    {
        /// <summary>Stores bounded owned chunk capacity; only populated bytes belong to the payload.</summary>
        public readonly byte[] Buffer = buffer;

        /// <summary>Stores the absolute starting byte within the payload.</summary>
        public readonly long Start = start;

        /// <summary>Retains this chunk's resident capacity charge.</summary>
        public readonly ResourceReservation Reservation = reservation;

        /// <summary>Stores the populated chunk byte count.</summary>
        public int Count;
    }

    private readonly record struct ChunkView(long Start, ReadOnlyMemory<byte> Content);

    private sealed class PayloadWriter(InMemoryCachePayload owner) : Stream
    {
        /// <inheritdoc />
        public override bool CanRead => false;

        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <inheritdoc />
        public override bool CanWrite => !owner.writerClosed && owner.frozen is null;

        /// <inheritdoc />
        public override long Length => owner.length;

        /// <inheritdoc />
        public override long Position { get => owner.length; set => throw new NotSupportedException(); }

        /// <inheritdoc />
        public override void Flush() => ObjectDisposedException.ThrowIf(owner.writerClosed, this);

        /// <inheritdoc />
        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            this.Flush();
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count) => owner.Append(buffer.AsSpan(offset, count));

        /// <inheritdoc />
        public override void Write(ReadOnlySpan<byte> buffer) => owner.Append(buffer);

        /// <inheritdoc />
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            owner.Append(buffer.Span);
            return ValueTask.CompletedTask;
        }

        /// <inheritdoc />
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            owner.Append(buffer.AsSpan(offset, count));
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                owner.writerClosed = true;
            }

            base.Dispose(disposing);
        }
    }

    private sealed class PayloadReader(InMemoryCachePayload owner, ChunkView[] chunks, long length, ResourceReservation views) : Stream
    {
        private InMemoryCachePayload owner = owner;
        private long position;

        /// <inheritdoc />
        public override bool CanRead => this.owner is not null;

        /// <inheritdoc />
        public override bool CanSeek => this.owner is not null;

        /// <inheritdoc />
        public override bool CanWrite => false;

        /// <inheritdoc />
        public override long Length => length;

        /// <inheritdoc />
        public override long Position
        {
            get => this.position;
            set
            {
                ObjectDisposedException.ThrowIf(this.owner is null, this);
                ArgumentOutOfRangeException.ThrowIfNegative(value);
                this.position = value;
            }
        }

        /// <inheritdoc />
        public override void Flush() { }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count) => this.Read(buffer.AsSpan(offset, count));

        /// <inheritdoc />
        public override int Read(Span<byte> destination)
        {
            ObjectDisposedException.ThrowIf(this.owner is null, this);
            var total = 0;
            while (!destination.IsEmpty && this.position < length)
            {
                var low = 0;
                var high = chunks.Length - 1;
                while (low < high)
                {
                    var middle = low + (high - low + 1) / 2;
                    if (chunks[middle].Start <= this.position)
                    {
                        low = middle;
                    }
                    else
                    {
                        high = middle - 1;
                    }
                }

                var chunk = chunks[low];
                var offset = (int)(this.position - chunk.Start);
                var count = (int)Math.Min(Math.Min(destination.Length, chunk.Content.Length - offset), length - this.position);
                chunk.Content.Span.Slice(offset, count).CopyTo(destination);
                this.position += count;
                destination = destination[count..];
                total += count;
            }

            return total;
        }

        /// <inheritdoc />
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(this.Read(buffer.Span));
        }

        /// <inheritdoc />
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(this.Read(buffer.AsSpan(offset, count)));
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin)
        {
            this.Position = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => checked(this.position + offset),
                SeekOrigin.End => checked(length + offset),
                _ => throw new ArgumentOutOfRangeException(nameof(origin))
            };
            return this.position;
        }

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Interlocked.Exchange(ref this.owner, null)?.Release();
                views?.Dispose();
                chunks = [];
            }

            base.Dispose(disposing);
        }
    }
}

/// <summary>Reports exhausted native in-process payload or index reservation capacity.</summary>
/// <param name="message">The bounded capacity failure description.</param>
/// <example><code>throw new CacheCapacityException("Payload capacity is exhausted.");</code></example>
public sealed class CacheCapacityException(string message) : IOException(message);