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

    [Fact]
    public async Task DownloadManyAsync_WithSmallBlobs_ReadsAllContentWithOneQuery()
    {
        // Arrange
        var sut = this.CreateProvider(new BlobStoreOptions { ChunkSize = 1024 });
        var keys = await this.UploadManyAsync(sut, 5);
        this.counter.Reset();

        // Act
        var result = await sut.DownloadManyAsync(keys);

        // Assert
        this.counter.Count.ShouldBe(1);
        result.Count.ShouldBe(5);
        foreach (var key in keys)
        {
            result[key].IsSuccess.ShouldBeTrue();
            (await ReadAllAsync(result[key])).ShouldBe($"content-{key.Name}");
        }
    }

    [Fact]
    public async Task DownloadManyAsync_WithEmptyBlob_ReturnsEmptyContent()
    {
        // Arrange
        var sut = this.CreateProvider(new BlobStoreOptions { ChunkSize = 1024 });
        var key = new BlobKey("reports", "empty.bin");
        await sut.UploadAsync(CreateUpload(string.Empty, key));

        // Act
        var result = await sut.DownloadManyAsync([key]);

        // Assert
        result[key].IsSuccess.ShouldBeTrue();
        (await ReadAllAsync(result[key])).ShouldBeEmpty();
    }

    [Fact]
    public async Task DownloadManyAsync_WithMissingBlob_ReturnsNotFoundForThatKeyOnly()
    {
        // Arrange
        var sut = this.CreateProvider(new BlobStoreOptions { ChunkSize = 1024 });
        var keys = await this.UploadManyAsync(sut, 2);
        var missing = new BlobKey("reports", "missing.bin");

        // Act
        var result = await sut.DownloadManyAsync([.. keys, missing]);

        // Assert
        result[keys[0]].IsSuccess.ShouldBeTrue();
        result[keys[1]].IsSuccess.ShouldBeTrue();
        result[missing].IsFailure.ShouldBeTrue();
        result[missing].HasError<BlobStoreNotFoundError>().ShouldBeTrue();
        (await ReadAllAsync(result[keys[0]])).ShouldBe($"content-{keys[0].Name}");
    }

    [Fact]
    public async Task DownloadManyAsync_WithBlobLargerThanChunkSize_LeavesItToTheSingleRead()
    {
        // Arrange
        var sut = this.CreateProvider(new BlobStoreOptions { ChunkSize = 8 });
        var small = new BlobKey("reports", "small.bin");
        var large = new BlobKey("reports", "large.bin");
        await sut.UploadAsync(CreateUpload("tiny", small));
        await sut.UploadAsync(CreateUpload("larger than one chunk", large));

        // Act
        var result = await sut.DownloadManyAsync([small, large]);

        // Assert
        result.ContainsKey(small).ShouldBeTrue();
        result.ContainsKey(large).ShouldBeFalse();
        (await ReadAllAsync(result[small])).ShouldBe("tiny");
    }

    [Fact]
    public async Task DownloadManyAsync_WithSeveralContainers_QueriesOncePerContainer()
    {
        // Arrange
        var sut = this.CreateProvider(new BlobStoreOptions { ChunkSize = 1024 });
        var first = new BlobKey("reports", "a.bin");
        var second = new BlobKey("exports", "b.bin");
        await sut.UploadAsync(CreateUpload("a", first));
        await sut.UploadAsync(CreateUpload("b", second));
        this.counter.Reset();

        // Act
        var result = await sut.DownloadManyAsync([first, second]);

        // Assert
        this.counter.Count.ShouldBe(2);
        (await ReadAllAsync(result[first])).ShouldBe("a");
        (await ReadAllAsync(result[second])).ShouldBe("b");
    }

    [Fact]
    public async Task DownloadBytesManyAsync_WithBatchSize_ReadsKeysInBatches()
    {
        // Arrange
        var options = new BlobStoreOptions { ChunkSize = 1024, DownloadBatchSize = 2 };
        var provider = this.CreateProvider(options);
        var keys = await this.UploadManyAsync(provider, 5);
        var sut = new BlobStoreClient(EntityFrameworkBlobStoreProvider<RelationalBlobDbContext>.ProviderName, provider, options);
        this.counter.Reset();

        // Act
        var result = await sut.DownloadBytesManyAsync(keys, new BlobBytesDownloadManyOptions { MaxDegreeOfParallelism = 3 });

        // Assert
        result.IsSuccess.ShouldBeTrue();
        this.counter.Count.ShouldBe(3);
        result.Value.Select(item => item.Key).ShouldBe(keys);
        result.Value.ShouldAllBe(item => item.Result.IsSuccess);
        result.Value.Select(item => Encoding.UTF8.GetString(item.Result.Value.Bytes))
            .ShouldBe(keys.Select(key => $"content-{key.Name}"));
    }

    [Fact]
    public async Task DownloadBytesManyAsync_WithMixedSizesAndMissingKey_ReturnsEachOutcome()
    {
        // Arrange
        var options = new BlobStoreOptions { ChunkSize = 8 };
        var provider = this.CreateProvider(options);
        var small = new BlobKey("reports", "small.bin");
        var large = new BlobKey("reports", "large.bin");
        var missing = new BlobKey("reports", "missing.bin");
        await provider.UploadAsync(CreateUpload("tiny", small));
        await provider.UploadAsync(CreateUpload("larger than one chunk", large));
        var sut = new BlobStoreClient(EntityFrameworkBlobStoreProvider<RelationalBlobDbContext>.ProviderName, provider, options);

        // Act
        var result = await sut.DownloadBytesManyAsync([small, large, missing]);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Encoding.UTF8.GetString(result.Value[0].Result.Value.Bytes).ShouldBe("tiny");
        Encoding.UTF8.GetString(result.Value[1].Result.Value.Bytes).ShouldBe("larger than one chunk");
        result.Value[2].Result.HasError<BlobStoreNotFoundError>().ShouldBeTrue();
    }

    [Fact]
    public async Task DownloadBytesManyAsync_WithDuplicateKey_ReturnsContentForEveryOccurrence()
    {
        // Arrange
        var options = new BlobStoreOptions { ChunkSize = 1024 };
        var provider = this.CreateProvider(options);
        var keys = await this.UploadManyAsync(provider, 1);
        var sut = new BlobStoreClient(EntityFrameworkBlobStoreProvider<RelationalBlobDbContext>.ProviderName, provider, options);

        // Act
        var result = await sut.DownloadBytesManyAsync([keys[0], keys[0]]);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldAllBe(item => item.Result.IsSuccess);
        result.Value.Select(item => Encoding.UTF8.GetString(item.Result.Value.Bytes))
            .ShouldBe([$"content-{keys[0].Name}", $"content-{keys[0].Name}"]);
    }

    [Fact]
    public async Task UploadAsync_WithSeveralBlobs_CreatesTimeOrderedRowIds()
    {
        // Arrange
        var sut = this.CreateProvider(new BlobStoreOptions { ChunkSize = 1024 });
        var keys = Enumerable.Range(0, 10).Select(i => new BlobKey("reports", $"ordered-{i}.bin")).ToArray();

        // Act
        foreach (var key in keys)
        {
            (await sut.UploadAsync(CreateUpload("x", key))).IsSuccess.ShouldBeTrue();
            await Task.Delay(3); // the ids carry a millisecond timestamp
        }

        // Assert
        using var scope = this.services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RelationalBlobDbContext>();
        var ids = await dbContext.StorageBlobs.AsNoTracking()
            .Where(b => b.Name.StartsWith("ordered-"))
            .OrderBy(b => b.Name)
            .Select(b => b.Id)
            .ToListAsync();
        ids.Count.ShouldBe(10);
        ids.ShouldBe(ids.OrderBy(id => id, StringComparer.Ordinal).ToList());
        ids.ShouldAllBe(id => id.Length == 32 && id[12] == '7');
    }

    [Fact]
    public void CreateId_ReturnsVersion7GuidInNFormat()
    {
        // Arrange & Act
        var id = StorageBlob.CreateId();

        // Assert
        id.Length.ShouldBe(32);
        Guid.TryParseExact(id, "N", out var guid).ShouldBeTrue();
        guid.Version.ShouldBe(7);
        new StorageBlob().Id.Length.ShouldBe(32);
    }

    private async Task<BlobKey[]> UploadManyAsync(
        EntityFrameworkBlobStoreProvider<RelationalBlobDbContext> provider,
        int count)
    {
        var keys = Enumerable.Range(0, count).Select(i => new BlobKey("reports", $"item-{i}.bin")).ToArray();
        foreach (var key in keys)
        {
            (await provider.UploadAsync(CreateUpload($"content-{key.Name}", key))).IsSuccess.ShouldBeTrue();
        }

        return keys;
    }

    private static async Task<string> ReadAllAsync(Result<BlobDownload> result)
    {
        await using var download = result.Value;
        using var target = new MemoryStream();
        await download.Content.CopyToAsync(target);

        return Encoding.UTF8.GetString(target.ToArray());
    }

    private static BlobUpload CreateUpload(string content, BlobKey key = null) =>
        new()
        {
            Key = key ?? Key,
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
