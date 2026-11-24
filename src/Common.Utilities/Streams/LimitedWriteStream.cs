// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Counts completed writes and rejects writes exceeding a byte limit without owning the destination.</summary>
/// <param name="destination">The writable destination that remains open when this wrapper is disposed.</param>
/// <param name="maximum">The maximum accepted byte count, including zero for an empty output.</param>
/// <remarks>A failed or excessive write permanently rejects the attempt, even when the producer catches its exception.
/// Inspect <see cref="CompletedSuccessfully"/> before disposing the wrapper. Concurrent writes are unsupported.</remarks>
/// <example><code>using var output = new LimitedWriteStream(destination, ByteSize.Megabytes(2));
/// await producer(output, cancellationToken);
/// if (!output.CompletedSuccessfully) { throw new InvalidDataException("The producer failed a write."); }</code></example>
public sealed class LimitedWriteStream(Stream destination, long maximum) : Stream
{
    private readonly Stream target = Validate(destination, maximum);
    private long written;
    private bool failed;
    private bool disposed;

    /// <summary>Gets bytes whose destination writes completed successfully.</summary>
    public long Written => this.written;

    /// <summary>Gets whether this wrapper remains open and no write or flush has failed.</summary>
    public bool CompletedSuccessfully => !this.failed && !this.disposed;

    /// <summary>Gets whether a destination or limit failure permanently rejected this write attempt.</summary>
    public bool HasFailed => this.failed;

    /// <inheritdoc />
    public override bool CanRead => false;

    /// <inheritdoc />
    public override bool CanWrite => !this.disposed && !this.failed && this.target.CanWrite;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override long Length => this.written;

    /// <inheritdoc />
    public override long Position { get => this.written; set => throw new NotSupportedException(); }

    /// <inheritdoc />
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        this.Check(buffer.Length);
        try
        {
            await this.target.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            this.written += buffer.Length;
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
    public override void Write(byte[] buffer, int offset, int count) => this.Write(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        this.Check(buffer.Length);
        try
        {
            this.target.Write(buffer);
            this.written += buffer.Length;
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
        this.Check(0);
        try { this.target.Flush(); }
        catch { this.failed = true; throw; }
    }

    /// <inheritdoc />
    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        this.Check(0);
        try { await this.target.FlushAsync(cancellationToken).ConfigureAwait(false); }
        catch { this.failed = true; throw; }
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
        this.disposed = true;
        base.Dispose(disposing);
    }

    private static Stream Validate(Stream destination, long maximum)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentOutOfRangeException.ThrowIfNegative(maximum);
        if (!destination.CanWrite) { throw new ArgumentException("A writable stream is required.", nameof(destination)); }

        return destination;
    }

    private void Check(int count)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        if (this.failed || count > maximum - this.written)
        {
            this.failed = true;
            var observed = count > long.MaxValue - this.written ? long.MaxValue : this.written + count;
            throw new StreamSizeLimitExceededException(maximum, observed);
        }
    }
}