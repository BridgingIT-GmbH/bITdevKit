// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Storage;

using System.Text;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Application.Storage;
using Infrastructure.EntityFramework;
using Infrastructure.EntityFramework.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[UnitTest("Infrastructure")]
public sealed class EntityFrameworkBlobStoreProviderUploadDownloadTests
{
    [Theory]
    [InlineData(4, 10, 2)]
    [InlineData(1, 8, 4)]
    public async Task UploadAsync_FlushesChunksByConfiguredCount(
        int chunkFlushCount,
        int contentLength,
        int expectedChunkFlushCount)
    {
        // Arrange
        await using var context = CreateContext();
        var sut = CreateProvider(context, new BlobStoreOptions
        {
            ChunkSize = 2,
            ChunkFlushCount = chunkFlushCount,
            MaxPendingChunkBytes = 1024
        });

        // Act
        var result = await sut.UploadAsync(CreateUpload(new string('x', contentLength)));

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        context.ChunkFlushSaveCount.ShouldBe(expectedChunkFlushCount);
        context.MaxAddedChunksDuringSave.ShouldBeLessThanOrEqualTo(chunkFlushCount);
    }

    [Fact]
    public async Task UploadAsync_FlushesWhenPendingByteLimitIsReached()
    {
        // Arrange
        await using var context = CreateContext();
        var sut = CreateProvider(context, new BlobStoreOptions
        {
            ChunkSize = 3,
            ChunkFlushCount = 100,
            MaxPendingChunkBytes = 5
        });

        // Act
        var result = await sut.UploadAsync(CreateUpload("abcdefghij"));

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        context.ChunkFlushSaveCount.ShouldBe(2);
        context.MaxAddedChunksDuringSave.ShouldBe(2);
    }

    [Fact]
    public async Task UploadAsync_FlushesFinalPartialChunkGroup()
    {
        // Arrange
        await using var context = CreateContext();
        var sut = CreateProvider(context, new BlobStoreOptions
        {
            ChunkSize = 2,
            ChunkFlushCount = 2,
            MaxPendingChunkBytes = 1024
        });

        // Act
        var result = await sut.UploadAsync(CreateUpload("abcdefghij"));

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        context.ChunkFlushSaveCount.ShouldBe(3);
        context.ChangeTracker.Entries<StorageBlobChunk>().ShouldBeEmpty();
    }

    [Fact]
    public async Task UploadAsync_WithValidContent_StoresContentInChunks()
    {
        // Arrange
        await using var context = CreateContext();
        var sut = CreateProvider(context, new BlobStoreOptions { ChunkSize = 3 });

        // Act
        var result = await sut.UploadAsync(CreateUpload("abcdefghij"));

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        var blob = await context.StorageBlobs.SingleAsync();
        var chunks = await context.StorageBlobChunks.OrderBy(e => e.Index).ToListAsync();
        blob.Length.ShouldBe(10);
        chunks.Count.ShouldBe(4);
        chunks.Select(e => e.Length).ShouldBe([3, 3, 3, 1]);
        Encoding.UTF8.GetString(chunks.SelectMany(e => e.Content.Take(e.Length)).ToArray()).ShouldBe("abcdefghij");
    }

    [Fact]
    public async Task UploadAsync_WithNonSeekableStream_ReadsInConfiguredChunks()
    {
        // Arrange
        await using var context = CreateContext();
        var sut = CreateProvider(context, new BlobStoreOptions { ChunkSize = 4 });
        var stream = new CountingNonSeekableReadStream(Encoding.UTF8.GetBytes("abcdefghijkl"));

        // Act
        var result = await sut.UploadAsync(CreateUpload(stream));

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        stream.MaxRequestedReadSize.ShouldBeLessThanOrEqualTo(4);
        stream.ReadCount.ShouldBeGreaterThan(1);
    }

    [Fact]
    public async Task UploadAsync_WithOverwrite_ReplacesOldChunks()
    {
        // Arrange
        await using var context = CreateContext();
        var sut = CreateProvider(context, new BlobStoreOptions { ChunkSize = 2 });
        (await sut.UploadAsync(CreateUpload("abcdef"))).IsSuccess.ShouldBeTrue();

        // Act
        var result = await sut.UploadAsync(CreateUpload("xy"));

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        var chunks = await context.StorageBlobChunks.OrderBy(e => e.Index).ToListAsync();
        chunks.Count.ShouldBe(1);
        Encoding.UTF8.GetString(chunks.Single().Content).ShouldBe("xy");
    }

    [Fact]
    public async Task UploadAsync_WithFailIfExists_ReturnsConflict()
    {
        // Arrange
        await using var context = CreateContext();
        var sut = CreateProvider(context);
        (await sut.UploadAsync(CreateUpload("first"))).IsSuccess.ShouldBeTrue();

        // Act
        var result = await sut.UploadAsync(CreateUpload("second", overwriteMode: BlobOverwriteMode.FailIfExists));

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<BlobStoreConflictError>().ShouldBeTrue();
        var chunks = await context.StorageBlobChunks.OrderBy(e => e.Index).ToListAsync();
        Encoding.UTF8.GetString(chunks.SelectMany(e => e.Content.Take(e.Length)).ToArray()).ShouldBe("first");
    }

    [Fact]
    public async Task UploadAsync_WithContent_StoresSha256ContentHash()
    {
        // Arrange
        await using var context = CreateContext();
        var sut = CreateProvider(context);
        var content = Encoding.UTF8.GetBytes("hash me");

        // Act
        var result = await sut.UploadAsync(CreateUpload(new MemoryStream(content)));

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        var expected = $"{BlobContentHash.Prefix}{HashHelper.ComputeSha256(content)}";
        result.Value.ContentHash.ShouldBe(expected);
        (await context.StorageBlobs.SingleAsync()).ContentHash.ShouldBe(expected);
    }

    [Fact]
    public async Task UploadAsync_WhenMaxBlobSizeExceeded_DoesNotCommitPartialContent()
    {
        // Arrange
        await using var context = CreateContext();
        var sut = CreateProvider(context, new BlobStoreOptions { ChunkSize = 2, MaxBlobSize = 3 });

        // Act
        var result = await sut.UploadAsync(CreateUpload("abcd"));

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<BlobStoreSizeLimitExceededError>().ShouldBeTrue();
        (await context.StorageBlobs.CountAsync()).ShouldBe(0);
        (await context.StorageBlobChunks.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task UploadAsync_WhenExpectedHashDoesNotMatch_DoesNotCommitPartialContent()
    {
        // Arrange
        await using var context = CreateContext();
        var sut = CreateProvider(context, new BlobStoreOptions { ChunkSize = 2 });

        // Act
        var result = await sut.UploadAsync(CreateUpload("abcd", expectedHash:
            "sha256:0000000000000000000000000000000000000000000000000000000000000000"));

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<BlobStoreIntegrityError>().ShouldBeTrue();
        (await context.StorageBlobs.CountAsync()).ShouldBe(0);
        (await context.StorageBlobChunks.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task UploadAsync_WithWrite_AcquiresAndReleasesLease()
    {
        // Arrange
        await using var context = CreateContext();
        var sut = CreateProvider(context, new BlobStoreOptions { ChunkSize = 2 });

        // Act
        var result = await sut.UploadAsync(CreateUpload("abcd"));

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        context.ObservedLeaseDuringSave.ShouldBeTrue();
        var blob = await context.StorageBlobs.SingleAsync();
        blob.LeaseId.ShouldBeNull();
        blob.LeaseAcquiredBy.ShouldBeNull();
        blob.LeaseAcquiredUntil.ShouldBeNull();
    }

    [Fact]
    public async Task UploadAsync_WithExpiredLease_ReusesBlob()
    {
        // Arrange
        await using var context = CreateContext();
        var blob = SeedExpiredLeasedBlob(context, "old");
        await context.SaveChangesAsync();
        var sut = CreateProvider(context, new BlobStoreOptions { ChunkSize = 2 });

        // Act
        var result = await sut.UploadAsync(CreateUpload("new"));

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        var stored = await context.StorageBlobs.SingleAsync();
        stored.Id.ShouldBe(blob.Id);
        stored.ContentHash.ShouldBe(result.Value.ContentHash);
        var content = await ReadStoredContentAsync(context);
        content.ShouldBe("new");
    }

    [Fact]
    public async Task UploadAsync_WhenStreamFails_RollsBackChunksAndMetadata()
    {
        // Arrange
        await using var context = CreateContext();
        var sut = CreateProvider(context, new BlobStoreOptions { ChunkSize = 2 });
        (await sut.UploadAsync(CreateUpload("stable"))).IsSuccess.ShouldBeTrue();
        var failing = new ThrowingReadStream(Encoding.UTF8.GetBytes("broken"), throwAfterBytes: 3);

        // Act
        var result = await sut.UploadAsync(CreateUpload(failing));

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<BlobStoreProviderError>().ShouldBeTrue();
        (await context.StorageBlobs.CountAsync()).ShouldBe(1);
        (await ReadStoredContentAsync(context)).ShouldBe("stable");
    }

    [Fact]
    public async Task UploadAsync_WhenLaterChunkGroupSaveFails_RestoresExistingBlob()
    {
        // Arrange
        await using var context = CreateContext();
        var sut = CreateProvider(context, new BlobStoreOptions
        {
            ChunkSize = 2,
            ChunkFlushCount = 2,
            MaxPendingChunkBytes = 1024
        });
        (await sut.UploadAsync(CreateUpload("stable"))).IsSuccess.ShouldBeTrue();
        context.ResetSaveObservation();
        context.FailOnChunkFlushNumber = 2;

        // Act
        var result = await sut.UploadAsync(CreateUpload("replacement-content"));

        // Assert
        result.HasError<BlobStoreProviderError>().ShouldBeTrue();
        context.ChunkFlushSaveCount.ShouldBeGreaterThanOrEqualTo(2);
        (await context.StorageBlobs.CountAsync()).ShouldBe(1);
        (await ReadStoredContentAsync(context)).ShouldBe("stable");
    }

    [Fact]
    public async Task UploadAsync_WithChunkGroups_EmitsLowCardinalityEfMetrics()
    {
        // Arrange
        await using var context = CreateContext();
        using var meterFactory = new TestMeterFactory();
        using var recorder = new RecordingMetrics();
        var sut = new EntityFrameworkBlobStoreProvider<TestBlobDbContext>(
            new SingleContextScopeFactory<TestBlobDbContext>(context),
            new BlobStoreOptions
            {
                ChunkSize = 2,
                ChunkFlushCount = 2,
                MaxPendingChunkBytes = 1024
            },
            metricsService: new MetricsService(meterFactory),
            storeName: "reports");

        // Act
        var result = await sut.UploadAsync(CreateUpload("abcdefghij"));

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        recorder.Sum("blobstorage_ef_chunks_written").ShouldBe(5);
        recorder.Sum("blobstorage_ef_chunk_flushes").ShouldBe(3);
        recorder.Count("blobstorage_ef_chunks_per_flush").ShouldBe(3);
        recorder.Count("blobstorage_ef_bytes_per_flush").ShouldBe(3);
        recorder.TagValues.ShouldNotContain("file.bin");
    }

    [Fact]
    public async Task UploadAsync_WhenChunkFlushLoggerThrows_PersistsUploadAndDetachesChunks()
    {
        // Arrange
        await using var context = CreateContext();
        var sut = new EntityFrameworkBlobStoreProvider<TestBlobDbContext>(
            new SingleContextScopeFactory<TestBlobDbContext>(context),
            new BlobStoreOptions
            {
                ChunkSize = 2,
                ChunkFlushCount = 2,
                MaxPendingChunkBytes = 1024
            },
            loggerFactory: new ThrowingLoggerFactory(),
            storeName: "reports");

        // Act
        var result = await sut.UploadAsync(CreateUpload("abcdefghij"));

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        context.ChangeTracker.Entries<StorageBlobChunk>().ShouldBeEmpty();
        (await ReadStoredContentAsync(context)).ShouldBe("abcdefghij");
    }

    [Fact]
    public async Task UploadAsync_WhenCanceledAfterPartialWrite_CleansUpNewBlob()
    {
        // Arrange
        await using var context = CreateContext();
        var sut = CreateProvider(context, new BlobStoreOptions { ChunkSize = 2 });
        var canceling = new CancelingReadStream(Encoding.UTF8.GetBytes("broken"), cancelAfterBytes: 2);

        // Act
        var action = async () => await sut.UploadAsync(CreateUpload(canceling));

        // Assert
        await action.ShouldThrowAsync<OperationCanceledException>();
        (await context.StorageBlobs.CountAsync()).ShouldBe(0);
        (await context.StorageBlobChunks.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task UploadAsync_WhenCanceledDuringOverwrite_RestoresExistingBlob()
    {
        // Arrange
        await using var context = CreateContext();
        var sut = CreateProvider(context, new BlobStoreOptions { ChunkSize = 2 });
        (await sut.UploadAsync(CreateUpload("stable"))).IsSuccess.ShouldBeTrue();
        var canceling = new CancelingReadStream(Encoding.UTF8.GetBytes("broken"), cancelAfterBytes: 2);

        // Act
        var action = async () => await sut.UploadAsync(CreateUpload(canceling));

        // Assert
        await action.ShouldThrowAsync<OperationCanceledException>();
        (await context.StorageBlobs.CountAsync()).ShouldBe(1);
        (await ReadStoredContentAsync(context)).ShouldBe("stable");
    }

    [Fact]
    public async Task DownloadAsync_WithExistingBlob_StreamsChunksInOrder()
    {
        // Arrange
        await using var context = CreateContext();
        var sut = CreateProvider(context, new BlobStoreOptions { ChunkSize = 3 });
        (await sut.UploadAsync(CreateUpload("abcdefghi"))).IsSuccess.ShouldBeTrue();

        // Act
        var result = await sut.DownloadAsync(new BlobKey("reports", "file.bin"));

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        await using var download = result.Value;
        using var target = new MemoryStream();
        await download.Content.CopyToAsync(target);
        Encoding.UTF8.GetString(target.ToArray()).ShouldBe("abcdefghi");
        result.Value.Info.Length.ShouldBe(9);
    }

    [Fact]
    public async Task DownloadAsync_WithMissingBlob_ReturnsNotFound()
    {
        // Arrange
        await using var context = CreateContext();
        var sut = CreateProvider(context);

        // Act
        var result = await sut.DownloadAsync(new BlobKey("reports", "missing.bin"));

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<BlobStoreNotFoundError>().ShouldBeTrue();
    }

    [Fact]
    public async Task BlobDownload_DisposeAsync_DisposesReturnedContentStream()
    {
        // Arrange
        await using var context = CreateContext();
        var sut = CreateProvider(context);
        (await sut.UploadAsync(CreateUpload("content"))).IsSuccess.ShouldBeTrue();
        var result = await sut.DownloadAsync(new BlobKey("reports", "file.bin"));

        // Act
        await result.Value.DisposeAsync();
        var action = async () => await result.Value.Content.ReadAsync(new byte[1]);

        // Assert
        await action.ShouldThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task UploadAsync_DoesNotDisposeCallerUploadStream()
    {
        // Arrange
        await using var context = CreateContext();
        var sut = CreateProvider(context);
        var stream = new DisposeTrackingStream(Encoding.UTF8.GetBytes("content"));

        // Act
        var result = await sut.UploadAsync(CreateUpload(stream));

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        stream.WasDisposed.ShouldBeFalse();
    }

    [Fact]
    public async Task UpdatePropertiesAsync_WithExistingBlob_DoesNotRewriteChunks()
    {
        // Arrange
        await using var context = CreateContext();
        var sut = CreateProvider(context, new BlobStoreOptions { ChunkSize = 2 });
        (await sut.UploadAsync(CreateUpload("content"))).IsSuccess.ShouldBeTrue();
        var chunksBefore = await ReadStoredContentAsync(context);
        context.ResetObservedState();

        // Act
        var result = await sut.UpdatePropertiesAsync(new BlobPropertiesUpdate
        {
            Key = new BlobKey("reports", "file.bin"),
            ContentType = ContentType.JSON,
            Properties = new PropertyBag
            {
                ["reviewed"] = true
            }
        });

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        context.StorageBlobChunksAccessCount.ShouldBe(0);
        (await ReadStoredContentAsync(context)).ShouldBe(chunksBefore);
        result.Value.ContentType.ShouldBe(ContentType.JSON);
        result.Value.Properties.Get<bool>("reviewed").ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteAsync_WithExistingBlob_RemovesBlobAndChunks()
    {
        // Arrange
        await using var context = CreateContext();
        var sut = CreateProvider(context, new BlobStoreOptions { ChunkSize = 2 });
        (await sut.UploadAsync(CreateUpload("content"))).IsSuccess.ShouldBeTrue();

        // Act
        var result = await sut.DeleteAsync(new BlobKey("reports", "file.bin"));

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        (await context.StorageBlobs.CountAsync()).ShouldBe(0);
        (await context.StorageBlobChunks.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task DeleteAsync_WithExistingBlob_AcquiresLease()
    {
        // Arrange
        await using var context = CreateContext();
        var sut = CreateProvider(context);
        (await sut.UploadAsync(CreateUpload("content"))).IsSuccess.ShouldBeTrue();
        context.ResetObservedState();

        // Act
        var result = await sut.DeleteAsync(new BlobKey("reports", "file.bin"));

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        context.ObservedLeaseDuringSave.ShouldBeTrue();
    }

    private static EntityFrameworkBlobStoreProvider<TestBlobDbContext> CreateProvider(
        TestBlobDbContext context,
        BlobStoreOptions options = null) =>
        new(
            new SingleContextScopeFactory<TestBlobDbContext>(context),
            options ?? new BlobStoreOptions { ChunkSize = 4 });

    private static BlobUpload CreateUpload(
        string content,
        string expectedHash = null,
        BlobOverwriteMode overwriteMode = BlobOverwriteMode.Overwrite,
        string name = "file.bin") =>
        CreateUpload(new MemoryStream(Encoding.UTF8.GetBytes(content)), expectedHash, overwriteMode, name);

    private static BlobUpload CreateUpload(
        Stream content,
        string expectedHash = null,
        BlobOverwriteMode overwriteMode = BlobOverwriteMode.Overwrite,
        string name = "file.bin") =>
        new()
        {
            Key = new BlobKey("reports", name),
            Content = content,
            ContentType = ContentType.TXT,
            ExpectedContentHash = expectedHash,
            OverwriteMode = overwriteMode,
            Properties = new PropertyBag
            {
                ["source"] = "unit-test"
            }
        };

    private static TestBlobDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TestBlobDbContext>()
            .UseInMemoryDatabase($"blob-upload-download-{Guid.NewGuid():N}")
            .Options);

    private sealed class SingleContextScopeFactory<TContext>(TContext context)
        : IServiceScopeFactory, IServiceScope, IServiceProvider
        where TContext : DbContext
    {
        public IServiceProvider ServiceProvider => this;

        public IServiceScope CreateScope() => this;

        public object GetService(Type serviceType) => serviceType == typeof(TContext) ? context : null;

        public void Dispose() { }
    }

    private static StorageBlob SeedExpiredLeasedBlob(TestBlobDbContext context, string content)
    {
        var blob = new StorageBlob
        {
            Id = Guid.NewGuid().ToString("N"),
            Container = "reports",
            Name = "file.bin",
            ContainerHash = HashHelper.ComputeSha256("reports"),
            NameHash = HashHelper.ComputeSha256("file.bin"),
            Length = content.Length,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            LastModifiedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            ETag = "\"old\"",
            ContentHash = $"{BlobContentHash.Prefix}{HashHelper.ComputeSha256(Encoding.UTF8.GetBytes(content))}",
            LeaseId = "expired",
            LeaseAcquiredBy = "other",
            LeaseAcquiredUntil = DateTimeOffset.UtcNow.AddMinutes(-1)
        };
        context.StorageBlobs.Add(blob);
        context.StorageBlobChunks.Add(new StorageBlobChunk
        {
            BlobId = blob.Id,
            Index = 0,
            Content = Encoding.UTF8.GetBytes(content),
            Length = content.Length
        });

        return blob;
    }

    private static async Task<string> ReadStoredContentAsync(TestBlobDbContext context)
    {
        var chunks = await context.StorageBlobChunks.OrderBy(e => e.Index).ToListAsync();
        return Encoding.UTF8.GetString(chunks.SelectMany(e => e.Content.Take(e.Length)).ToArray());
    }

    private sealed class TestBlobDbContext(DbContextOptions<TestBlobDbContext> options)
        : DbContext(options), IBlobStoreContext
    {
        public bool ObservedLeaseDuringSave { get; private set; }

        public int SaveChangesCount { get; private set; }

        public int ChunkFlushSaveCount { get; private set; }

        public int MaxAddedChunksDuringSave { get; private set; }

        public int? FailOnChunkFlushNumber { get; set; }

        public int StorageBlobChunksAccessCount { get; private set; }

        public DbSet<StorageBlob> StorageBlobs { get; set; }

        public DbSet<StorageBlobChunk> StorageBlobChunks
        {
            get
            {
                this.StorageBlobChunksAccessCount++;
                return this.Set<StorageBlobChunk>();
            }

            set { }
        }

        public void ResetObservedState()
        {
            this.ObservedLeaseDuringSave = false;
            this.StorageBlobChunksAccessCount = 0;
        }

        public void ResetSaveObservation()
        {
            this.SaveChangesCount = 0;
            this.ChunkFlushSaveCount = 0;
            this.MaxAddedChunksDuringSave = 0;
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            this.SaveChangesCount++;
            var addedChunkCount = this.ChangeTracker.Entries<StorageBlobChunk>()
                .Count(entry => entry.State == EntityState.Added);
            if (addedChunkCount > 0)
            {
                this.ChunkFlushSaveCount++;
                if (this.ChunkFlushSaveCount == this.FailOnChunkFlushNumber)
                {
                    throw new DbUpdateException("simulated chunk group save failure");
                }
            }

            this.MaxAddedChunksDuringSave = Math.Max(this.MaxAddedChunksDuringSave, addedChunkCount);

            if (this.ChangeTracker.Entries<StorageBlob>().Any(e =>
                e.Entity.LeaseId is not null &&
                e.Entity.LeaseAcquiredBy is not null &&
                e.Entity.LeaseAcquiredUntil is not null))
            {
                this.ObservedLeaseDuringSave = true;
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }

    private sealed class CountingNonSeekableReadStream(byte[] content) : Stream
    {
        private readonly MemoryStream inner = new(content);

        public int ReadCount { get; private set; }

        public int MaxRequestedReadSize { get; private set; }

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

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            this.ReadCount++;
            this.MaxRequestedReadSize = Math.Max(this.MaxRequestedReadSize, buffer.Length);
            return await this.inner.ReadAsync(buffer, cancellationToken);
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class ThrowingReadStream(byte[] content, int throwAfterBytes) : Stream
    {
        private readonly MemoryStream inner = new(content);
        private int totalRead;

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

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (this.totalRead >= throwAfterBytes)
            {
                throw new IOException("simulated upload failure");
            }

            var read = await this.inner.ReadAsync(buffer, cancellationToken);
            this.totalRead += read;
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class CancelingReadStream(byte[] content, int cancelAfterBytes) : Stream
    {
        private readonly MemoryStream inner = new(content);
        private int totalRead;

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

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (this.totalRead >= cancelAfterBytes)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            var read = await this.inner.ReadAsync(buffer, cancellationToken);
            this.totalRead += read;
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class DisposeTrackingStream(byte[] content) : MemoryStream(content)
    {
        public bool WasDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            this.WasDisposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class TestMeterFactory : IMeterFactory
    {
        private readonly ConcurrentDictionary<string, Meter> meters = new(StringComparer.Ordinal);

        public Meter Create(MeterOptions options) =>
            this.meters.GetOrAdd(options.Name, _ => new Meter(options));

        public void Dispose()
        {
            foreach (var meter in this.meters.Values)
            {
                meter.Dispose();
            }
        }
    }

    private sealed class ThrowingLoggerFactory : ILoggerFactory
    {
        public void AddProvider(ILoggerProvider provider)
        {
        }

        public ILogger CreateLogger(string categoryName) => new ThrowingLogger();

        public void Dispose()
        {
        }
    }

    private sealed class ThrowingLogger : ILogger
    {
        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull =>
            NoopDisposable.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception exception,
            Func<TState, Exception, string> formatter) =>
            throw new InvalidOperationException("simulated telemetry failure");
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static NoopDisposable Instance { get; } = new();

        public void Dispose()
        {
        }
    }

    private sealed class RecordingMetrics : IDisposable
    {
        private readonly MeterListener listener = new();
        private readonly ConcurrentDictionary<string, ConcurrentBag<long>> measurements =
            new(StringComparer.Ordinal);
        private readonly ConcurrentBag<string> tagValues = [];

        public RecordingMetrics()
        {
            this.listener.InstrumentPublished = (instrument, listener) =>
            {
                if (string.Equals(instrument.Meter.Name, Metrics.MeterName, StringComparison.Ordinal) &&
                    instrument.Name.StartsWith("blobstorage_ef_", StringComparison.Ordinal))
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            this.listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            {
                this.measurements.GetOrAdd(instrument.Name, _ => []).Add(value);
                foreach (var tag in tags)
                {
                    this.tagValues.Add(tag.Value?.ToString() ?? string.Empty);
                }
            });
            this.listener.Start();
        }

        public IReadOnlyCollection<string> TagValues => this.tagValues.ToArray();

        public long Sum(string name) =>
            this.measurements.TryGetValue(name, out var values) ? values.Sum() : 0;

        public int Count(string name) =>
            this.measurements.TryGetValue(name, out var values) ? values.Count : 0;

        public void Dispose() => this.listener.Dispose();
    }
}
