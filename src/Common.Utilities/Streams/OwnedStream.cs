// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Delegates stream operations and releases an associated owner exactly once after disposing the underlying stream.</summary>
/// <remarks>Synchronous disposal blocks when the supplied release callback is asynchronous. Release still runs if stream disposal fails.</remarks>
/// <example><code>await using var stream = new OwnedStream(providerStream, lease.Dispose);</code></example>
public sealed class OwnedStream : Stream
{
    private readonly Stream inner;
    private readonly Action release;
    private readonly Func<ValueTask> releaseAsync;
    private int disposed;

    /// <summary>Associates a stream with a synchronous ownership-release callback.</summary>
    /// <param name="inner">The stream owned and disposed by this wrapper.</param>
    /// <param name="release">The callback invoked once after underlying disposal, including failed disposal.</param>
    /// <example><code>using var output = new OwnedStream(stream, resource.Dispose);</code></example>
    public OwnedStream(Stream inner, Action release)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        this.release = release ?? throw new ArgumentNullException(nameof(release));
    }

    /// <summary>Associates a stream with an asynchronous ownership-release callback.</summary>
    /// <param name="inner">The stream owned and disposed by this wrapper.</param>
    /// <param name="release">The callback awaited once after underlying disposal, including failed disposal.</param>
    /// <example><code>await using var output = new OwnedStream(stream, resource.DisposeAsync);</code></example>
    public OwnedStream(Stream inner, Func<ValueTask> release)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        this.releaseAsync = release ?? throw new ArgumentNullException(nameof(release));
    }

    /// <inheritdoc />
    public override bool CanRead => Volatile.Read(ref this.disposed) == 0 && this.inner.CanRead;

    /// <inheritdoc />
    public override bool CanWrite => Volatile.Read(ref this.disposed) == 0 && this.inner.CanWrite;

    /// <inheritdoc />
    public override bool CanSeek => Volatile.Read(ref this.disposed) == 0 && this.inner.CanSeek;

    /// <inheritdoc />
    public override long Length => this.inner.Length;

    /// <inheritdoc />
    public override long Position { get => this.inner.Position; set => this.inner.Position = value; }

    /// <inheritdoc />
    public override void Flush() => this.inner.Flush();

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken) => this.inner.FlushAsync(cancellationToken);

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => this.inner.Read(buffer, offset, count);

    /// <inheritdoc />
    public override int Read(Span<byte> buffer) => this.inner.Read(buffer);

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        this.inner.ReadAsync(buffer, offset, count, cancellationToken);

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        this.inner.ReadAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => this.inner.Write(buffer, offset, count);

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer) => this.inner.Write(buffer);

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        this.inner.WriteAsync(buffer, offset, count, cancellationToken);

    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        this.inner.WriteAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => this.inner.Seek(offset, origin);

    /// <inheritdoc />
    public override void SetLength(long value) => this.inner.SetLength(value);

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) != 0)
        {
            return;
        }

        try
        {
            await this.inner.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            if (this.releaseAsync is not null)
            {
                await this.releaseAsync().ConfigureAwait(false);
            }
            else
            {
                this.release();
            }
        }

        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && Interlocked.Exchange(ref this.disposed, 1) == 0)
        {
            try
            {
                this.inner.Dispose();
            }
            finally
            {
                if (this.releaseAsync is not null)
                {
                    this.releaseAsync().AsTask().GetAwaiter().GetResult();
                }
                else
                {
                    this.release();
                }
            }
        }

        base.Dispose(disposing);
    }
}