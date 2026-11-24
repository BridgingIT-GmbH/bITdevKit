// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.LiteDB.Storage;

using System.Security.Cryptography;
using System.Text;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Infrastructure.LiteDb;
using Microsoft.Extensions.DependencyInjection;

[IntegrationTest("Infrastructure")]
public class LiteDbFileStorageTests
{
    [Fact]
    public async Task ChunkedFiles_RoundTripSeekAndChecksum_WithoutCrossLocationLeaks()
    {
        await using var host = new LiteDbStorageTestHost();
        var sut = host.Files();
        var bytes = RandomNumberGenerator.GetBytes(LiteDbFileContentStore.ChunkSize * 3 + 17);
        (await sut.WriteFileAsync("reports\\daily.bin", new MemoryStream(bytes))).IsSuccess.ShouldBeTrue();
        (await sut.DirectoryExistsAsync("reports")).IsSuccess.ShouldBeTrue();
        var opened = await sut.ReadFileAsync("reports/daily.bin");
        opened.IsSuccess.ShouldBeTrue();
        await using var read = opened.Value;
        using var actual = new MemoryStream();
        await read.CopyToAsync(actual);
        actual.ToArray().ShouldBe(bytes);
        read.Seek(LiteDbFileContentStore.ChunkSize - 2, SeekOrigin.Begin);
        var slice = new byte[5];
        await read.ReadExactlyAsync(slice);
        slice.ShouldBe(bytes.Skip(LiteDbFileContentStore.ChunkSize - 2).Take(5).ToArray());
        (await sut.GetChecksumAsync("reports/daily.bin")).Value.ShouldBe(Convert.ToHexStringLower(SHA256.HashData(bytes)));
        (await sut.GetFileMetadataAsync("reports/daily.bin")).Value.Length.ShouldBe(bytes.Length);
        (await host.Files("second").FileExistsAsync("reports/daily.bin")).IsFailure.ShouldBeTrue();
        sut.SupportsNotifications.ShouldBeFalse();
    }

    [Fact]
    public async Task OpenWrite_StagesUntilClose_AndRejectsAStaleConcurrentPublisher()
    {
        await using var host = new LiteDbStorageTestHost();
        var sut = host.Files();
        await sut.WriteFileAsync("file", new MemoryStream([1]));
        var first = (await sut.OpenWriteFileAsync("file", true)).Value;
        var stale = (await sut.OpenWriteFileAsync("file", true)).Value;
        await first.WriteAsync(new byte[] { 2, 3 });
        await first.FlushAsync();
        (await Read(sut, "file")).ShouldBe([1]);
        await first.WriteAsync(new byte[] { 4 });
        await first.DisposeAsync();
        await stale.WriteAsync(new byte[] { 9 });
        await Should.ThrowAsync<LiteDbProviderException>(async () => await stale.DisposeAsync());
        (await Read(sut, "file")).ShouldBe([2, 3, 4]);
    }

    [Fact]
    public async Task CanceledOrFailedSource_PreservesPreviousFile_AndAbandonedChunksAreCollectible()
    {
        await using var host = new LiteDbStorageTestHost();
        var sut = host.Files();
        await sut.WriteFileAsync("file", new MemoryStream([1]));
        using var canceled = new CancellationTokenSource();
        var opened = (await sut.OpenWriteFileAsync("file", true, cancellationToken: canceled.Token)).Value;
        await opened.WriteAsync(new byte[LiteDbFileContentStore.ChunkSize + 1]);
        canceled.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(async () => await opened.DisposeAsync());
        (await Read(sut, "file")).ShouldBe([1]);
        (await sut.WriteFileAsync("file", new FailedSource())).IsFailure.ShouldBeTrue();
        (await Read(sut, "file")).ShouldBe([1]);
        (await host.Services.GetRequiredService<LiteDbFileContentStore>().SweepContentAsync()).ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task OpenReader_RetainsOldGenerationAcrossOverwriteDeleteAndCollection()
    {
        await using var host = new LiteDbStorageTestHost();
        var sut = host.Files();
        var original = RandomNumberGenerator.GetBytes(LiteDbFileContentStore.ChunkSize + 1);
        await sut.WriteFileAsync("file", new MemoryStream(original));
        var old = (await sut.ReadFileAsync("file")).Value;
        await sut.WriteFileAsync("file", new MemoryStream([9]));
        await sut.DeleteFileAsync("file");
        var store = host.Services.GetRequiredService<LiteDbFileContentStore>();
        await store.SweepContentAsync();
        using var copy = new MemoryStream();
        await old.CopyToAsync(copy);
        copy.ToArray().ShouldBe(original);
        await old.DisposeAsync();
        (await store.SweepContentAsync()).ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Directories_CopyMoveRenameDelete_AreIsolatedAndAtomic()
    {
        await using var host = new LiteDbStorageTestHost();
        var sut = host.Files();
        await sut.CreateDirectoryAsync("empty/nested");
        await sut.WriteFileAsync("tree/child/source", new MemoryStream([1, 2]));
        (await sut.CopyFileAsync("tree/child/source", "copy")).IsSuccess.ShouldBeTrue();
        (await sut.MoveFileAsync("copy", "moved")).IsSuccess.ShouldBeTrue();
        (await sut.FileExistsAsync("copy")).IsFailure.ShouldBeTrue();
        (await sut.RenameDirectoryAsync("tree", "archive")).IsSuccess.ShouldBeTrue();
        (await Read(sut, "archive/child/source")).ShouldBe([1, 2]);
        (await sut.RenameDirectoryAsync("archive", "archive/child/recursive")).IsFailure.ShouldBeTrue();
        (await sut.DeleteDirectoryAsync("archive", false)).IsFailure.ShouldBeTrue();
        (await sut.DeleteDirectoryAsync("archive", true)).IsSuccess.ShouldBeTrue();
        (await sut.FileExistsAsync("archive/child/source")).IsFailure.ShouldBeTrue();
        (await sut.DirectoryExistsAsync("empty/nested")).IsSuccess.ShouldBeTrue();
        (await Read(sut, "moved")).ShouldBe([1, 2]);
    }

    [Fact]
    public async Task NamespaceFence_RejectsPublicationAfterDirectoryMove()
    {
        await using var host = new LiteDbStorageTestHost();
        var sut = host.Files();
        await sut.CreateDirectoryAsync("tree");
        var upload = (await sut.OpenWriteFileAsync("tree/new")).Value;
        await upload.WriteAsync(new byte[] { 1 });
        await sut.RenameDirectoryAsync("tree", "moved");
        await Should.ThrowAsync<LiteDbProviderException>(async () => await upload.DisposeAsync());
        (await sut.DirectoryExistsAsync("tree")).IsFailure.ShouldBeTrue();
        (await sut.FileExistsAsync("tree/new")).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task Listing_UsesStablePages_ExactCasingPatternsAndQueryBoundTokens()
    {
        await using var host = new LiteDbStorageTestHost();
        var sut = host.Files();
        for (var i = 0; i < 103; i++) await sut.WriteFileAsync($"reports/{i:D3}.txt", new MemoryStream([1]));
        await sut.WriteFileAsync("reports/sub/inside.bin", new MemoryStream([2]));
        var first = (await sut.ListFilesAsync("reports", "*.txt")).Value;
        first.Files.Count().ShouldBe(100);
        first.NextContinuationToken.ShouldNotBeNull();
        var last = (await sut.ListFilesAsync("reports", "*.txt", continuationToken: first.NextContinuationToken)).Value;
        last.Files.Count().ShouldBe(3);
        last.NextContinuationToken.ShouldBeNull();
        (await sut.ListFilesAsync("reports", "*.bin", continuationToken: first.NextContinuationToken)).IsFailure.ShouldBeTrue();
        (await sut.ListDirectoriesAsync("reports")).Value.ShouldBe(["reports/sub"]);
        (await sut.ListFilesAsync("Reports")).IsFailure.ShouldBeTrue();
        (await sut.ListFilesAsync("reports", "*.bin", true)).Value.Files.ShouldBe(["reports/sub/inside.bin"]);
    }

    [Fact]
    public async Task MetadataAndTraversal_PreservePhysicalContentAndLogicalRoot()
    {
        await using var host = new LiteDbStorageTestHost();
        var sut = host.Files();
        await sut.WriteFileAsync("file", new MemoryStream([1, 2, 3]));
        var metadata = (await sut.GetFileMetadataAsync("file")).Value;
        metadata.LastModified = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        (await sut.SetFileMetadataAsync("file", metadata)).IsSuccess.ShouldBeTrue();
        (await sut.GetFileMetadataAsync("file")).Value.LastModified.ShouldBe(metadata.LastModified);
        metadata.Length = 100;
        (await sut.SetFileMetadataAsync("file", metadata)).IsFailure.ShouldBeTrue();
        (await Read(sut, "file")).ShouldBe([1, 2, 3]);
        (await sut.WriteFileAsync("../escape", new MemoryStream([1]))).IsFailure.ShouldBeTrue();
        (await sut.WriteFileAsync("file/child", new MemoryStream([1]))).IsFailure.ShouldBeTrue();
        (await sut.CheckHealthAsync()).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ReopenAndCrossProviderCopy_PreserveContentDirectoriesAndMetadata()
    {
        var directory = Directory.CreateTempSubdirectory("bdk-files-reopen-");
        try
        {
            var file = Path.Combine(directory.FullName, "files.db");
            await using (var host = new LiteDbStorageTestHost(file))
            {
                await host.Files().CreateDirectoryAsync("empty");
                await host.Files().WriteFileAsync("source", new MemoryStream([1, 2, 3]));
                (await host.Files().CopyFileAsync(host.Files("second"), "source", "target")).IsSuccess.ShouldBeTrue();
            }

            await using (var host = new LiteDbStorageTestHost(file))
            {
                (await host.Files().DirectoryExistsAsync("empty")).IsSuccess.ShouldBeTrue();
                (await Read(host.Files("second"), "target")).ShouldBe([1, 2, 3]);
            }
        }
        finally { directory.Delete(true); }
    }

    private static async Task<byte[]> Read(IFileStorageProvider provider, string path)
    {
        var opened = await provider.ReadFileAsync(path);
        opened.IsSuccess.ShouldBeTrue(string.Join(";", opened.Errors.Select(error => error.Message)));
        await using var read = opened.Value;
        using var copy = new MemoryStream();
        await read.CopyToAsync(copy);
        return copy.ToArray();
    }

    private sealed class FailedSource : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => throw new IOException("source failed");
        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            await destination.WriteAsync(new byte[LiteDbFileContentStore.ChunkSize + 1], cancellationToken);
            throw new IOException("source failed");
        }
    }
}
