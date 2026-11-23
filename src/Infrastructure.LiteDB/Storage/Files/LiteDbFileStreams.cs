// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using BridgingIT.DevKit.Application.Storage;

/// <summary>Reads a protected immutable file generation with one bounded chunk buffer and seek support.</summary>
/// <param name="store">The generation owner.</param>
/// <param name="generation">The immutable generation identity.</param>
/// <param name="length">The published file length.</param>
/// <param name="progress">Optional read progress.</param>
public sealed class LiteDbFileReadStream(LiteDbFileContentStore store, Guid generation, long length, IProgress<FileProgress> progress = null) : Stream
{
    private byte[] buffer;
    private long chunkIndex = -1;
    private long position;
    private bool disposed;

    /// <inheritdoc />
    public override bool CanRead => !this.disposed;
    /// <inheritdoc />
    public override bool CanSeek => !this.disposed;
    /// <inheritdoc />
    public override bool CanWrite => false;
    /// <inheritdoc />
    public override long Length => length;
    /// <inheritdoc />
    public override long Position { get => this.position; set => this.Seek(value, SeekOrigin.Begin); }

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (destination.Length == 0 || this.position == length) return 0;
        var index = this.position / LiteDbFileContentStore.ChunkSize;
        if (this.chunkIndex != index)
        {
            this.buffer = await store.ReadChunkAsync(generation, index, cancellationToken).ConfigureAwait(false);
            this.chunkIndex = index;
        }

        var offset = (int)(this.position % LiteDbFileContentStore.ChunkSize);
        var count = (int)Math.Min(Math.Min(destination.Length, this.buffer.Length - offset), length - this.position);
        if (count <= 0) throw new IOException("The immutable file chunk does not match its published length.");
        this.buffer.AsMemory(offset, count).CopyTo(destination);
        this.position += count;
        progress?.Report(new() { BytesProcessed = this.position, FilesProcessed = this.position == length ? 1 : 0 });
        return count;
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => this.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => this.ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        var target = origin switch { SeekOrigin.Begin => offset, SeekOrigin.Current => checked(this.position + offset), SeekOrigin.End => checked(length + offset), _ => throw new ArgumentOutOfRangeException(nameof(origin)) };
        if (target < 0 || target > length) throw new IOException("The seek position is outside the published file.");
        return this.position = target;
    }

    /// <inheritdoc />
    public override void Flush() { ObjectDisposedException.ThrowIf(this.disposed, this); }

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();
    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (!this.disposed) { this.disposed = true; store.RemoveReader(generation); this.buffer = null; }

        base.Dispose(disposing);
    }
}

/// <summary>Stages bounded chunks and publishes the complete file only after successful disposal.</summary>
/// <param name="store">The upload owner.</param>
/// <param name="generation">The unpublished generation identity.</param>
/// <param name="progress">Optional upload progress.</param>
/// <param name="cancellationToken">The opening operation's cancellation token.</param>
public sealed class LiteDbFileWriteStream(LiteDbFileContentStore store, Guid generation, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) : Stream
{
    private readonly byte[] buffer = new byte[LiteDbFileContentStore.ChunkSize];
    private int buffered;
    private long chunkIndex;
    private long length;
    private bool disposed;
    private bool faulted;

    /// <inheritdoc />
    public override bool CanRead => false;
    /// <inheritdoc />
    public override bool CanSeek => false;
    /// <inheritdoc />
    public override bool CanWrite => !this.disposed;
    /// <inheritdoc />
    public override long Length => this.length;
    /// <inheritdoc />
    public override long Position { get => this.length; set => throw new NotSupportedException(); }

    /// <inheritdoc />
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, this.OpeningToken);
            linked.Token.ThrowIfCancellationRequested();
            while (source.Length > 0)
            {
                var count = Math.Min(source.Length, this.buffer.Length - this.buffered);
                source[..count].CopyTo(this.buffer.AsMemory(this.buffered));
                this.buffered += count;
                this.length = checked(this.length + count);
                source = source[count..];
                if (this.buffered == this.buffer.Length)
                {
                    await store.WriteChunkAsync(generation, this.chunkIndex, this.buffer.ToArray(), linked.Token).ConfigureAwait(false);
                    this.chunkIndex++;
                    this.buffered = 0;
                }
            }

            progress?.Report(new() { BytesProcessed = this.length });
        }
        catch { this.faulted = true; throw; }
    }

    private CancellationToken OpeningToken { get; } = cancellationToken;
    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => this.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => this.WriteAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
    /// <inheritdoc />
    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, this.OpeningToken);
            linked.Token.ThrowIfCancellationRequested();
            if (this.buffered > 0) await store.WriteChunkAsync(generation, this.chunkIndex, this.buffer.AsSpan(0, this.buffered).ToArray(), linked.Token).ConfigureAwait(false);
        }
        catch { this.faulted = true; throw; }
    }

    /// <inheritdoc />
    public override void Flush() => this.FlushAsync(default).GetAwaiter().GetResult();
    /// <summary>Abandons the generation after a source stream or write failure.</summary>
    /// <returns>The durable abandonment operation.</returns>
    public async Task AbortAsync()
    {
        if (this.disposed) return;
        this.faulted = true;
        this.disposed = true;
        await store.AbortAsync(generation).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        if (this.disposed) return;
        try
        {
            if (this.faulted) { await store.AbortAsync(generation).ConfigureAwait(false); return; }

            await this.FlushAsync(this.OpeningToken).ConfigureAwait(false);
            await store.PublishAsync(generation, this.OpeningToken).ConfigureAwait(false);
            progress?.Report(new() { BytesProcessed = this.length, FilesProcessed = 1 });
        }
        catch { await store.AbortAsync(generation).ConfigureAwait(false); throw; }
        finally { this.disposed = true; GC.SuppressFinalize(this); }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing) this.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.Dispose(disposing);
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();
}
