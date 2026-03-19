// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// A stream wrapper that provides progress reporting, cancellation support, and success/finalization callbacks for write operations to a file stream. It ensures that progress is reported after each write operation and that appropriate callbacks are invoked upon successful completion or finalization of the stream. The stream also handles exceptions and ensures that resources are properly disposed of, even in the case of errors.
/// </summary>
/// <param name="innerStream">The underlying stream to write to.</param>
/// <param name="progress">An optional progress reporter to report file progress.</param>
/// <param name="cancellationToken">An optional cancellation token to support cancellation of asynchronous operations.</param>
/// <param name="onSuccessAsync">An optional asynchronous callback to be invoked upon successful completion of the stream operations.</param>
/// <param name="onFinalizeAsync">An optional asynchronous callback to be invoked upon finalization of the stream, with a boolean parameter indicating whether the operations were successful.</param>
/// <remarks>
/// This class is designed to be used in scenarios where you want to write to a file stream
/// and need to report progress, handle cancellation, and perform specific actions upon success or finalization of the stream operations. It ensures that resources are properly managed and that progress is accurately reported based on the number of bytes written.
/// </remarks>
public class OpenWriteFileStream(
    Stream innerStream,
    IProgress<FileProgress> progress = null,
    CancellationToken cancellationToken = default,
    Func<Task> onSuccessAsync = null,
    Func<bool, Task> onFinalizeAsync = null) : Stream
{
    private readonly Stream innerStream = innerStream ?? throw new ArgumentNullException(nameof(innerStream));
    private readonly IProgress<FileProgress> progress = progress;
    private readonly CancellationToken cancellationToken = cancellationToken;
    private readonly Func<Task> onSuccessAsync = onSuccessAsync;
    private readonly Func<bool, Task> onFinalizeAsync = onFinalizeAsync;
    private bool disposed;
    private bool faulted;
    private long bytesWritten;

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

        try
        {
            this.innerStream.Flush();
            this.ReportProgress();
        }
        catch
        {
            this.faulted = true;
            throw;
        }
    }

    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        this.ThrowIfDisposed();

        try
        {
            await this.innerStream.FlushAsync(cancellationToken);
            this.ReportProgress();
        }
        catch
        {
            this.faulted = true;
            throw;
        }
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
        this.bytesWritten = this.GetBytesWritten();
        this.ReportProgress();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        this.ThrowIfDisposed();

        try
        {
            this.innerStream.Write(buffer, offset, count);
            this.bytesWritten = this.GetBytesWritten(count);
            this.ReportProgress();
        }
        catch
        {
            this.faulted = true;
            throw;
        }
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        this.ThrowIfDisposed();

        try
        {
            this.innerStream.Write(buffer);
            this.bytesWritten = this.GetBytesWritten(buffer.Length);
            this.ReportProgress();
        }
        catch
        {
            this.faulted = true;
            throw;
        }
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        this.ThrowIfDisposed();

        try
        {
            await this.innerStream.WriteAsync(buffer, offset, count, cancellationToken);
            this.bytesWritten = this.GetBytesWritten(count);
            this.ReportProgress();
        }
        catch
        {
            this.faulted = true;
            throw;
        }
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        this.ThrowIfDisposed();

        try
        {
            await this.innerStream.WriteAsync(buffer, cancellationToken);
            this.bytesWritten = this.GetBytesWritten(buffer.Length);
            this.ReportProgress();
        }
        catch
        {
            this.faulted = true;
            throw;
        }
    }

    public override void WriteByte(byte value)
    {
        this.ThrowIfDisposed();

        try
        {
            this.innerStream.WriteByte(value);
            this.bytesWritten = this.GetBytesWritten(1);
            this.ReportProgress();
        }
        catch
        {
            this.faulted = true;
            throw;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (this.disposed)
        {
            return;
        }

        if (disposing)
        {
            this.DisposeCoreAsync(sync: true).GetAwaiter().GetResult();
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

        await this.DisposeCoreAsync(sync: false);
        this.disposed = true;
        await base.DisposeAsync();
    }

    private async Task DisposeCoreAsync(bool sync)
    {
        Exception exception = null;
        var success = false;

        try
        {
            if (!this.faulted)
            {
                if (sync)
                {
                    this.innerStream.Flush();
                }
                else
                {
                    await this.innerStream.FlushAsync(this.cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            this.faulted = true;
            exception = ex;
        }

        try
        {
            if (sync)
            {
                this.innerStream.Dispose();
            }
            else
            {
                await this.innerStream.DisposeAsync();
            }
        }
        catch (Exception ex) when (exception is null)
        {
            this.faulted = true;
            exception = ex;
        }

        if (!this.faulted && exception is null)
        {
            try
            {
                if (this.onSuccessAsync is not null)
                {
                    await this.onSuccessAsync();
                }

                success = true;
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        }

        try
        {
            if (this.onFinalizeAsync is not null)
            {
                await this.onFinalizeAsync(success);
            }
        }
        catch (Exception ex) when (exception is null)
        {
            exception = ex;
        }
        finally
        {
            this.progress?.Report(new FileProgress
            {
                BytesProcessed = this.bytesWritten,
                FilesProcessed = success ? 1 : 0,
                TotalFiles = 1
            });
        }

        if (exception is not null)
        {
            throw exception;
        }
    }

    private long GetBytesWritten(long bytesWrittenFallback = 0)
    {
        return this.innerStream.CanSeek ? this.innerStream.Position : this.bytesWritten + bytesWrittenFallback;
    }

    private void ReportProgress()
    {
        this.progress?.Report(new FileProgress
        {
            BytesProcessed = this.bytesWritten,
            FilesProcessed = 0,
            TotalFiles = 1
        });
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
    }
}
