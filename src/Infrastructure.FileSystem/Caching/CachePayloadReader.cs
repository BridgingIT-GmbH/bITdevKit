// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

using System.Buffers.Binary;
using System.Security.Cryptography;
using BridgingIT.DevKit.Application.Caching;

// Native positions belong to this reader. A caller receives bytes only after the complete chunk validates.
/// <summary>Reads an immutable payload through independently verified 64 KiB frames while retaining its native reader pin.</summary>
/// <example><code>await using var payload = await CachePayloadReader.OpenAsync(directory, storeId, reference, pin, transient, resident, token);</code></example>
public sealed class CachePayloadReader : Stream
{
    private readonly CachePayloadReference payload;
    private readonly FileStream file;
    private readonly IAsyncDisposable pin;
    private readonly ResourceCharge memory;
    private readonly SemaphoreSlim gate = new(1);
    private readonly byte[] buffer = new byte[CachePayloadReference.BlockBytes];
    private readonly byte[] header = new byte[CachePayloadReference.FrameBytes];
    private readonly IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private long position;
    private long loadedBlock = -1;
    private bool sequential = true;
    private bool fullHashChecked;
    private bool failed;
    private bool disposed;

    private CachePayloadReader(CachePayloadReference payload, FileStream file, IAsyncDisposable pin, ResourceCharge memory)
    {
        this.payload = payload;
        this.file = file;
        this.pin = pin;
        this.memory = memory;
    }

    /// <summary>Opens the exact payload under memory admission and transfers the supplied reader pin to the stream; failures release the pin.</summary>
    public static async ValueTask<CachePayloadReader> OpenAsync(string directory, string storeId, CachePayloadReference payload,
        IAsyncDisposable pin, ResourceBudget transient, ResourceBudget resident, CancellationToken cancellationToken)
    {
        FileStream file = null;
        ResourceCharge memory = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(pin);
            payload.Validate(storeId);
            if (!ResourceCharge.TryReserve(CachePayloadReference.BlockBytes + 16384L, out memory, transient, resident))
            {
                throw new CacheTransferException(CacheErrorCode.Capacity);
            }

            try
            {
                file = FileSystemFileOperations.Open(CachePayloadReference.PathFor(directory, payload.Version), FileMode.Open, FileAccess.Read,
                    FileShare.Read | FileShare.Delete, 4096, FileOptions.Asynchronous | FileOptions.RandomAccess);
            }
            catch (FileNotFoundException) { throw new CacheTransferException(CacheErrorCode.Corruption); }

            if (file.Length != payload.PhysicalBytes) { throw new CacheTransferException(CacheErrorCode.Corruption); }

            return new(payload, file, pin, memory);
        }
        catch
        {
            try { if (file is not null) { await file.DisposeAsync().ConfigureAwait(false); } }
            finally { memory?.Dispose(); if (pin is not null) { await pin.DisposeAsync().ConfigureAwait(false); } }

            throw;
        }
    }

    /// <inheritdoc />
    public override bool CanRead => !this.disposed && !this.failed;

    /// <inheritdoc />
    public override bool CanSeek => !this.disposed && !this.failed;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => this.payload.Length;

    /// <inheritdoc />
    public override long Position { get => Interlocked.Read(ref this.position); set => this.Seek(value, SeekOrigin.Begin); }

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> output, CancellationToken cancellationToken = default)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            this.CheckReadable();
            cancellationToken.ThrowIfCancellationRequested();
            if (output.IsEmpty) { return 0; }

            if (this.position == this.Length) { this.CheckFullHash(); return 0; }

            var ordinal = this.position / CachePayloadReference.BlockBytes;
            if (ordinal != this.loadedBlock) { await this.LoadBlockAsync(ordinal, cancellationToken).ConfigureAwait(false); }

            var offset = (int)(this.position % CachePayloadReference.BlockBytes);
            var length = (int)Math.Min(Math.Min(output.Length, CachePayloadReference.BlockBytes - offset), this.Length - this.position);
            if (this.sequential) { this.hash.AppendData(this.buffer.AsSpan(offset, length)); }

            // Fail before delivering the last bytes if the acquired descriptor and complete body disagree.
            if (this.position + length == this.Length) { this.CheckFullHash(); }

            this.buffer.AsMemory(offset, length).CopyTo(output);
            this.position += length;
            return length;
        }
        catch { this.failed = true; throw; }
        finally { this.gate.Release(); }
    }

    private async ValueTask LoadBlockAsync(long ordinal, CancellationToken cancellationToken)
    {
        this.loadedBlock = -1;
        this.file.Position = checked(ordinal * (CachePayloadReference.BlockBytes + CachePayloadReference.FrameBytes));
        var expected = (int)Math.Min(CachePayloadReference.BlockBytes, this.Length - ordinal * CachePayloadReference.BlockBytes);
        try
        {
            await this.file.ReadExactlyAsync(this.header, cancellationToken).ConfigureAwait(false);
            if (!this.header.AsSpan(0, 4).SequenceEqual("BDKP"u8) || BinaryPrimitives.ReadInt32LittleEndian(this.header.AsSpan(4)) != expected ||
                BinaryPrimitives.ReadInt64LittleEndian(this.header.AsSpan(8)) != ordinal)
            {
                throw new CacheTransferException(CacheErrorCode.Corruption);
            }

            await this.file.ReadExactlyAsync(this.buffer.AsMemory(0, expected), cancellationToken).ConfigureAwait(false);
        }
        catch (EndOfStreamException) { throw new CacheTransferException(CacheErrorCode.Corruption); }

        Span<byte> digest = stackalloc byte[32];
        SHA256.HashData(this.buffer.AsSpan(0, expected), digest);
        if (!CryptographicOperations.FixedTimeEquals(digest, this.header.AsSpan(16))) { throw new CacheTransferException(CacheErrorCode.Corruption); }

        this.loadedBlock = ordinal;
    }

    private void CheckFullHash()
    {
        if (!this.sequential || this.fullHashChecked) { return; }

        this.fullHashChecked = true;
        if (!CryptographicOperations.FixedTimeEquals(this.hash.GetHashAndReset(), Convert.FromHexString(this.payload.Hash)))
        {
            throw new CacheTransferException(CacheErrorCode.Corruption);
        }
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin)
    {
        this.gate.Wait();
        try
        {
            this.CheckReadable();
            long next;
            try { next = checked(offset + (origin switch { SeekOrigin.Begin => 0, SeekOrigin.Current => this.position, SeekOrigin.End => this.Length, _ => throw new ArgumentOutOfRangeException(nameof(origin)) })); }
            catch (OverflowException) { throw new IOException("The requested payload position is outside the acquired version."); }

            if (next < 0 || next > this.Length) { throw new IOException("The requested payload position is outside the acquired version."); }

            if (next != this.position) { this.sequential = false; }

            this.position = next;
            return next;
        }
        finally { this.gate.Release(); }
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        // Waiting for the actual native read is essential: cancellation/deadlines cannot release its pin or buffer.
        await this.gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (this.disposed) { return; }

            this.disposed = true;
            try { await this.file.DisposeAsync().ConfigureAwait(false); }
            finally
            {
                try { await this.pin.DisposeAsync().ConfigureAwait(false); }
                finally { this.hash.Dispose(); this.memory.Dispose(); }
            }
        }
        finally { this.gate.Release(); }

        GC.SuppressFinalize(this);
    }

    private void CheckReadable()
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        if (this.failed) { throw new CacheTransferException(CacheErrorCode.Corruption); }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing) { if (disposing) { this.DisposeAsync().AsTask().GetAwaiter().GetResult(); } base.Dispose(disposing); }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => this.ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => this.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override void Flush() { }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();
}