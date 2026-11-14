// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

// Each frame is checked before exposing any of its bytes. Sequential EOF additionally checks
// the footer and the complete stored representation. Seeking deliberately loses full validation.
/// <summary>Checks persisted header compatibility and each stored frame before exposing its payload bytes.</summary>
/// <remarks>Sequential EOF verifies the footer and full stored representation. Seeking or positioned ranges establish only affected-frame integrity. Disposal releases reader buffers and leaves source ownership with its provider handle.</remarks>
/// <example><code>var opened = await CacheEnvelopeReader.OpenAsync(source, metadata, transient, resident, cancellationToken);</code></example>
public sealed class CacheEnvelopeReader : Stream
{
    private readonly Stream source;
    private readonly CacheEntryMetadata metadata;
    private readonly ResourceCharge reservation;
    private readonly IncrementalHash storedHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private readonly IncrementalHash bodyHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private readonly byte[] frame = new byte[36];
    private byte[] buffer;
    private long payloadOffset;
    private long storedBytes;
    private long bodyBytes;
    private long position;
    private int available;
    private int offset;
    private bool shortFrame;
    private bool partial;
    private bool ended;
    private bool disposed;

    private CacheEnvelopeReader(Stream source, CacheEntryMetadata metadata, ResourceCharge reservation)
    {
        this.source = source;
        this.metadata = metadata;
        this.reservation = reservation;
        this.buffer = GC.AllocateUninitializedArray<byte>((int)Math.Max(1, Math.Min(CacheEnvelopeWriter.BlockSize, metadata.StoredSizeBytes)));
    }

    /// <summary>Gets the parsed envelope header after it has matched the acquired metadata.</summary>
    public CacheEntryEnvelope Envelope { get; private set; }

    /// <inheritdoc />
    public override bool CanRead => !this.disposed;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override bool CanSeek => !this.disposed && this.source.CanSeek && this.Envelope.Compression == "none";

    /// <inheritdoc />
    public override long Length => this.CanSeek ? this.metadata.EncodedSizeBytes : throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position { get => this.position; set => this.Seek(value, SeekOrigin.Begin); }

    /// <summary>Reserves the frame buffer and validates the persisted header against the acquired metadata.</summary>
    /// <param name="source">The acquired native representation positioned at its header; ownership remains with the caller.</param>
    /// <param name="metadata">The authoritative metadata that the persisted header must match.</param>
    /// <param name="transient">The process transient budget charged before buffer allocation.</param>
    /// <param name="resident">The applicable collection resident budget.</param>
    /// <param name="cancellationToken">Cancellation forwarded to header reads.</param>
    /// <returns>The admitted reader, or compatibility, capacity, or corruption failure.</returns>
    public static async ValueTask<Result<CacheEnvelopeReader>> OpenAsync(Stream source, CacheEntryMetadata metadata,
        ResourceBudget transient, ResourceBudget resident, CancellationToken cancellationToken)
    {
        if (metadata.EnvelopeVersion is not (1 or 2) || (metadata.EnvelopeVersion == 1 && metadata.Compression is not (null or "none" or "gzip" or "brotli")) ||
            (metadata.EnvelopeVersion == 2 && metadata.Compression != "stack"))
        {
            return Result<CacheEnvelopeReader>.Failure(new CacheError(CacheErrorCode.Compatibility, "The persisted payload format is not registered."));
        }

        if (!ResourceCharge.TryReserve(Math.Max(1, Math.Min(CacheEnvelopeWriter.BlockSize, metadata.StoredSizeBytes)) + 8192,
            out var reservation, transient, resident))
        {
            return Result<CacheEnvelopeReader>.Failure(new CacheError(CacheErrorCode.Capacity, "Envelope reader admission was rejected."));
        }

        var reader = new CacheEnvelopeReader(source, metadata, reservation);
        try
        {
            var prefix = new byte[12];
            await reader.ReadStoredExactlyAsync(prefix, cancellationToken).ConfigureAwait(false);
            if (!prefix.AsSpan(0, 8).SequenceEqual(CacheEnvelopeWriter.Magic))
            {
                throw new InvalidDataException("The cache envelope signature is invalid.");
            }

            var length = BinaryPrimitives.ReadInt32LittleEndian(prefix.AsSpan(8));
            if (length is < 2 or > 4096)
            {
                throw new InvalidDataException("The cache envelope header length is invalid.");
            }

            var header = new byte[length];
            await reader.ReadStoredExactlyAsync(header, cancellationToken).ConfigureAwait(false);
            reader.Envelope = JsonSerializer.Deserialize<CacheEntryEnvelope>(header) ?? throw new InvalidDataException("The cache envelope header is missing.");
            var envelope = reader.Envelope;
            if (envelope.EnvelopeVersion != metadata.EnvelopeVersion || envelope.BlockSize != CacheEnvelopeWriter.BlockSize ||
                envelope.Transforms is null || envelope.Transforms.Count > 8 || (envelope.EnvelopeVersion == 1 && envelope.Transforms.Count != 0) ||
                (envelope.EnvelopeVersion == 2 && envelope.Transforms.Count == 0) ||
                envelope.IdentityDigest != metadata.Identity.Digest || envelope.EntryVersion != metadata.Version.Value ||
                envelope.ContractId != metadata.ContractId || envelope.CodecId != metadata.CodecId ||
                envelope.CodecVersion != metadata.CodecVersion || envelope.Compression != (metadata.Compression ?? "none"))
            {
                throw new InvalidDataException("The cache envelope does not match its acquired metadata.");
            }

            reader.payloadOffset = reader.storedBytes;
            return Result<CacheEnvelopeReader>.Success(reader);
        }
        catch (Exception exception) when (exception is InvalidDataException or EndOfStreamException or JsonException)
        {
            reader.Dispose();
            return Result<CacheEnvelopeReader>.Failure(new CacheError(CacheErrorCode.Corruption, "The acquired cache envelope is invalid."));
        }
        catch
        {
            reader.Dispose();
            throw;
        }
    }

    /// <summary>Checks sequential terminal framing without turning a partial read into a full-entry integrity claim.</summary>
    /// <param name="partial">Whether only already checked affected frames are required.</param>
    /// <param name="cancellationToken">Cancellation forwarded to the terminal EOF probe.</param>
    /// <returns>Success for the applicable verification scope, or corruption when unexpected transformed bytes remain.</returns>
    public async ValueTask<Result> VerifyAsync(bool partial, CancellationToken cancellationToken)
    {
        if (partial || this.partial)
        {
            return Result.Success(); // Already delivered frames were checked; never claims whole-entry validation.
        }

        if (!this.ended)
        {
            var probe = new byte[1];
            if (await this.ReadAsync(probe, cancellationToken).ConfigureAwait(false) != 0)
            {
                return Result.Failure(new CacheError(CacheErrorCode.Corruption, "The transformed payload has trailing bytes."));
            }
        }

        return Result.Success();
    }

    /// <summary>Positions an uncompressed acquired representation at its containing checked frame for a logical range.</summary>
    /// <param name="logicalOffset">The requested zero-based offset within the encoded logical bytes.</param>
    /// <param name="positionStored">The provider capability that positions the native snapshot at a stored offset.</param>
    /// <param name="cancellationToken">Cancellation forwarded to positioning and frame acquisition.</param>
    /// <returns>Success after frame validation, or unsupported, positioning, or truncated-frame failure.</returns>
    public async ValueTask<Result> PositionRangeAsync(long logicalOffset,
        Func<long, CancellationToken, ValueTask<Result>> positionStored, CancellationToken cancellationToken)
    {
        if (this.Envelope.Compression != "none" || logicalOffset < 0 || logicalOffset >= this.metadata.EncodedSizeBytes)
        {
            return Result.Failure(new CacheError(CacheErrorCode.Unsupported, "This representation does not support the requested checked range."));
        }

        var block = logicalOffset / CacheEnvelopeWriter.BlockSize;
        var positioned = await positionStored(checked(this.payloadOffset + block * (CacheEnvelopeWriter.BlockSize + 36L)), cancellationToken).ConfigureAwait(false);
        if (positioned.IsFailure)
        {
            return positioned;
        }

        this.partial = true;
        this.position = logicalOffset;
        this.ended = this.shortFrame = false;
        this.offset = this.available = 0;
        await this.ReadFrameAsync(cancellationToken).ConfigureAwait(false);
        this.offset = (int)(logicalOffset % CacheEnvelopeWriter.BlockSize);
        return this.offset < this.available ? Result.Success() : Result.Failure(new CacheError(CacheErrorCode.Corruption,
            "The acquired frame is shorter than its indexed logical position."));
    }

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        if (destination.IsEmpty || this.ended)
        {
            return 0;
        }

        if (this.offset == this.available)
        {
            await this.ReadFrameAsync(cancellationToken).ConfigureAwait(false);
            if (this.ended)
            {
                return 0;
            }
        }

        var count = Math.Min(destination.Length, this.available - this.offset);
        this.buffer.AsMemory(this.offset, count).CopyTo(destination);
        this.offset += count;
        this.position += count;
        return count;
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        this.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override int Read(byte[] destination, int offset, int count) =>
        this.ReadAsync(destination.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

    /// <inheritdoc />
    public override int Read(Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        if (destination.IsEmpty || this.ended)
        {
            return 0;
        }

        if (this.offset == this.available)
        {
            this.ReadFrameAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();
            if (this.ended)
            {
                return 0;
            }
        }

        var count = Math.Min(destination.Length, this.available - this.offset);
        this.buffer.AsSpan(this.offset, count).CopyTo(destination);
        this.offset += count;
        this.position += count;
        return count;
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin)
    {
        if (!this.CanSeek)
        {
            throw new NotSupportedException("This acquired representation does not support seeking.");
        }

        var target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => checked(this.position + offset),
            SeekOrigin.End => checked(this.Length + offset),
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };
        if (target < 0 || target > this.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        this.partial = true;
        this.position = target;
        this.ended = target == this.Length;
        this.shortFrame = false;
        this.offset = this.available = 0;
        if (!this.ended)
        {
            var block = target / CacheEnvelopeWriter.BlockSize;
            this.source.Position = checked(this.payloadOffset + block * (CacheEnvelopeWriter.BlockSize + 36L));
            this.ReadFrameAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();
            this.offset = (int)(target % CacheEnvelopeWriter.BlockSize);
            if (this.offset >= this.available)
            {
                throw new InvalidDataException("The acquired frame is shorter than its indexed position.");
            }
        }

        return target;
    }

    /// <inheritdoc />
    public override void Flush() { }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

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

        base.Dispose(disposing); // The acquired provider handle owns the source.
    }

    private async ValueTask ReadFrameAsync(CancellationToken cancellationToken)
    {
        await this.ReadStoredExactlyAsync(this.frame.AsMemory(0, 4), cancellationToken).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadInt32LittleEndian(this.frame);
        if (length == 0)
        {
            await this.ReadFooterAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        if (length < 0 || length > this.buffer.Length || this.shortFrame)
        {
            throw new InvalidDataException("The payload frame length/order is invalid.");
        }

        await this.ReadStoredExactlyAsync(this.frame.AsMemory(4, 32), cancellationToken).ConfigureAwait(false);
        await this.ReadStoredExactlyAsync(this.buffer.AsMemory(0, length), cancellationToken).ConfigureAwait(false);
        if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(this.buffer.AsSpan(0, length)), this.frame.AsSpan(4, 32)))
        {
            throw new InvalidDataException("The payload frame checksum is invalid.");
        }

        this.bodyHash.AppendData(this.buffer.AsSpan(0, length));
        this.bodyBytes = checked(this.bodyBytes + length);
        this.available = length;
        this.offset = 0;
        this.shortFrame = length < CacheEnvelopeWriter.BlockSize;
    }

    private async ValueTask ReadFooterAsync(CancellationToken cancellationToken)
    {
        await this.ReadStoredExactlyAsync(this.frame.AsMemory(0, 4), cancellationToken).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadInt32LittleEndian(this.frame);
        if (length is < 2 or > 4096)
        {
            throw new InvalidDataException("The cache envelope footer length is invalid.");
        }

        var bytes = new byte[length];
        await this.ReadStoredExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
        CacheEntryEnvelopeFooter footer;
        try
        {
            footer = JsonSerializer.Deserialize<CacheEntryEnvelopeFooter>(bytes) ?? throw new InvalidDataException("The cache footer is missing.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The cache footer is malformed.", exception);
        }

        var probe = new byte[1];
        if (await this.source.ReadAsync(probe, cancellationToken).ConfigureAwait(false) != 0 ||
            (!this.partial && (footer.LogicalBytes != this.metadata.EncodedSizeBytes || footer.BodyBytes != this.bodyBytes ||
                footer.LogicalHash != this.metadata.PayloadHash || footer.BodyHash != Convert.ToHexStringLower(this.bodyHash.GetHashAndReset()) ||
                this.storedBytes != this.metadata.StoredSizeBytes || Convert.ToHexStringLower(this.storedHash.GetHashAndReset()) != this.metadata.StoredHash)))
        {
            throw new InvalidDataException("The complete stored payload integrity check failed.");
        }

        this.ended = true;
        this.offset = this.available = 0;
    }

    private async ValueTask ReadStoredExactlyAsync(Memory<byte> destination, CancellationToken cancellationToken)
    {
        await this.source.ReadExactlyAsync(destination, cancellationToken).ConfigureAwait(false);
        this.storedHash.AppendData(destination.Span);
        this.storedBytes = checked(this.storedBytes + destination.Length);
        if (this.storedBytes > this.metadata.StoredSizeBytes && !this.partial)
        {
            throw new InvalidDataException("The acquired payload exceeds its stored size.");
        }
    }
}