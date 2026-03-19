// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Wraps a writable stream and tracks the number of bytes written without requiring seek support.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="WriteStreamWrapper"/> class.
/// </remarks>
/// <param name="innerStream">The inner stream to wrap.</param>
/// <param name="leaveOpen">Whether disposing the wrapper should leave the inner stream open.</param>
public sealed class WriteStreamWrapper(Stream innerStream, bool leaveOpen = true) : Stream
{
    private readonly Stream innerStream = innerStream ?? throw new ArgumentNullException(nameof(innerStream));
    private readonly bool leaveOpen = leaveOpen;
    private long bytesWritten;
    private bool disposed;

    /// <summary>
    /// Gets the total number of bytes written through the wrapper.
    /// </summary>
    public long BytesWritten
    {
        get
        {
            if (this.innerStream.CanSeek)
            {
                try
                {
                    return this.innerStream.Length;
                }
                catch (NotSupportedException)
                {
                }
            }

            return this.bytesWritten;
        }
    }

    public override bool CanRead => this.innerStream.CanRead;

    public override bool CanSeek => this.innerStream.CanSeek;

    public override bool CanWrite => this.innerStream.CanWrite;

    public override long Length => this.innerStream.Length;

    public override long Position
    {
        get => this.innerStream.Position;
        set => this.innerStream.Position = value;
    }

    public override void Flush()
    {
        this.ThrowIfDisposed();
        this.innerStream.Flush();
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        this.ThrowIfDisposed();
        return this.innerStream.FlushAsync(cancellationToken);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        this.ThrowIfDisposed();
        return this.innerStream.Read(buffer, offset, count);
    }

    public override int Read(Span<byte> buffer)
    {
        this.ThrowIfDisposed();
        return this.innerStream.Read(buffer);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        this.ThrowIfDisposed();
        return this.innerStream.ReadAsync(buffer, offset, count, cancellationToken);
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        this.ThrowIfDisposed();
        return this.innerStream.ReadAsync(buffer, cancellationToken);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        this.ThrowIfDisposed();
        return this.innerStream.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        this.ThrowIfDisposed();
        this.innerStream.SetLength(value);

        if (!this.innerStream.CanSeek)
        {
            this.bytesWritten = value;
        }
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        this.ThrowIfDisposed();
        this.innerStream.Write(buffer, offset, count);
        this.bytesWritten += count;
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        this.ThrowIfDisposed();
        this.innerStream.Write(buffer);
        this.bytesWritten += buffer.Length;
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        this.ThrowIfDisposed();
        await this.innerStream.WriteAsync(buffer, offset, count, cancellationToken);
        this.bytesWritten += count;
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        this.ThrowIfDisposed();
        await this.innerStream.WriteAsync(buffer, cancellationToken);
        this.bytesWritten += buffer.Length;
    }

    public override void WriteByte(byte value)
    {
        this.ThrowIfDisposed();
        this.innerStream.WriteByte(value);
        this.bytesWritten++;
    }

    protected override void Dispose(bool disposing)
    {
        if (this.disposed)
        {
            return;
        }

        if (disposing && !this.leaveOpen)
        {
            this.innerStream.Dispose();
        }

        this.disposed = true;
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        if (this.disposed)
        {
            return;
        }

        if (!this.leaveOpen)
        {
            await this.innerStream.DisposeAsync();
        }

        this.disposed = true;
        await base.DisposeAsync();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
    }
}
