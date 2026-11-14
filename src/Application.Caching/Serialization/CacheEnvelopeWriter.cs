// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

// Payload frames are independently checked for partial reads. Whole-entry hashes include all
// persisted headers, framing, data, and the footer; the footer never hashes itself recursively.
/// <summary>Writes an explicit versioned envelope with independently checked frames and complete stored-byte integrity.</summary>
/// <remarks>CompleteAsync emits the final short frame and footer. Disposal releases buffers without completing the envelope or closing the provider-owned destination.</remarks>
/// <example><code>var created = await CacheEnvelopeWriter.CreateAsync(destination, envelope, transient, resident, cancellationToken);</code></example>
public sealed class CacheEnvelopeWriter : Stream
{
    /// <summary>The persisted maximum payload bytes per independently checked frame.</summary>
    public const int BlockSize = 65536;

    /// <summary>The persisted eight-byte envelope signature; callers must not modify its contents.</summary>
    public static readonly byte[] Magic = "BDKCACHE"u8.ToArray();
    private readonly Stream destination;
    private readonly CacheEntryEnvelope envelope;
    private readonly ResourceCharge reservation;
    private readonly IncrementalHash bodyHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private readonly IncrementalHash storedHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private readonly byte[] frame = new byte[36];
    private byte[] buffer = GC.AllocateUninitializedArray<byte>(BlockSize);
    private int buffered;
    private long bodyBytes;
    private long storedBytes;
    private bool completed;
    private bool failed;
    private bool disposed;

    private CacheEnvelopeWriter(Stream destination, CacheEntryEnvelope envelope, ResourceCharge reservation)
    {
        this.destination = destination;
        this.envelope = envelope;
        this.reservation = reservation;
    }

    /// <inheritdoc />
    public override bool CanRead => false;

    /// <inheritdoc />
    public override bool CanWrite => !this.completed && !this.failed && !this.disposed;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override long Length => this.bodyBytes;

    /// <inheritdoc />
    public override long Position { get => this.bodyBytes; set => throw new NotSupportedException(); }

    /// <summary>Reserves frame storage and writes the bounded explicit envelope header to an existing staging destination.</summary>
    /// <param name="destination">The provider-owned staging stream, which remains caller-owned.</param>
    /// <param name="envelope">The supported versioned header with at most eight transformation descriptors.</param>
    /// <param name="transient">The process transient budget charged before allocation.</param>
    /// <param name="resident">The applicable collection resident budget.</param>
    /// <param name="cancellationToken">Cancellation forwarded to persisted header writes.</param>
    /// <returns>The admitted writer, or a capacity failure when its bounded workspace cannot be reserved.</returns>
    /// <exception cref="InvalidDataException">The envelope header is unsupported or exceeds its bound.</exception>
    public static async ValueTask<Result<CacheEnvelopeWriter>> CreateAsync(Stream destination, CacheEntryEnvelope envelope,
        ResourceBudget transient, ResourceBudget resident, CancellationToken cancellationToken)
    {
        if (!ResourceCharge.TryReserve(BlockSize + 8192, out var reservation, transient, resident))
        {
            return Result<CacheEnvelopeWriter>.Failure(new CacheError(CacheErrorCode.Capacity, "Envelope buffer admission was rejected."));
        }

        var writer = new CacheEnvelopeWriter(destination, envelope, reservation);
        try
        {
            var header = JsonSerializer.SerializeToUtf8Bytes(envelope);
            if (header.Length > 4096 || envelope.EnvelopeVersion is not (1 or 2) || envelope.BlockSize != BlockSize || envelope.Transforms is null ||
                envelope.Transforms.Count > 8 || (envelope.EnvelopeVersion == 1 && envelope.Transforms.Count != 0) ||
                (envelope.EnvelopeVersion == 2 && (envelope.Transforms.Count == 0 || envelope.Compression != "stack")))
            {
                throw new InvalidDataException("Unsupported envelope header.");
            }

            await writer.WriteStoredAsync(Magic, cancellationToken).ConfigureAwait(false);
            var length = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(length, header.Length);
            await writer.WriteStoredAsync(length, cancellationToken).ConfigureAwait(false);
            await writer.WriteStoredAsync(header, cancellationToken).ConfigureAwait(false);
            return Result<CacheEnvelopeWriter>.Success(writer);
        }
        catch
        {
            writer.Dispose();
            throw;
        }
    }

    /// <summary>Writes the last frame and footer, flushes the destination, and prevents further payload writes.</summary>
    /// <param name="cancellationToken">Cancellation forwarded to frame, footer, and flush operations.</param>
    /// <param name="logicalBytes">The original logical byte count, or null to use the untransformed body count.</param>
    /// <param name="logicalHash">The original logical hash, or null to use the body digest.</param>
    /// <returns>Exact finalized sizes, logical hash, stored hash, and persisted compression identifier.</returns>
    /// <exception cref="InvalidOperationException">The envelope already completed or a previous write failed.</exception>
    public async ValueTask<CacheEncodedPayload> CompleteAsync(CancellationToken cancellationToken,
        long? logicalBytes = null, string logicalHash = null)
    {
        this.EnsureWritable();
        try
        {
            if (this.buffered > 0)
            {
                await this.FlushFrameAsync(cancellationToken).ConfigureAwait(false);
            }

            var actualBodyHash = Convert.ToHexStringLower(this.bodyHash.GetHashAndReset());
            var logical = logicalBytes ?? this.bodyBytes;
            var hash = logicalHash ?? actualBodyHash;
            var footer = JsonSerializer.SerializeToUtf8Bytes(new CacheEntryEnvelopeFooter(logical, this.bodyBytes, hash, actualBodyHash));
            var terminal = new byte[8];
            BinaryPrimitives.WriteInt32LittleEndian(terminal.AsSpan(4), footer.Length);
            await this.WriteStoredAsync(terminal, cancellationToken).ConfigureAwait(false);
            await this.WriteStoredAsync(footer, cancellationToken).ConfigureAwait(false);
            await this.destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            this.completed = true;
            return new(logical, this.bodyBytes, this.storedBytes, hash,
                Convert.ToHexStringLower(this.storedHash.GetHashAndReset()), this.envelope.Compression);
        }
        catch
        {
            this.failed = true;
            throw;
        }
    }

    /// <inheritdoc />
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> input, CancellationToken cancellationToken = default)
    {
        this.EnsureWritable();
        try
        {
            while (!input.IsEmpty)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var count = Math.Min(input.Length, BlockSize - this.buffered);
                input.Span[..count].CopyTo(this.buffer.AsSpan(this.buffered));
                this.bodyHash.AppendData(input.Span[..count]);
                this.bodyBytes += count;
                this.buffered += count;
                input = input[count..];
                if (this.buffered == BlockSize)
                {
                    await this.FlushFrameAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch
        {
            this.failed = true;
            throw;
        }
    }

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        this.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override void Write(byte[] input, int offset, int count) => this.Write(input.AsSpan(offset, count));

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> input)
    {
        this.EnsureWritable();
        try
        {
            while (!input.IsEmpty)
            {
                var count = Math.Min(input.Length, BlockSize - this.buffered);
                input[..count].CopyTo(this.buffer.AsSpan(this.buffered));
                this.bodyHash.AppendData(input[..count]);
                this.bodyBytes += count;
                this.buffered += count;
                input = input[count..];
                if (this.buffered == BlockSize)
                {
                    this.FlushFrame();
                }
            }
        }
        catch
        {
            this.failed = true;
            throw;
        }
    }

    /// <inheritdoc />
    public override void Flush()
    {
        this.EnsureWritable();
        // Do not emit a short middle frame; only completion can emit the last partial frame.
        this.destination.Flush();
    }

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        this.EnsureWritable();
        return this.destination.FlushAsync(cancellationToken);
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
        if (disposing && !this.disposed)
        {
            this.disposed = true;
            this.buffer = null;
            this.bodyHash.Dispose();
            this.storedHash.Dispose();
            this.reservation.Dispose();
        }

        base.Dispose(disposing); // The provider session owns the destination.
    }

    private async ValueTask FlushFrameAsync(CancellationToken cancellationToken)
    {
        BinaryPrimitives.WriteInt32LittleEndian(this.frame, this.buffered);
        SHA256.HashData(this.buffer.AsSpan(0, this.buffered), this.frame.AsSpan(4));
        await this.WriteStoredAsync(this.frame, cancellationToken).ConfigureAwait(false);
        await this.WriteStoredAsync(this.buffer.AsMemory(0, this.buffered), cancellationToken).ConfigureAwait(false);
        this.buffered = 0;
    }

    private void FlushFrame()
    {
        BinaryPrimitives.WriteInt32LittleEndian(this.frame, this.buffered);
        SHA256.HashData(this.buffer.AsSpan(0, this.buffered), this.frame.AsSpan(4));
        this.WriteStored(this.frame);
        this.WriteStored(this.buffer.AsSpan(0, this.buffered));
        this.buffered = 0;
    }

    private async ValueTask WriteStoredAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        await this.destination.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        this.storedHash.AppendData(bytes.Span);
        this.storedBytes += bytes.Length;
    }

    private void WriteStored(ReadOnlySpan<byte> bytes)
    {
        this.destination.Write(bytes);
        this.storedHash.AppendData(bytes);
        this.storedBytes += bytes.Length;
    }

    private void EnsureWritable()
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        if (this.completed || this.failed)
        {
            throw new InvalidOperationException("The envelope destination is completed or failed.");
        }
    }
}