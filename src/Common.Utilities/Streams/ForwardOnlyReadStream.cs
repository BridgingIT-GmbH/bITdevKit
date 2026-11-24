// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Exposes a readable stream without seeking or writing capabilities.</summary>
/// <param name="inner">The readable stream whose read methods receive the caller buffers.</param>
/// <param name="leaveOpen">Whether disposing this wrapper leaves the underlying stream open.</param>
/// <example><code>using var input = new ForwardOnlyReadStream(source, leaveOpen: true);</code></example>
public sealed class ForwardOnlyReadStream(Stream inner, bool leaveOpen = false) : Stream
{
    private readonly Stream source = Validate(inner);
    private int disposed;

    /// <inheritdoc />
    public override bool CanRead => Volatile.Read(ref this.disposed) == 0 && this.source.CanRead;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        this.ThrowIfDisposed();
        return this.source.ReadAsync(buffer, cancellationToken);
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        this.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => this.Read(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    public override int Read(Span<byte> buffer)
    {
        this.ThrowIfDisposed();
        return this.source.Read(buffer);
    }

    /// <inheritdoc />
    public override void Flush() => this.ThrowIfDisposed();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) == 0 && !leaveOpen)
        {
            await this.source.DisposeAsync().ConfigureAwait(false);
        }

        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && Interlocked.Exchange(ref this.disposed, 1) == 0 && !leaveOpen)
        {
            this.source.Dispose();
        }

        base.Dispose(disposing);
    }

    private static Stream Validate(Stream inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        if (!inner.CanRead) { throw new ArgumentException("A readable stream is required.", nameof(inner)); }

        return inner;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(Volatile.Read(ref this.disposed) != 0, this);
}