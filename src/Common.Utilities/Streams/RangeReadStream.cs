// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Exposes at most a fixed number of bytes from an already positioned readable stream.</summary>
/// <param name="inner">The source positioned at the requested logical range start. This wrapper never closes it.</param>
/// <param name="length">The nonnegative maximum range length in bytes.</param>
/// <remarks>Premature source exhaustion returns end-of-stream. The wrapper neither seeks the source nor verifies its total length.</remarks>
/// <example><code>using var range = new RangeReadStream(positionedSource, requestedLength);</code></example>
public sealed class RangeReadStream(Stream inner, long length) : Stream
{
    private readonly Stream source = Validate(inner, length);
    private readonly long length = length;
    private long position;
    private bool disposed;

    /// <inheritdoc />
    public override bool CanRead => !this.disposed && this.source.CanRead;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override long Length => this.length;

    /// <inheritdoc />
    public override long Position { get => this.position; set => throw new NotSupportedException(); }

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        var count = (int)Math.Min(buffer.Length, this.length - this.position);
        var read = count == 0 ? 0 : await this.source.ReadAsync(buffer[..count], cancellationToken).ConfigureAwait(false);
        this.position += read;
        return read;
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        this.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => this.Read(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    public override int Read(Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        var count = (int)Math.Min(buffer.Length, this.length - this.position);
        var read = count == 0 ? 0 : this.source.Read(buffer[..count]);
        this.position += read;
        return read;
    }

    /// <inheritdoc />
    public override void Flush() => ObjectDisposedException.ThrowIf(this.disposed, this);

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        this.disposed = true;
        base.Dispose(disposing);
    }

    private static Stream Validate(Stream inner, long length)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        if (!inner.CanRead) { throw new ArgumentException("A readable stream is required.", nameof(inner)); }

        return inner;
    }
}