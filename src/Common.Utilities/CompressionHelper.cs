// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.IO.Compression;
using System.Linq;

/// <summary>
///     Provides methods for compressing and decompressing data asynchronously.
/// </summary>
public static class CompressionHelper
{
    /// <summary>
    ///     Represents the lead bytes in a GZip file header used to identify GZip compressed data.
    /// </summary>
    private const ushort GzipLeadBytes = 0x8b1f;

    /// <summary>
    ///     Compresses the given string asynchronously.
    /// </summary>
    /// <param name="source">The string to compress.</param>
    /// <returns>
    ///     A task representing the asynchronous operation. The task result contains the compressed string in Base64
    ///     format, or null if the source is null.
    /// </returns>
    public static async Task<string> CompressAsync(string source)
    {
        if (source is null)
        {
            return null;
        }

        var result = await CompressAsync(Encoding.UTF8.GetBytes(source)).AnyContext();

        return Convert.ToBase64String(result.ToArray());
    }

    /// <summary>
    ///     Decompresses a Base64 encoded and compressed string asynchronously.
    /// </summary>
    /// <param name="source">The Base64 encoded and compressed string to decompress.</param>
    /// <returns>
    ///     A task that represents the asynchronous decompression operation. The task result contains the decompressed
    ///     string, or null if the input is null.
    /// </returns>
    public static async Task<string> DecompressAsync(string source)
    {
        if (source is null)
        {
            return null;
        }

        var result = await DecompressAsync(Convert.FromBase64String(source)).AnyContext();

        return Encoding.UTF8.GetString(result);
    }

    /// <summary>
    ///     Compresses a given string using GZip compression and returns the compressed data as a Base64 encoded string.
    /// </summary>
    /// <param name="source">The string to be compressed.</param>
    /// <returns>
    ///     A task representing the asynchronous operation. The result of the task contains the compressed string in
    ///     Base64 format, or null if the input was null.
    /// </returns>
    public static async Task<byte[]> CompressAsync(byte[] source)
    {
        if (source is null)
        {
            return null;
        }

        using var sourceStream = new MemoryStream(source);
        using var destinationStream = new MemoryStream();
        await CompressAsync(sourceStream, destinationStream).AnyContext();

        return destinationStream.ToArray();
    }

    /// <summary>
    ///     Decompresses a Base64 encoded compressed string asynchronously.
    /// </summary>
    /// <param name="source">The Base64 encoded compressed string to decompress.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the decompressed string.</returns>
    public static async Task<byte[]> DecompressAsync(byte[] source)
    {
        if (source is null)
        {
            return null;
        }

        using var sourceStream = new MemoryStream(source);
        using var destinationStream = new MemoryStream();
        await DecompressAsync(sourceStream, destinationStream).AnyContext();

        return destinationStream.ToArray();
    }

    /// <summary>
    ///     Compresses the input stream asynchronously using gzip compression and writes the compressed data to the destination
    ///     stream.
    /// </summary>
    /// <param name="source">The input stream to be compressed.</param>
    /// <param name="destination">The stream where the compressed data will be written to.</param>
    /// <returns>A task that represents the asynchronous compression operation.</returns>
    public static async Task CompressAsync(Stream source, Stream destination)
    {
        EnsureArg.IsNotNull(destination, nameof(destination));

        if (source is null)
        {
            return;
        }

        await using var compressor = CreateGZipCompressionStream(destination);
        await source.CopyToAsync(compressor).AnyContext();
        await compressor.FlushAsync();
    }

    /// <summary>
    ///     Decompresses the given Base64 encoded string asynchronously.
    /// </summary>
    /// <param name="source">The Base64 encoded string to decompress.</param>
    /// <param name="destination">The stream where the compressed data will be written to.</param>
    /// <returns>
    ///     A task representing the asynchronous operation. The task result contains the decompressed string, or null if
    ///     the source is null.
    /// </returns>
    public static async Task DecompressAsync(Stream source, Stream destination)
    {
        EnsureArg.IsNotNull(destination, nameof(destination));

        if (source is null)
        {
            return;
        }

        await using var decompressor = CreateGZipDecompressionStream(source);
        await decompressor.CopyToAsync(destination).AnyContext();
        await destination.FlushAsync().AnyContext();
    }

    /// <summary>
    /// Creates a gzip compression stream that writes compressed data into the destination stream.
    /// </summary>
    /// <param name="destination">The destination stream that receives compressed data.</param>
    /// <param name="compressionLevel">The compression level to use.</param>
    /// <param name="leaveOpen">True to leave the destination stream open when the gzip stream is disposed.</param>
    /// <returns>A writable gzip compression stream.</returns>
    public static Stream CreateGZipCompressionStream(
        Stream destination,
        CompressionLevel compressionLevel = CompressionLevel.Optimal,
        bool leaveOpen = true)
    {
        EnsureArg.IsNotNull(destination, nameof(destination));

        return new GZipStream(destination, compressionLevel, leaveOpen);
    }

    /// <summary>
    /// Creates a gzip decompression stream that reads decompressed data from the source stream.
    /// </summary>
    /// <param name="source">The source stream containing gzip-compressed data.</param>
    /// <param name="leaveOpen">True to leave the source stream open when the gzip stream is disposed.</param>
    /// <returns>A readable gzip decompression stream.</returns>
    public static Stream CreateGZipDecompressionStream(Stream source, bool leaveOpen = true)
    {
        EnsureArg.IsNotNull(source, nameof(source));

        return new GZipStream(source, CompressionMode.Decompress, leaveOpen);
    }

    /// <summary>
    /// Creates a writable stream for a single ZIP archive entry.
    /// </summary>
    /// <param name="destination">The destination stream that receives the ZIP archive bytes.</param>
    /// <param name="entryName">The ZIP entry name to create.</param>
    /// <param name="compressionLevel">The compression level to apply to the entry.</param>
    /// <param name="leaveOpen">True to leave the destination stream open when the returned stream is disposed.</param>
    /// <returns>A writable stream for the ZIP entry content.</returns>
    public static Stream CreateZipEntryWriteStream(
        Stream destination,
        string entryName,
        CompressionLevel compressionLevel = CompressionLevel.Optimal,
        bool leaveOpen = true)
    {
        EnsureArg.IsNotNull(destination, nameof(destination));
        EnsureArg.IsNotNullOrWhiteSpace(entryName, nameof(entryName));

        var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen);
        var entry = archive.CreateEntry(entryName, compressionLevel);
        var entryStream = entry.Open();

        return new OwnedStream(entryStream, archive);
    }

    /// <summary>
    /// Opens a readable stream for a single ZIP archive entry.
    /// </summary>
    /// <param name="source">The source stream containing a ZIP archive.</param>
    /// <param name="entryName">The optional ZIP entry name to open. When null, the archive must contain exactly one file entry.</param>
    /// <param name="leaveOpen">True to leave the source stream open when the returned stream is disposed.</param>
    /// <returns>A readable stream for the ZIP entry content.</returns>
    public static Stream OpenZipEntryReadStream(Stream source, string entryName = null, bool leaveOpen = true)
    {
        EnsureArg.IsNotNull(source, nameof(source));

        var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen);
        var fileEntries = archive.Entries.Where(e => !string.IsNullOrWhiteSpace(e.Name)).ToList();

        if (fileEntries.Count == 0)
        {
            archive.Dispose();
            throw new InvalidDataException("The ZIP archive does not contain any file entries.");
        }

        ZipArchiveEntry entry;

        if (string.IsNullOrWhiteSpace(entryName))
        {
            if (fileEntries.Count != 1)
            {
                archive.Dispose();
                throw new InvalidDataException("The ZIP archive contains multiple file entries. Specify the entry name explicitly.");
            }

            entry = fileEntries[0];
        }
        else
        {
            entry = fileEntries.FirstOrDefault(e => string.Equals(e.FullName, entryName, StringComparison.OrdinalIgnoreCase))
                ?? fileEntries.FirstOrDefault(e => string.Equals(e.Name, entryName, StringComparison.OrdinalIgnoreCase));

            if (entry is null)
            {
                archive.Dispose();
                throw new InvalidDataException($"The ZIP archive does not contain an entry named '{entryName}'.");
            }
        }

        var entryStream = entry.Open();
        return new OwnedStream(entryStream, archive);
    }

    /// <summary>
    ///     Determines if a given byte array is compressed using gzip format.
    /// </summary>
    /// <param name="source">The byte array to check for compression.</param>
    /// <returns>True if the byte array is compressed, otherwise false.</returns>
    public static bool IsCompressed(byte[] source)
    {
        if (source is null || source.Length < 2)
        {
            return false;
        }

        return BitConverter.ToUInt16(source, 0) == GzipLeadBytes;
    }

    private sealed class OwnedStream(Stream innerStream, IDisposable owner) : Stream
    {
        private readonly Stream innerStream = innerStream ?? throw new ArgumentNullException(nameof(innerStream));
        private readonly IDisposable owner = owner ?? throw new ArgumentNullException(nameof(owner));
        private bool disposed;

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
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            this.ThrowIfDisposed();
            this.innerStream.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            this.ThrowIfDisposed();
            this.innerStream.Write(buffer);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            this.ThrowIfDisposed();
            return this.innerStream.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            this.ThrowIfDisposed();
            return this.innerStream.WriteAsync(buffer, cancellationToken);
        }

        public override void WriteByte(byte value)
        {
            this.ThrowIfDisposed();
            this.innerStream.WriteByte(value);
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposing || this.disposed)
            {
                base.Dispose(disposing);
                return;
            }

            this.innerStream.Dispose();
            this.owner.Dispose();
            this.disposed = true;
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            if (this.disposed)
            {
                return;
            }

            await this.innerStream.DisposeAsync();
            this.owner.Dispose();
            this.disposed = true;
            await base.DisposeAsync();
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);
        }
    }
}
