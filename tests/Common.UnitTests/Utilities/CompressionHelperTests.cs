// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using System.IO.Compression;

[UnitTest("Common")]
public class CompressionHelperTests
{
    [Fact]
    public async Task Compress_ShouldCompressString()
    {
        // Arrange
        const string source = "hello world!";

        // Act
        var result = await CompressionHelper.CompressAsync(source);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldStartWith("H4sI");
    }

    [Fact]
    public async Task Decompress_ShouldDecompressString()
    {
        // Arrange
        const string source = "H4sIAAAAAAAACspIzcnJVyjPL8pJUQQAAAD//wMAbcK0AwwAAAA=";
        const string expected = "hello world!";

        // Act
        var result = await CompressionHelper.DecompressAsync(source);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe(expected);
    }

    [Fact]
    public async Task Compress_ShouldRoundtripString()
    {
        // Arrange
        const string source = "hello world!";

        // Act
        var compressed = await CompressionHelper.CompressAsync(source);
        var result = await CompressionHelper.DecompressAsync(compressed);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldStartWith(source);
    }

    [Fact]
    public async Task CompressAsync_ShouldCompressByteArray()
    {
        // Arrange
        var source = Encoding.UTF8.GetBytes("hello world!");
        var expected = new byte[]
                { 31, 139, 8, 0, 0, 0, 0, 0, 0, 10, 202, 72, 205, 201, 201, 87, 40, 207, 47, 202, 73, 81, 4, 0, 0, 0, 255, 255, 3, 0, 109, 194, 180, 3, 12, 0, 0, 0 }
            .Skip(10)
            .ToArray();

        // Act
        var result = await CompressionHelper.CompressAsync(source);

        // Assert
        result.ShouldNotBeNull();
        result.Skip(10)
            .ToArray()
            .ShouldBe(expected); // skip first 10 bytes as they seem to differ across machines
    }

    [Fact]
    public async Task DecompressAsync_ShouldDecompressByteArray()
    {
        // Arrange
        var source = new byte[]
            { 31, 139, 8, 0, 0, 0, 0, 0, 0, 10, 202, 72, 205, 201, 201, 87, 40, 207, 47, 202, 73, 81, 4, 0, 0, 0, 255, 255, 3, 0, 109, 194, 180, 3, 12, 0, 0, 0 };
        var expected = Encoding.UTF8.GetBytes("hello world!")
            .Skip(10)
            .ToArray();

        // Act
        var result = await CompressionHelper.DecompressAsync(source);

        // Assert
        result.ShouldNotBeNull();
        result.Skip(10)
            .ToArray()
            .ShouldBe(expected); // skip first 10 bytes as they seem to differ across machines
    }

    [Fact]
    public async Task CompressAsync_ShouldRoundtripByteArray()
    {
        // Arrange
        var source = Encoding.UTF8.GetBytes("hello world!");

        // Act
        var compressed = await CompressionHelper.CompressAsync(source);
        var result = await CompressionHelper.DecompressAsync(compressed);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe(source);
    }

    [Fact]
    public void IsCompressed_ShouldReturnTrueIfByteArrayIsCompressed()
    {
        // Arrange
        var source = new byte[]
            { 31, 139, 8, 0, 0, 0, 0, 0, 0, 10, 202, 72, 205, 201, 201, 87, 40, 207, 47, 202, 73, 81, 4, 0, 0, 0, 255, 255, 3, 0, 109, 194, 180, 3, 12, 0, 0, 0 };

        // Act
        var result = CompressionHelper.IsCompressed(source);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsCompressed_ShouldReturnFalseIfByteArrayIsNotCompressed()
    {
        // Arrange
        var source = Encoding.UTF8.GetBytes("hello world!");

        // Act
        var result = CompressionHelper.IsCompressed(source);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task CreateGZipCompressionStream_ShouldRoundtripStream()
    {
        // Arrange
        var sourceBytes = Encoding.UTF8.GetBytes("hello gzip stream");
        await using var compressed = new MemoryStream();

        // Act
        await using (var compressionStream = CompressionHelper.CreateGZipCompressionStream(compressed))
        {
            await compressionStream.WriteAsync(sourceBytes);
        }

        compressed.Position = 0;
        await using var decompressed = new MemoryStream();
        await using (var decompressionStream = CompressionHelper.CreateGZipDecompressionStream(compressed))
        {
            await decompressionStream.CopyToAsync(decompressed);
        }

        // Assert
        decompressed.ToArray().ShouldBe(sourceBytes);
    }

    [Fact]
    public async Task CreateZipEntryWriteStream_AndOpenZipEntryReadStream_ShouldRoundtripSingleEntry()
    {
        // Arrange
        var sourceBytes = Encoding.UTF8.GetBytes("hello zip entry");
        await using var archiveStream = new MemoryStream();

        // Act
        await using (var entryWriteStream = CompressionHelper.CreateZipEntryWriteStream(archiveStream, "payload.csv"))
        {
            await entryWriteStream.WriteAsync(sourceBytes);
        }

        archiveStream.Position = 0;
        await using var entryReadStream = CompressionHelper.OpenZipEntryReadStream(archiveStream);
        await using var content = new MemoryStream();
        await entryReadStream.CopyToAsync(content);

        // Assert
        content.ToArray().ShouldBe(sourceBytes);
    }

    [Fact]
    public async Task OpenZipEntryReadStream_WithExplicitEntryName_ShouldReadSelectedEntry()
    {
        // Arrange
        await using var archiveStream = await CreateZipArchiveStream(
            ("first.txt", "first"),
            ("second.txt", "second"));

        // Act
        await using var entryReadStream = CompressionHelper.OpenZipEntryReadStream(archiveStream, "second.txt");
        using var reader = new StreamReader(entryReadStream);
        var content = await reader.ReadToEndAsync();

        // Assert
        content.ShouldBe("second");
    }

    [Fact]
    public async Task OpenZipEntryReadStream_WithMultipleEntriesAndNoEntryName_ShouldThrow()
    {
        // Arrange
        await using var archiveStream = await CreateZipArchiveStream(
            ("first.txt", "first"),
            ("second.txt", "second"));

        // Act
        var exception = await Should.ThrowAsync<InvalidDataException>(async () =>
        {
            await using var _ = CompressionHelper.OpenZipEntryReadStream(archiveStream);
            await Task.CompletedTask;
        });

        // Assert
        exception.Message.ShouldContain("multiple file entries");
    }

    private static async Task<MemoryStream> CreateZipArchiveStream(params (string Name, string Content)[] entries)
    {
        var stream = new MemoryStream();

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
                await using var entryStream = entry.Open();
                await using var writer = new StreamWriter(entryStream, Encoding.UTF8, leaveOpen: true);
                await writer.WriteAsync(content);
                await writer.FlushAsync();
            }
        }

        stream.Position = 0;
        return stream;
    }
}
