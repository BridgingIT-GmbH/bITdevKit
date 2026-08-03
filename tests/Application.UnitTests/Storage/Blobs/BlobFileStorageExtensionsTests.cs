// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using System.Text;
using Application.Storage;

[UnitTest("Application")]
public sealed class BlobFileStorageExtensionsTests
{
    [Fact]
    public async Task UploadFileAsync_WithExistingFile_UploadsContentAndMetadata()
    {
        // Arrange
        var files = new InMemoryFileStorageProvider("files");
        var blobs = CreateClient();
        var key = new BlobKey("reports", "2026/07/report.pdf");
        var content = Encoding.UTF8.GetBytes("file-content");
        await files.WriteFileAsync("source/report.pdf", new MemoryStream(content));

        // Act
        var result = await blobs.UploadFileAsync(
            files,
            "source/report.pdf",
            key,
            new BlobFileUploadOptions
            {
                Properties = new PropertyBag
                {
                    ["source"] = "filesystem"
                }
            });
        var download = await blobs.DownloadAsync(key);

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.Key.ShouldBe(key);
        result.Value.Length.ShouldBe(content.Length);
        result.Value.ContentType.ShouldBe(ContentType.PDF);
        result.Value.Properties.Get<string>("source").ShouldBe("filesystem");

        download.IsSuccess.ShouldBeTrue();
        await using var blobDownload = download.Value;
        using var target = new MemoryStream();
        await blobDownload.Content.CopyToAsync(target);
        target.ToArray().ShouldBe(content);
    }

    [Fact]
    public async Task UploadFileAsync_WithExplicitContentType_DisablesFileNameInference()
    {
        // Arrange
        var files = new InMemoryFileStorageProvider("files");
        var blobs = CreateClient();
        var key = new BlobKey("reports", "source.bin");
        await files.WriteFileAsync("source/report.pdf", new MemoryStream([1, 2, 3]));

        // Act
        var result = await blobs.UploadFileAsync(
            files,
            "source/report.pdf",
            key,
            new BlobFileUploadOptions
            {
                ContentType = ContentType.JSON
            });

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ContentType.ShouldBe(ContentType.JSON);
    }

    [Fact]
    public async Task UploadFileAsync_WithExpectedContentHash_PassesHashToBlobUpload()
    {
        // Arrange
        var files = new InMemoryFileStorageProvider("files");
        var blobs = CreateClient();
        var key = new BlobKey("reports", "hash.txt");
        var content = Encoding.UTF8.GetBytes("hash-me");
        var expectedHash = $"{BlobContentHash.Prefix}{HashHelper.ComputeSha256(content)}";
        await files.WriteFileAsync("source/hash.txt", new MemoryStream(content));

        // Act
        var result = await blobs.UploadFileAsync(
            files,
            "source/hash.txt",
            key,
            new BlobFileUploadOptions
            {
                ExpectedContentHash = expectedHash
            });

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ContentHash.ShouldBe(expectedHash);
    }

    [Fact]
    public async Task UploadFileAsync_WithMissingFile_ReturnsFileStorageFailure()
    {
        // Arrange
        var files = new InMemoryFileStorageProvider("files");
        var blobs = CreateClient();

        // Act
        var result = await blobs.UploadFileAsync(
            files,
            "missing.txt",
            new BlobKey("reports", "missing.txt"));

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<FileSystemError>().ShouldBeTrue();
    }

    [Fact]
    public async Task UploadFileAsync_WithInvalidBlobKey_DoesNotReadFile()
    {
        // Arrange
        var files = new TrackingReadFileStorageProvider("files", Encoding.UTF8.GetBytes("tracked"));
        var blobs = CreateClient();

        // Act
        var result = await blobs.UploadFileAsync(
            files,
            "tracked.txt",
            new BlobKey("", "tracked.txt"));

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<BlobStoreValidationError>().ShouldBeTrue();
        files.LastReadStream.ShouldBeNull();
    }

    [Fact]
    public async Task UploadFileAsync_WithFileStream_DisposesReadStreamAfterUpload()
    {
        // Arrange
        var files = new TrackingReadFileStorageProvider("files", Encoding.UTF8.GetBytes("tracked"));
        var blobs = CreateClient();

        // Act
        var result = await blobs.UploadFileAsync(
            files,
            "tracked.txt",
            new BlobKey("reports", "tracked.txt"));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        files.LastReadStream.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task DownloadToFileAsync_WithExistingBlob_WritesFileAndDisposesDownloadedContent()
    {
        // Arrange
        var files = new InMemoryFileStorageProvider("files");
        var content = Encoding.UTF8.GetBytes("download-content");
        var stream = new TrackingStream(content);
        var key = new BlobKey("reports", "download.txt");
        var blobs = new ScriptedBlobStoreClient
        {
            DownloadHandler = _ => Result<BlobDownload>.Success(new BlobDownload
            {
                Content = stream,
                Info = new BlobInfo
                {
                    Key = key,
                    Length = content.Length,
                    ContentType = ContentType.TXT
                }
            })
        };

        // Act
        var result = await blobs.DownloadToFileAsync(key, files, "downloads/download.txt");
        var readResult = await files.ReadFileAsync("downloads/download.txt");

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.Blob.Key.ShouldBe(key);
        result.Value.FilePath.ShouldBe("downloads/download.txt");
        result.Value.BytesTransferred.ShouldBe(content.Length);
        stream.IsDisposed.ShouldBeTrue();

        readResult.IsSuccess.ShouldBeTrue();
        await using var readStream = readResult.Value;
        using var target = new MemoryStream();
        await readStream.CopyToAsync(target);
        target.ToArray().ShouldBe(content);
    }

    [Fact]
    public async Task SaveToFileAsync_WithExistingDownload_WritesFileWithoutDisposingDownload()
    {
        // Arrange
        var files = new InMemoryFileStorageProvider("files");
        var content = Encoding.UTF8.GetBytes("save-content");
        var stream = new TrackingStream(content);
        var download = new BlobDownload
        {
            Content = stream,
            Info = new BlobInfo
            {
                Key = new BlobKey("reports", "save.txt"),
                Length = content.Length
            }
        };

        // Act
        var result = await download.SaveToFileAsync(files, "downloads/save.txt");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.BytesTransferred.ShouldBe(content.Length);
        stream.IsDisposed.ShouldBeFalse();

        await download.DisposeAsync();
        stream.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task DownloadToFileAsync_WithMissingBlob_ReturnsBlobFailure()
    {
        // Arrange
        var files = new InMemoryFileStorageProvider("files");
        var key = new BlobKey("reports", "missing.txt");
        var blobs = new ScriptedBlobStoreClient
        {
            DownloadHandler = _ => Result<BlobDownload>.Failure(new BlobStoreNotFoundError(key))
        };

        // Act
        var result = await blobs.DownloadToFileAsync(key, files, "downloads/missing.txt");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<BlobStoreNotFoundError>().ShouldBeTrue();
    }

    [Fact]
    public async Task DownloadToFileAsync_WithInvalidDestinationPath_DoesNotDownloadBlob()
    {
        // Arrange
        var files = new InMemoryFileStorageProvider("files");
        var blobs = new ScriptedBlobStoreClient
        {
            DownloadHandler = key => Result<BlobDownload>.Success(new BlobDownload
            {
                Content = new MemoryStream([1, 2, 3]),
                Info = new BlobInfo { Key = key, Length = 3 }
            })
        };

        // Act
        var result = await blobs.DownloadToFileAsync(
            new BlobKey("reports", "source.txt"),
            files,
            "");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<FileSystemError>().ShouldBeTrue();
        blobs.DownloadCalls.ShouldBe(0);
    }

    [Fact]
    public void PublicBlobStoreClientContract_WithFileBridge_DoesNotExposeFileTransferMethods()
    {
        // Arrange & Act
        var methods = typeof(IBlobStoreClient).GetMethods().Select(method => method.Name).ToArray();

        // Assert
        methods.ShouldNotContain(nameof(BlobFileStorageExtensions.UploadFileAsync));
        methods.ShouldNotContain(nameof(BlobFileStorageExtensions.DownloadToFileAsync));
        methods.ShouldNotContain(nameof(BlobFileStorageExtensions.SaveToFileAsync));
    }

    private static IBlobStoreClient CreateClient()
    {
        var provider = new InMemoryBlobStoreProvider();

        return new BlobStoreClient(InMemoryBlobStoreProvider.ProviderName, provider);
    }

    private sealed class TrackingReadFileStorageProvider(
        string locationName,
        byte[] content) : InMemoryFileStorageProvider(locationName)
    {
        public TrackingStream LastReadStream { get; private set; }

        public override Task<Result<Stream>> ReadFileAsync(
            string path,
            IProgress<FileProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            this.LastReadStream = new TrackingStream(content);

            return Task.FromResult(Result<Stream>.Success(this.LastReadStream));
        }
    }

    private sealed class ScriptedBlobStoreClient : IBlobStoreClient
    {
        public Func<BlobKey, Result<BlobDownload>> DownloadHandler { get; init; }

        public int DownloadCalls { get; private set; }

        public Task<Result<BlobInfo>> UploadAsync(
            BlobUpload upload,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<BlobInfo>.Success(new BlobInfo { Key = upload.Key }));

        public Task<Result<BlobDownload>> DownloadAsync(
            BlobKey key,
            CancellationToken cancellationToken = default)
        {
            this.DownloadCalls++;

            return Task.FromResult(this.DownloadHandler?.Invoke(key) ?? Result<BlobDownload>.Failure(new BlobStoreNotFoundError(key)));
        }

        public Task<Result<BlobInfo>> GetPropertiesAsync(
            BlobKey key,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<BlobInfo>.Success(new BlobInfo { Key = key }));

        public Task<Result<BlobInfo>> UpdatePropertiesAsync(
            BlobPropertiesUpdate update,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<BlobInfo>.Success(new BlobInfo { Key = update.Key }));

        public Task<Result<bool>> ExistsAsync(
            BlobKey key,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<bool>.Success(true));

        public Task<Result<BlobPage>> ListPageAsync(
            BlobQuery query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<BlobPage>.Success(new BlobPage()));

        public Task<Result> DeleteAsync(
            BlobKey key,
            BlobDeleteOptions options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());
    }

    private sealed class TrackingStream(byte[] content) : MemoryStream(content)
    {
        public bool IsDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            this.IsDisposed = true;
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            this.IsDisposed = true;
            await base.DisposeAsync();
        }
    }
}
