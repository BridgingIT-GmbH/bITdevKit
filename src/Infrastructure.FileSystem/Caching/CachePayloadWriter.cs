// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

using System.Buffers.Binary;
using System.Security.Cryptography;
using BridgingIT.DevKit.Application.Caching;

// One forward-only source pass, with a fixed owned chunk. No caller buffer survives WriteAsync.
/// <summary>Stages one immutable payload as checked blocks under incremental space admission before sealing its descriptor.</summary>
/// <example><code>await using var writer = await CachePayloadWriter.CreateAsync(directory, storeId, version, maximumBytes, profile, reserve, transient, resident, token);</code></example>
public sealed class CachePayloadWriter : Stream
{
    private readonly string directory;
    private readonly string storeId;
    private readonly string version;
    private readonly long maximum;
    private readonly bool durable;
    private readonly Func<long, CancellationToken, ValueTask<Result>> reserve;
    private readonly ResourceBudget transient;
    private readonly ResourceBudget resident;
    private readonly ResourceCharge memory;
    private readonly FileSystemAuthority authority;
    private readonly FileStream file;
    private readonly SemaphoreSlim gate = new(1);
    private readonly IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private readonly byte[] buffer = new byte[CachePayloadReference.BlockBytes];
    private readonly byte[] header = new byte[CachePayloadReference.FrameBytes];
    private int buffered;
    private long written;
    private long blocks;
    private bool failed;
    private bool disposed;
    private CachePayloadReference sealedPayload;

    private CachePayloadWriter(string directory, string storeId, string version, long maximum, bool durable,
        Func<long, CancellationToken, ValueTask<Result>> reserve, ResourceBudget transient, ResourceBudget resident,
        ResourceCharge memory, FileSystemAuthority authority, FileStream file)
    {
        this.directory = directory;
        this.storeId = storeId;
        this.version = version;
        this.maximum = maximum;
        this.durable = durable;
        this.reserve = reserve;
        this.transient = transient;
        this.resident = resident;
        this.memory = memory;
        this.authority = authority;
        this.file = file;
    }

    /// <summary>Creates a new generated-version staging file after checking the dedicated directory and admitting writer workspace.</summary>
    public static async ValueTask<CachePayloadWriter> CreateAsync(string directory, string storeId, string version, long maximum,
        FileSystemCacheOptions profile, Func<long, CancellationToken, ValueTask<Result>> reserve, ResourceBudget transient,
        ResourceBudget resident, CancellationToken cancellationToken)
    {
        if (!Guid.TryParseExact(storeId, "N", out _) || !Guid.TryParseExact(version, "N", out _) || maximum < 0 || maximum > long.MaxValue / 2)
        {
            throw new CacheTransferException(CacheErrorCode.Validation);
        }

        ArgumentNullException.ThrowIfNull(reserve);
        if (!ResourceCharge.TryReserve(CachePayloadReference.BlockBytes + 16384L, out var memory, transient, resident))
        {
            throw new CacheTransferException(CacheErrorCode.Capacity);
        }

        FileSystemAuthority authority = null;
        FileStream file = null;
        try
        {
            var accepted = await reserve(4096, cancellationToken).ConfigureAwait(false);
            if (accepted.IsFailure) { throw new CacheTransferException(CacheErrorCode.Capacity); }

            FileSystemPaths.RejectLinks(directory);
            FileSystemDurability.EnsureDirectory(directory);
            // A generated generation is never adopted or overwritten, even after an incomplete attempt.
            if (File.Exists(CachePayloadReference.PathFor(directory, version)) || File.Exists(CachePayloadReference.PathFor(directory, version, true)) ||
                File.Exists(Path.Combine(directory, version + ".payload.json")) || File.Exists(Path.Combine(directory, "stage-" + version)))
            {
                throw new CacheTransferException(CacheErrorCode.Conflict);
            }

            authority = await FileSystemAuthority.AcquireAsync(profile.AuthorityOptions, directory, "stage-" + version, true, cancellationToken).ConfigureAwait(false);
            if (authority is null) { throw new CacheTransferException(CacheErrorCode.Conflict); }

            file = FileSystemFileOperations.Open(CachePayloadReference.PathFor(directory, version, true), FileMode.CreateNew, FileAccess.Write,
                FileShare.Read | FileShare.Delete, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            return new(directory, storeId, version, maximum, profile.Durable, reserve, transient, resident, memory, authority, file);
        }
        catch
        {
            if (file is not null) { await file.DisposeAsync().ConfigureAwait(false); }

            authority?.Dispose();
            memory.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public override bool CanRead => false;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => !this.disposed && !this.failed && this.sealedPayload is null;

    /// <inheritdoc />
    public override long Length => this.written;

    /// <inheritdoc />
    public override long Position { get => this.written; set => throw new NotSupportedException(); }

    /// <inheritdoc />
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> input, CancellationToken cancellationToken = default)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            this.CheckWritable();
            if (input.Length > this.maximum - this.written) { throw new CacheTransferException(CacheErrorCode.Capacity); }

            while (!input.IsEmpty)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var length = Math.Min(input.Length, this.buffer.Length - this.buffered);
                input.Span[..length].CopyTo(this.buffer.AsSpan(this.buffered));
                this.hash.AppendData(input.Span[..length]);
                this.buffered += length;
                this.written += length;
                input = input[length..];
                if (this.buffered == this.buffer.Length) { await this.FlushBlockAsync(cancellationToken).ConfigureAwait(false); }
            }
        }
        catch { this.failed = true; throw; }
        finally { this.gate.Release(); }
    }

    private async ValueTask FlushBlockAsync(CancellationToken cancellationToken)
    {
        if (this.buffered == 0) { return; }

        var accepted = await this.reserve(checked(this.written + (this.blocks + 1) * CachePayloadReference.FrameBytes + 4096), cancellationToken).ConfigureAwait(false);
        if (accepted.IsFailure) { throw new CacheTransferException(CacheErrorCode.Capacity); }

        "BDKP"u8.CopyTo(this.header);
        BinaryPrimitives.WriteInt32LittleEndian(this.header.AsSpan(4), this.buffered);
        BinaryPrimitives.WriteInt64LittleEndian(this.header.AsSpan(8), this.blocks);
        SHA256.HashData(this.buffer.AsSpan(0, this.buffered), this.header.AsSpan(16));
        await this.file.WriteAsync(this.header, cancellationToken).ConfigureAwait(false);
        await this.file.WriteAsync(this.buffer.AsMemory(0, this.buffered), cancellationToken).ConfigureAwait(false);
        this.blocks++;
        this.buffered = 0;
    }

    /// <summary>Flushes the last checked block, moves the staged payload to its immutable name and publishes its checked descriptor under native authority.</summary>
    public async ValueTask<CachePayloadReference> SealAsync(CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);
            if (this.sealedPayload is not null) { return this.sealedPayload; }

            this.CheckWritable();
            await this.FlushBlockAsync(cancellationToken).ConfigureAwait(false);
            await this.file.FlushAsync(cancellationToken).ConfigureAwait(false);
            if (this.durable) { FileSystemDurability.Flush(this.file); }

            await this.file.DisposeAsync().ConfigureAwait(false);
            var payload = new CachePayloadReference { StoreId = this.storeId, Version = this.version, Length = this.written,
                Hash = Convert.ToHexStringLower(this.hash.GetHashAndReset()) };
            cancellationToken.ThrowIfCancellationRequested();
            FileSystemDurability.Move(CachePayloadReference.PathFor(this.directory, this.version, true),
                CachePayloadReference.PathFor(this.directory, this.version), false, this.durable);
            await CacheCheckedMetadata.WriteAsync(this.directory, this.version + ".payload.json", payload, this.authority,
                this.durable, this.transient, this.resident, cancellationToken).ConfigureAwait(false);
            this.sealedPayload = payload;
            return payload;
        }
        catch { this.failed = true; this.authority.RetainIfShared(); throw; }
        finally { this.gate.Release(); }
    }

    /// <inheritdoc />
    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);
            if (this.sealedPayload is null) { await this.file.FlushAsync(cancellationToken).ConfigureAwait(false); }
        }
        finally { this.gate.Release(); }
    }

    // Physical files and credits belong to the native operation until publication or proven cleanup.
    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        await this.gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (this.disposed) { return; }

            this.disposed = true;
            try { await this.file.DisposeAsync().ConfigureAwait(false); }
            finally { this.hash.Dispose(); this.authority.Dispose(); this.memory.Dispose(); }
        }
        finally { this.gate.Release(); }

        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing) { if (disposing) { this.DisposeAsync().AsTask().GetAwaiter().GetResult(); } base.Dispose(disposing); }

    private void CheckWritable()
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        if (this.failed || this.sealedPayload is not null) { throw new CacheTransferException(CacheErrorCode.Contract); }
    }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => this.WriteAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => this.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override void Flush() => this.FlushAsync().GetAwaiter().GetResult();

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();
}