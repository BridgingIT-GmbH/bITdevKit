// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Storage;

using System.Data.Common;
using System.Text;
using Application.Storage;
using Infrastructure.EntityFramework;
using Infrastructure.EntityFramework.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

[UnitTest("Infrastructure")]
public sealed class EntityFrameworkBlobStoreProviderRelationalReadTests : IDisposable
{
    private static readonly BlobKey Key = new("reports", "file.bin");

    private readonly SqliteConnection connection = new("DataSource=:memory:");
    private readonly CommandCounter counter = new();
    private readonly ServiceProvider services;

    public EntityFrameworkBlobStoreProviderRelationalReadTests()
    {
        this.connection.Open();
        var collection = new ServiceCollection();
        collection.AddDbContext<RelationalBlobDbContext>(options => options
            .UseSqlite(this.connection)
            .AddInterceptors(this.counter));
        this.services = collection.BuildServiceProvider();

        using var scope = this.services.CreateScope();
        scope.ServiceProvider.GetRequiredService<RelationalBlobDbContext>().Database.EnsureCreated();
    }

    public void Dispose()
    {
        this.services.Dispose();
        this.connection.Dispose();
    }

    [Fact]
    public async Task DownloadAsync_WithSmallBlob_ReadsContentWithoutExtraQueries()
    {
        // Arrange
        var sut = this.CreateProvider(new BlobStoreOptions { ChunkSize = 1024 });
        await sut.UploadAsync(CreateUpload("hello world"));
        this.counter.Reset();

        // Act
        var result = await sut.DownloadAsync(Key);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Info.Length.ShouldBe(11);
        (await ReadAllAsync(result)).ShouldBe("hello world");
        this.counter.Count.ShouldBe(1);
    }

    [Fact]
    public async Task DownloadAsync_WithSmallBlob_ReleasesContextBeforeContentIsRead()
    {
        // Arrange
        var sut = this.CreateProvider(new BlobStoreOptions { ChunkSize = 1024 });
        await sut.UploadAsync(CreateUpload("hello world"));
        this.counter.Reset();

        // Act
        var result = await sut.DownloadAsync(Key);
        this.counter.Reset();
        var content = await ReadAllAsync(result);

        // Assert
        content.ShouldBe("hello world");
        this.counter.Count.ShouldBe(0);
    }

    [Fact]
    public async Task DownloadAsync_WithEmptyBlob_ReturnsEmptyContent()
    {
        // Arrange
        var sut = this.CreateProvider(new BlobStoreOptions { ChunkSize = 1024 });
        await sut.UploadAsync(CreateUpload(string.Empty));
        this.counter.Reset();

        // Act
        var result = await sut.DownloadAsync(Key);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await ReadAllAsync(result)).ShouldBeEmpty();
        this.counter.Count.ShouldBe(1);
    }

    [Fact]
    public async Task DownloadAsync_WithBlobLargerThanChunkSize_StreamsOneQueryPerChunk()
    {
        // Arrange
        var sut = this.CreateProvider(new BlobStoreOptions { ChunkSize = 3 });
        await sut.UploadAsync(CreateUpload("abcdefghi"));
        this.counter.Reset();

        // Act
        var result = await sut.DownloadAsync(Key);
        var content = await ReadAllAsync(result);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        content.ShouldBe("abcdefghi");
        this.counter.Count.ShouldBe(1 + 3);
    }

    [Fact]
    public async Task DownloadAsync_WhenChunkSizeWasSmallerAtWriteTime_ReadsAllChunks()
    {
        // Arrange
        var writer = this.CreateProvider(new BlobStoreOptions { ChunkSize = 3 });
        await writer.UploadAsync(CreateUpload("abcdefgh"));
        var sut = this.CreateProvider(new BlobStoreOptions { ChunkSize = 1024 });

        // Act
        var result = await sut.DownloadAsync(Key);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await ReadAllAsync(result)).ShouldBe("abcdefgh");
    }

    [Fact]
    public async Task DownloadAsync_WhenChunkIsMissing_ThrowsInvalidDataException()
    {
        // Arrange
        var sut = this.CreateProvider(new BlobStoreOptions { ChunkSize = 3 });
        await sut.UploadAsync(CreateUpload("abcdefghi"));
        using (var scope = this.services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<RelationalBlobDbContext>();
            await context.StorageBlobChunks.Where(e => e.Index == 1).ExecuteDeleteAsync();
        }

        // Act
        var result = await sut.DownloadAsync(Key);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await Should.ThrowAsync<InvalidDataException>(async () => await ReadAllAsync(result));
    }

    [Fact]
    public async Task DownloadAsync_WithMissingBlob_ReturnsNotFound()
    {
        // Arrange
        var sut = this.CreateProvider(new BlobStoreOptions());

        // Act
        var result = await sut.DownloadAsync(new BlobKey("reports", "missing.bin"));

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<BlobStoreNotFoundError>().ShouldBeTrue(string.Join(" | ", result.Errors.Select(e => e.Message)));
    }

    private static async Task<string> ReadAllAsync(Result<BlobDownload> result)
    {
        await using var download = result.Value;
        using var target = new MemoryStream();
        await download.Content.CopyToAsync(target);

        return Encoding.UTF8.GetString(target.ToArray());
    }

    private static BlobUpload CreateUpload(string content) =>
        new()
        {
            Key = Key,
            Content = new MemoryStream(Encoding.UTF8.GetBytes(content)),
            ContentType = ContentType.TXT
        };

    private EntityFrameworkBlobStoreProvider<RelationalBlobDbContext> CreateProvider(BlobStoreOptions options) =>
        new(this.services.GetRequiredService<IServiceScopeFactory>(), options);

    private sealed class RelationalBlobDbContext(DbContextOptions<RelationalBlobDbContext> options)
        : DbContext(options), IBlobStoreContext
    {
        public DbSet<StorageBlob> StorageBlobs { get; set; }

        public DbSet<StorageBlobChunk> StorageBlobChunks { get; set; }
    }

    private sealed class CommandCounter : DbCommandInterceptor
    {
        private int count;

        public int Count => this.count;

        public void Reset() => Interlocked.Exchange(ref this.count, 0);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref this.count);

            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
