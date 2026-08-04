// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using Application.Storage;
using Application.UnitTests.Storage;
using Infrastructure.EntityFramework.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

public abstract class EntityFrameworkBlobStoreProviderContractTestsBase(ITestOutputHelper output, TestEnvironmentFixture fixture)
    : BlobStoreProviderContractTests
{
    private ServiceProvider serviceProvider;

    protected override string ProviderName => EntityFrameworkBlobStoreProvider<StubDbContext>.ProviderName;

    protected ITestOutputHelper Output { get; } = output;

    protected TestEnvironmentFixture Fixture { get; } = fixture.WithOutput(output);

    protected abstract StubDbContext CreateDbContext(bool forceNew = false);

    [Fact]
    public async Task UploadAsync_WithSeveralFlushGroups_CommitsCompleteContent()
    {
        // Arrange
        var client = this.CreateClient(new BlobStoreOptions
        {
            ChunkSize = 1024,
            ChunkFlushCount = 4,
            MaxPendingChunkBytes = 4096
        });
        var key = CreateKey("high-volume-grouped.bin");
        var content = string.Concat(Enumerable.Repeat("0123456789abcdef", 2048));

        // Act
        var upload = await client.UploadAsync(CreateUpload(key, content));
        var download = await client.DownloadAsync(key);

        // Assert
        upload.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, upload.Errors.Select(e => e.Message)));
        download.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, download.Errors.Select(e => e.Message)));
        await using var value = download.Value;
        (await ReadAllTextAsync(value.Content)).ShouldBe(content);
    }

    [Fact]
    public async Task UploadAsync_WhenGroupedOverwriteHashFails_PreservesCommittedContent()
    {
        // Arrange
        var client = this.CreateClient(new BlobStoreOptions
        {
            ChunkSize = 2,
            ChunkFlushCount = 2,
            MaxPendingChunkBytes = 4
        });
        var key = CreateKey("high-volume-rollback.bin");
        (await client.UploadAsync(CreateUpload(key, "stable")))
            .IsSuccess.ShouldBeTrue();

        // Act
        var failed = await client.UploadAsync(new BlobUpload
        {
            Key = key,
            Content = new MemoryStream(Encoding.UTF8.GetBytes("replacement-content")),
            ExpectedContentHash = $"{BlobContentHash.Prefix}{new string('0', 64)}"
        });
        var download = await client.DownloadAsync(key);

        // Assert
        failed.HasError<BlobStoreIntegrityError>().ShouldBeTrue();
        download.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, download.Errors.Select(e => e.Message)));
        await using var value = download.Value;
        (await ReadAllTextAsync(value.Content)).ShouldBe("stable");
    }

    [Fact]
    public async Task UploadAsync_WithDifferentKeysConcurrently_RemainsIndependent()
    {
        // Arrange
        await using var providerProbe = this.CreateDbContext(forceNew: true);
        if (providerProbe.Database.ProviderName?.Contains(
            "Sqlite",
            StringComparison.OrdinalIgnoreCase) == true)
        {
            // SQLite intentionally permits one writer; SQL Server and PostgreSQL exercise
            // independent concurrent provider operations through this shared contract.
            return;
        }

        var client = this.CreateClient(new BlobStoreOptions
        {
            ChunkSize = 2,
            ChunkFlushCount = 2,
            MaxPendingChunkBytes = 4
        });
        var firstKey = CreateKey("concurrent-first.bin");
        var secondKey = CreateKey("concurrent-second.bin");

        // Act
        var results = await Task.WhenAll(
            client.UploadAsync(CreateUpload(firstKey, "first-content")),
            client.UploadAsync(CreateUpload(secondKey, "second-content")));

        // Assert
        results.ShouldAllBe(result => result.IsSuccess);
        var first = await client.DownloadAsync(firstKey);
        var second = await client.DownloadAsync(secondKey);
        await using var firstValue = first.Value;
        await using var secondValue = second.Value;
        (await ReadAllTextAsync(firstValue.Content)).ShouldBe("first-content");
        (await ReadAllTextAsync(secondValue.Content)).ShouldBe("second-content");
    }

    [Fact]
    public async Task UploadAsync_DuringGroupedOverwrite_DoesNotExposePartialReplacement()
    {
        // Arrange
        await using var providerProbe = this.CreateDbContext(forceNew: true);
        if (providerProbe.Database.ProviderName?.Contains(
            "Sqlite",
            StringComparison.OrdinalIgnoreCase) == true)
        {
            return;
        }

        var client = this.CreateClient(new BlobStoreOptions
        {
            ChunkSize = 2,
            ChunkFlushCount = 2,
            MaxPendingChunkBytes = 1024
        });
        var key = CreateKey("transaction-visibility.bin");
        (await client.UploadAsync(CreateUpload(key, "stable")))
            .IsSuccess.ShouldBeTrue();
        var stream = new GatedReadStream(
            Encoding.UTF8.GetBytes("replacement-content"),
            pauseAfterBytes: 4);
        var uploadTask = client.UploadAsync(new BlobUpload
        {
            Key = key,
            Content = stream
        });

        try
        {
            await stream.Paused.WaitAsync(TimeSpan.FromSeconds(30));
            await using var observer = this.CreateDbContext(forceNew: true);
            var blobId = await observer.StorageBlobs
                .AsNoTracking()
                .Where(blob => blob.Container == key.Container && blob.Name == key.Name)
                .Select(blob => blob.Id)
                .SingleAsync();
            var visibleChunks = await observer.StorageBlobChunks
                .AsNoTracking()
                .Where(chunk => chunk.BlobId == blobId)
                .OrderBy(chunk => chunk.Index)
                .Select(chunk => chunk.Content)
                .ToArrayAsync();

            Encoding.UTF8.GetString(visibleChunks.SelectMany(bytes => bytes).ToArray())
                .ShouldBe("stable");
        }
        finally
        {
            stream.Release();
        }

        (await uploadTask).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task UploadAsync_WithSameNewKeyConcurrently_PreservesConflictSemantics()
    {
        // Arrange
        await using var providerProbe = this.CreateDbContext(forceNew: true);
        if (providerProbe.Database.ProviderName?.Contains(
            "Sqlite",
            StringComparison.OrdinalIgnoreCase) == true)
        {
            return;
        }

        var client = this.CreateClient(new BlobStoreOptions
        {
            ChunkSize = 2,
            ChunkFlushCount = 2,
            MaxPendingChunkBytes = 1024
        });
        var key = CreateKey("concurrent-same-key.bin");
        static BlobUpload Upload(BlobKey key, string content) => new()
        {
            Key = key,
            Content = new MemoryStream(Encoding.UTF8.GetBytes(content)),
            OverwriteMode = BlobOverwriteMode.FailIfExists
        };

        // Act
        var results = await Task.WhenAll(
            client.UploadAsync(Upload(key, "first")),
            client.UploadAsync(Upload(key, "second")));

        // Assert
        results.Count(result => result.IsSuccess).ShouldBe(1);
        results.Count(result => result.HasError<BlobStoreConflictError>()).ShouldBe(1);
    }

    protected override IBlobStoreProvider CreateProvider(BlobStoreOptions options = null)
    {
        this.ResetStore();

        this.serviceProvider?.Dispose();
        var services = new ServiceCollection();
        services.AddScoped(_ => this.CreateDbContext(forceNew: true));
        this.serviceProvider = services.BuildServiceProvider(validateScopes: true);

        return new EntityFrameworkBlobStoreProvider<StubDbContext>(
            this.serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            options);
    }

    private void ResetStore()
    {
        using var dbContext = this.CreateDbContext(forceNew: true);
        dbContext.Database.EnsureCreated();

        var chunks = dbContext.StorageBlobChunks.ToList();
        if (chunks.Count != 0)
        {
            dbContext.StorageBlobChunks.RemoveRange(chunks);
        }

        var blobs = dbContext.StorageBlobs.ToList();
        if (blobs.Count != 0)
        {
            dbContext.StorageBlobs.RemoveRange(blobs);
        }

        dbContext.SaveChanges();
    }

    private sealed class GatedReadStream(byte[] content, int pauseAfterBytes) : Stream
    {
        private readonly MemoryStream inner = new(content);
        private readonly TaskCompletionSource paused =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource released =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int totalRead;

        public Task Paused => this.paused.Task;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public void Release() => this.released.TrySetResult();

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (this.totalRead >= pauseAfterBytes && !this.released.Task.IsCompleted)
            {
                this.paused.TrySetResult();
                await this.released.Task.WaitAsync(cancellationToken);
            }

            var read = await this.inner.ReadAsync(buffer, cancellationToken);
            this.totalRead += read;
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }
}
