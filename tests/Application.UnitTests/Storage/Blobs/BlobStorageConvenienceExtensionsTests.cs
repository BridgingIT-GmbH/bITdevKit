// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using System.Text;
using Application.Jobs;
using Application.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

[UnitTest("Application")]
public sealed class BlobStorageConvenienceExtensionsTests
{
    [Fact]
    public async Task ListAllAsync_WithPagedInMemoryProvider_ReturnsAllMatchingItems()
    {
        // Arrange
        var blobs = CreateClient();
        await blobs.UploadTextAsync(new BlobKey("reports", "2026/a.txt"), "a");
        await blobs.UploadTextAsync(new BlobKey("reports", "2026/b.txt"), "b");
        await blobs.UploadTextAsync(new BlobKey("reports", "2026/c.txt"), "c");
        await blobs.UploadTextAsync(new BlobKey("reports", "2025/a.txt"), "old");

        // Act
        var result = await blobs.ListAllAsync(new BlobQuery
        {
            Container = "reports",
            Prefix = "2026/",
            Take = 2
        });

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.Select(item => item.Key.Name).ShouldBe(["2026/a.txt", "2026/b.txt", "2026/c.txt"]);
    }

    [Fact]
    public async Task UploadBytesAndDownloadBytesAsync_RoundTripsContentAndMetadata()
    {
        // Arrange
        var blobs = CreateClient();
        var key = new BlobKey("assets", "image.bin");
        var bytes = new byte[] { 1, 2, 3, 4 };

        // Act
        var upload = await blobs.UploadBytesAsync(
            key,
            bytes,
            new BlobBytesUploadOptions
            {
                Properties = new PropertyBag { ["kind"] = "thumbnail" }
            });
        var download = await blobs.DownloadBytesAsync(key);

        // Assert
        upload.IsSuccess.ShouldBeTrue();
        upload.Value.ContentType.ShouldBe(ContentType.BIN);
        upload.Value.Properties.Get<string>("kind").ShouldBe("thumbnail");

        download.IsSuccess.ShouldBeTrue();
        download.Value.Info.Key.ShouldBe(key);
        download.Value.Bytes.ShouldBe(bytes);
    }

    [Fact]
    public async Task PropertyPatchHelpers_UpdatePropertiesWithoutChangingContent()
    {
        // Arrange
        var blobs = CreateClient();
        var key = new BlobKey("reports", "review.txt");
        await blobs.UploadTextAsync(key, "unchanged", new BlobTextUploadOptions
        {
            Properties = new PropertyBag { ["status"] = "new", ["temporary"] = true }
        });

        // Act
        await blobs.SetPropertyAsync(key, "status", "approved");
        await blobs.RemovePropertyAsync(key, "temporary");
        var merge = await blobs.MergePropertiesAsync(key, new PropertyBag { ["reviewer"] = "qa" });
        var text = await blobs.DownloadTextAsync(key);

        // Assert
        merge.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, merge.Errors.Select(e => e.Message)));
        merge.Value.Properties.Get<string>("status").ShouldBe("approved");
        merge.Value.Properties.Keys.Contains("temporary").ShouldBeFalse();
        merge.Value.Properties.Get<string>("reviewer").ShouldBe("qa");

        text.IsSuccess.ShouldBeTrue();
        text.Value.Text.ShouldBe("unchanged");
    }

    [Fact]
    public async Task DownloadVerifiedToAsync_WithStoredHash_WritesBytesAndKeepsDestinationOpen()
    {
        // Arrange
        var blobs = CreateClient();
        var key = new BlobKey("reports", "verified.txt");
        await blobs.UploadTextAsync(key, "verified");
        await using var destination = new TrackingStream();

        // Act
        var result = await blobs.DownloadVerifiedToAsync(key, destination);

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.BytesTransferred.ShouldBe(8);
        result.Value.CalculatedContentHash.ShouldStartWith(BlobContentHash.Prefix);
        destination.IsDisposed.ShouldBeFalse();
        destination.ToArray().ShouldBe(Encoding.UTF8.GetBytes("verified"));
    }

    [Fact]
    public async Task DownloadVerifiedToAsync_WithHashMismatch_ReturnsIntegrityFailure()
    {
        // Arrange
        var key = new BlobKey("reports", "bad.txt");
        var blobs = new ScriptedBlobStoreClient
        {
            DownloadHandler = _ => Result<BlobDownload>.Success(new BlobDownload
            {
                Content = new MemoryStream(Encoding.UTF8.GetBytes("actual")),
                Info = new BlobInfo
                {
                    Key = key,
                    ContentHash = $"{BlobContentHash.Prefix}{new string('0', 64)}"
                }
            })
        };

        // Act
        var result = await blobs.DownloadVerifiedToAsync(key, new MemoryStream());

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<BlobStoreIntegrityError>().ShouldBeTrue();
    }

    [Fact]
    public async Task DownloadVerifiedToFileAsync_WithHashMismatch_DoesNotReplaceDestinationFile()
    {
        // Arrange
        var key = new BlobKey("reports", "bad.txt");
        var files = new InMemoryFileStorageProvider("files");
        await files.WriteFileAsync("downloads/bad.txt", new MemoryStream(Encoding.UTF8.GetBytes("existing")));
        var blobs = new ScriptedBlobStoreClient
        {
            DownloadHandler = _ => Result<BlobDownload>.Success(new BlobDownload
            {
                Content = new MemoryStream(Encoding.UTF8.GetBytes("actual")),
                Info = new BlobInfo
                {
                    Key = key,
                    ContentHash = $"{BlobContentHash.Prefix}{new string('0', 64)}"
                }
            })
        };

        // Act
        var result = await blobs.DownloadVerifiedToFileAsync(key, files, "downloads/bad.txt");
        var readResult = await files.ReadFileAsync("downloads/bad.txt");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<BlobStoreIntegrityError>().ShouldBeTrue();

        readResult.IsSuccess.ShouldBeTrue();
        using var reader = new StreamReader(readResult.Value, Encoding.UTF8);
        (await reader.ReadToEndAsync()).ShouldBe("existing");
    }

    [Fact]
    public async Task CopyToAsync_PreservesContentHashContentTypeAndProperties()
    {
        // Arrange
        var source = CreateClient();
        var target = CreateClient();
        var sourceKey = new BlobKey("reports", "source.txt");
        var targetKey = new BlobKey("archive", "source.txt");
        await source.UploadTextAsync(sourceKey, "copy", new BlobTextUploadOptions
        {
            Properties = new PropertyBag { ["source"] = "reports" }
        });

        // Act
        var result = await source.CopyToAsync(sourceKey, target, targetKey);
        var copied = await target.DownloadTextAsync(targetKey);

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.SourceDeleted.ShouldBeFalse();
        result.Value.Target.ContentHash.ShouldBe(result.Value.Source.ContentHash);
        result.Value.Target.ContentType.ShouldBe(ContentType.TXT);
        result.Value.Target.Properties.Get<string>("source").ShouldBe("reports");

        copied.IsSuccess.ShouldBeTrue();
        copied.Value.Text.ShouldBe("copy");
    }

    [Fact]
    public async Task CopyToAsync_WithHashPreservationAndNonSeekableDownload_BuffersSeekableTargetUpload()
    {
        // Arrange
        var sourceKey = new BlobKey("reports", "source.txt");
        var targetKey = new BlobKey("archive", "source.txt");
        var content = Encoding.UTF8.GetBytes("copy");
        var contentHash = $"{BlobContentHash.Prefix}{HashHelper.ComputeSha256(content)}";
        var source = new ScriptedBlobStoreClient
        {
            DownloadHandler = _ => Result<BlobDownload>.Success(new BlobDownload
            {
                Content = new NonSeekableReadStream(content),
                Info = new BlobInfo
                {
                    Key = sourceKey,
                    ContentHash = contentHash,
                    ContentType = ContentType.TXT,
                    Properties = new PropertyBag { ["source"] = "reports" }
                }
            })
        };
        var target = new ScriptedBlobStoreClient
        {
            UploadHandler = upload =>
            {
                upload.ExpectedContentHash.ShouldBe(contentHash);
                upload.Content.CanSeek.ShouldBeTrue();
                using var reader = new StreamReader(upload.Content, Encoding.UTF8, leaveOpen: true);
                reader.ReadToEnd().ShouldBe("copy");

                return Result<BlobInfo>.Success(new BlobInfo
                {
                    Key = upload.Key,
                    ContentHash = upload.ExpectedContentHash,
                    ContentType = upload.ContentType,
                    Properties = upload.Properties
                });
            }
        };

        // Act
        var result = await source.CopyToAsync(sourceKey, target, targetKey);

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.Target.ContentHash.ShouldBe(contentHash);
        result.Value.Target.ContentType.ShouldBe(ContentType.TXT);
        result.Value.Target.Properties.Get<string>("source").ShouldBe("reports");
    }

    [Fact]
    public async Task MoveToAsync_WhenDeleteFails_ReturnsTransferFailureWithPartialState()
    {
        // Arrange
        var sourceKey = new BlobKey("reports", "move.txt");
        var targetKey = new BlobKey("archive", "move.txt");
        var sourceInfo = new BlobInfo
        {
            Key = sourceKey,
            ETag = "\"source-etag\"",
            ContentHash = $"{BlobContentHash.Prefix}{HashHelper.ComputeSha256(Encoding.UTF8.GetBytes("move"))}",
            ContentType = ContentType.TXT
        };
        var source = new ScriptedBlobStoreClient
        {
            DownloadHandler = _ => Result<BlobDownload>.Success(new BlobDownload
            {
                Content = new MemoryStream(Encoding.UTF8.GetBytes("move")),
                Info = sourceInfo
            }),
            DeleteOptionsHandler = (_, options) =>
            {
                options.IfMatchETag.ShouldBe(sourceInfo.ETag);
                return Result.Failure(new BlobStoreProviderError("delete failed"));
            }
        };
        var target = CreateClient();

        // Act
        var result = await source.MoveToAsync(sourceKey, target, targetKey);

        // Assert
        result.IsFailure.ShouldBeTrue();
        var error = result.Errors.OfType<BlobStoreTransferError>().Single();
        error.CopySucceeded.ShouldBeTrue();
        error.DeleteSucceeded.ShouldBeFalse();
        error.Source.ShouldBe(sourceInfo);
        error.Target.Key.ShouldBe(targetKey);
    }

    [Fact]
    public async Task CopyToAsync_PreservesExpirationByDefaultAndAllowsOverride()
    {
        // Arrange
        var source = CreateClient();
        var target = CreateClient();
        var sourceKey = new BlobKey("reports", "expiring.txt");
        var preservedExpiration = DateTimeOffset.UtcNow.AddHours(2);
        var overriddenExpiration = preservedExpiration.AddHours(2);
        await source.UploadAsync(new BlobUpload
        {
            Key = sourceKey,
            Content = new MemoryStream(Encoding.UTF8.GetBytes("content")),
            ExpiresAt = preservedExpiration
        });

        // Act
        var preserved = await source.CopyToAsync(sourceKey, target, new BlobKey("archive", "preserved.txt"));
        var overridden = await source.CopyToAsync(
            sourceKey,
            target,
            new BlobKey("archive", "overridden.txt"),
            new BlobCopyOptions { ExpiresAtOverride = overriddenExpiration });

        // Assert
        preserved.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, preserved.Errors.Select(e => e.Message)));
        preserved.Value.Target.ExpiresAt.ShouldBe(preservedExpiration);
        overridden.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, overridden.Errors.Select(e => e.Message)));
        overridden.Value.Target.ExpiresAt.ShouldBe(overriddenExpiration);
    }

    [Fact]
    public async Task MoveToAsync_WithSameClientAndKey_IsSuccessfulNoOp()
    {
        // Arrange
        var client = CreateClient();
        var key = new BlobKey("reports", "same.txt");
        await client.UploadAsync(new BlobUpload
        {
            Key = key,
            Content = new MemoryStream(Encoding.UTF8.GetBytes("content"))
        });

        // Act
        var result = await client.MoveToAsync(key, client, key);

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.SourceDeleted.ShouldBeFalse();
        result.Value.Source.ShouldBe(result.Value.Target);
        (await client.ExistsAsync(key)).Value.ShouldBeTrue();
    }

    [Fact]
    public async Task MoveToAsync_WithMissingClients_ReturnsArgumentError()
    {
        // Arrange
        var key = new BlobKey("reports", "same.txt");

        // Act
        var result = await BlobTransferStorageExtensions.MoveToAsync(null, key, null, key);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<ArgumentError>().ShouldBeTrue();
    }

    [Fact]
    public async Task SetPropertyAsync_PreservesExistingExpiration()
    {
        // Arrange
        var client = CreateClient();
        var key = new BlobKey("reports", "properties.txt");
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);
        await client.UploadAsync(new BlobUpload
        {
            Key = key,
            Content = new MemoryStream(Encoding.UTF8.GetBytes("content")),
            ExpiresAt = expiresAt
        });

        // Act
        var result = await client.SetPropertyAsync(key, "reviewed", true);

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.ExpiresAt.ShouldBe(expiresAt);
        result.Value.Properties.Get<bool>("reviewed").ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteByPrefixAsync_DryRunAndDeleteRespectPrefix()
    {
        // Arrange
        var blobs = CreateClient();
        await blobs.UploadTextAsync(new BlobKey("reports", "tmp/a.txt"), "a");
        await blobs.UploadTextAsync(new BlobKey("reports", "tmp/b.txt"), "b");
        await blobs.UploadTextAsync(new BlobKey("reports", "keep/c.txt"), "c");

        // Act
        var dryRun = await blobs.DeleteByPrefixAsync("reports", "tmp/", new BlobDeletePrefixOptions { DryRun = true });
        var afterDryRun = await blobs.ListAllAsync(new BlobQuery { Container = "reports", Prefix = "tmp/" });
        var delete = await blobs.DeleteByPrefixAsync("reports", "tmp/");
        var afterDelete = await blobs.ListAllAsync(new BlobQuery { Container = "reports", Prefix = "keep/" });

        // Assert
        dryRun.IsSuccess.ShouldBeTrue();
        dryRun.Value.CandidateCount.ShouldBe(2);
        dryRun.Value.DeletedCount.ShouldBe(0);
        afterDryRun.Value.Count.ShouldBe(2);

        delete.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, delete.Errors.Select(e => e.Message)));
        delete.Value.DeletedCount.ShouldBe(2);
        afterDelete.Value.Select(item => item.Key.Name).ShouldBe(["keep/c.txt"]);
    }

    [Fact]
    public async Task DeleteByPrefixAsync_WithoutPrefixAndApproval_ReturnsQueryTooBroad()
    {
        // Arrange
        var blobs = CreateClient();

        // Act
        var result = await blobs.DeleteByPrefixAsync("reports", string.Empty);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<BlobStoreQueryTooBroadError>().ShouldBeTrue();
    }

    [Fact]
    public async Task DiagnosticsSnapshot_WithHealthyInMemoryClient_ReturnsReadableClientStatus()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBlobStorage()
            .WithInMemoryClient("reports");
        using var serviceProvider = services.BuildServiceProvider();

        // Act
        var result = await serviceProvider.GetRequiredService<IBlobStorageDiagnosticsService>().GetSnapshotAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ClientCount.ShouldBe(1);
        result.Value.HealthyClientCount.ShouldBe(1);
        result.Value.Clients.Single().Name.ShouldBe("reports");
        result.Value.Clients.Single().ProviderName.ShouldBe(InMemoryBlobStoreProvider.ProviderName);
        result.Value.Clients.Single().HealthStatus.ShouldBe("Healthy");
    }

    [Fact]
    public async Task BlobDeletePrefixMaintenanceJob_Process_DeletesByPrefixUsingNamedBlobClient()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBlobStorage()
            .WithInMemoryClient("reports");
        using var serviceProvider = services.BuildServiceProvider();
        var blobs = serviceProvider.GetRequiredService<IBlobStoreClientFactory>().CreateClient("reports");
        await blobs.UploadTextAsync(new BlobKey("reports", "tmp/a.txt"), "a");
        await blobs.UploadTextAsync(new BlobKey("reports", "keep/b.txt"), "b");
        var sut = new BlobDeletePrefixMaintenanceJob(
            NullLogger<BlobDeletePrefixMaintenanceJob>.Instance,
            serviceProvider.GetRequiredService<IBlobStoreClientFactory>());
        var context = new JobExecutionContextBuilder<BlobDeletePrefixMaintenanceJobData>()
            .WithJobName("blob-delete-prefix")
            .WithData(new BlobDeletePrefixMaintenanceJobData
            {
                StoreName = "reports",
                Container = "reports",
                Prefix = "tmp/"
            })
            .Build();

        // Act
        var result = await sut.ExecuteAsync(context);
        var remaining = await blobs.ListAllAsync(new BlobQuery { Container = "reports", Prefix = "keep/" });

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        context.Items["CandidateCount"].ShouldBe(1);
        context.Items["DeletedCount"].ShouldBe(1);
        context.Items["DryRun"].ShouldBe(false);
        remaining.Value.Select(item => item.Key.Name).ShouldBe(["keep/b.txt"]);
    }

    [Fact]
    public async Task FileMonitoringLocationScanJob_ExecuteAsync_ScansLocationWithTypedData()
    {
        // Arrange
        FileScanOptions capturedOptions = null;
        var service = Substitute.For<IFileMonitoringService>();
        service.ScanLocationAsync("inbound", Arg.Do<FileScanOptions>(options => capturedOptions = options), Arg.Any<IProgress<FileScanProgress>>(), Arg.Any<CancellationToken>())
            .Returns(new FileScanContext
            {
                LocationName = "inbound",
                Events =
                [
                    new FileEvent { FilePath = "a.txt", EventType = FileEventType.Added },
                    new FileEvent { FilePath = "b.txt", EventType = FileEventType.Deleted }
                ]
            });
        var sut = new FileMonitoringLocationScanJob(
            NullLogger<FileMonitoringLocationScanJob>.Instance,
            service);
        var context = new JobExecutionContextBuilder<FileMonitoringLocationScanJobData>()
            .WithJobName("scan-inbound")
            .WithData(new FileMonitoringLocationScanJobData
            {
                LocationName = "inbound",
                DelayPerFile = TimeSpan.FromMilliseconds(25),
                FileFilter = "*.txt",
                FileBlackListFilter = ["*.tmp"],
                MaxFilesToScan = 10
            })
            .Build();

        // Act
        var result = await sut.ExecuteAsync(context);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        capturedOptions.ShouldNotBeNull();
        capturedOptions.DelayPerFile.ShouldBe(TimeSpan.FromMilliseconds(25));
        capturedOptions.FileFilter.ShouldBe("*.txt");
        capturedOptions.FileBlackListFilter.ShouldBe(["*.tmp"]);
        capturedOptions.MaxFilesToScan.ShouldBe(10);
        context.Items["DetectedEvents"].ShouldBe(2);
        context.Items["DetectedAddedEvents"].ShouldBe(1);
        context.Items["DetectedDeletedEvents"].ShouldBe(1);
    }

    [Fact]
    public void PublicBlobStoreClientContract_WithConvenienceHelpers_DoesNotExposeHelperMethods()
    {
        // Arrange & Act
        var methods = typeof(IBlobStoreClient).GetMethods().Select(method => method.Name).ToArray();

        // Assert
        methods.ShouldNotContain(nameof(BlobEnumerationExtensions.ListAllAsync));
        methods.ShouldNotContain(nameof(BlobPropertyStorageExtensions.SetPropertyAsync));
        methods.ShouldNotContain(nameof(BlobBytesStorageExtensions.UploadBytesAsync));
        methods.ShouldNotContain(nameof(BlobVerifiedDownloadExtensions.DownloadVerifiedToAsync));
        methods.ShouldNotContain(nameof(BlobTransferStorageExtensions.CopyToAsync));
        methods.ShouldNotContain(nameof(BlobTransferStorageExtensions.DeleteByPrefixAsync));
    }

    private static IBlobStoreClient CreateClient()
    {
        var provider = new InMemoryBlobStoreProvider();

        return new BlobStoreClient(InMemoryBlobStoreProvider.ProviderName, provider);
    }

    private sealed class ScriptedBlobStoreClient : IBlobStoreClient
    {
        public Func<BlobUpload, Result<BlobInfo>> UploadHandler { get; init; }

        public Func<BlobKey, Result<BlobDownload>> DownloadHandler { get; init; }

        public Func<BlobKey, Result> DeleteHandler { get; init; }

        public Func<BlobKey, BlobDeleteOptions, Result> DeleteOptionsHandler { get; init; }

        public Task<Result<BlobInfo>> UploadAsync(
            BlobUpload upload,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(this.UploadHandler?.Invoke(upload) ?? Result<BlobInfo>.Success(new BlobInfo
            {
                Key = upload.Key,
                ContentHash = upload.ExpectedContentHash,
                ContentType = upload.ContentType,
                Properties = upload.Properties
            }));

        public Task<Result<BlobDownload>> DownloadAsync(
            BlobKey key,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(this.DownloadHandler?.Invoke(key) ?? Result<BlobDownload>.Failure(new BlobStoreNotFoundError(key)));

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
            Task.FromResult(this.DeleteOptionsHandler?.Invoke(key, options) ?? this.DeleteHandler?.Invoke(key) ?? Result.Success());
    }

    private sealed class NonSeekableReadStream(byte[] content) : Stream
    {
        private readonly MemoryStream inner = new(content);

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count) =>
            this.inner.Read(buffer, offset, count);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            this.inner.ReadAsync(buffer, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class TrackingStream : MemoryStream
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
