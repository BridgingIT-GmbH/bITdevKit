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
using Microsoft.Extensions.Time.Testing;

[UnitTest("Infrastructure")]
public sealed class EntityFrameworkBlobStoreProviderRelationalReadTests : IDisposable
{
    private static readonly BlobKey Key = new("reports", "file.bin");

    private readonly SqliteConnection connection = new("DataSource=:memory:");
    private readonly CommandCounter counter = new();
    private readonly SaveObserver saves = new();
    private readonly ServiceProvider services;

    public EntityFrameworkBlobStoreProviderRelationalReadTests()
    {
        this.connection.Open();
        var collection = new ServiceCollection();
        collection.AddDbContext<RelationalBlobDbContext>(options => options
            .UseSqlite(this.connection)
            .AddInterceptors(this.counter, this.saves));
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
    public async Task SweepExpiredAsync_WhenOneDeleteConflicts_CommitsTheOtherBlobs()
    {
        var sut = this.CreateProvider(new BlobStoreOptions { ChunkSize = 2 });
        var cutoff = DateTimeOffset.UtcNow;
        var keys = new BlobKey[] { new("reports", "first"), new("reports", "raced"), new("reports", "last") };
        for (var index = 0; index < keys.Length; index++)
        {
            (await sut.UploadAsync(new()
            {
                Key = keys[index],
                Content = new MemoryStream([1, 2, 3]),
                ExpiresAt = cutoff.AddMinutes(-3 + index)
            })).IsSuccess.ShouldBeTrue();
        }

        var transactions = new List<Guid>();
        this.saves.BeforeSave = dbContext =>
        {
            var deleted = dbContext.ChangeTracker.Entries<StorageBlob>().SingleOrDefault(entry => entry.State == EntityState.Deleted);
            if (deleted is null)
            {
                return;
            }

            transactions.Add(dbContext.Database.CurrentTransaction.TransactionId);
            if (deleted.Entity.Name == keys[1].Name)
            {
                throw new DbUpdateConcurrencyException("Simulated concurrent blob update.");
            }
        };

        var result = await sut.SweepExpiredAsync(new() { ExpiresOnOrBefore = cutoff, BatchSize = 10, MaxBatches = 1 });

        result.IsSuccess.ShouldBeTrue();
        result.Value.DeletedCount.ShouldBe(2);
        result.Value.SkippedCount.ShouldBe(1);
        result.Value.DeletedKeys.ShouldBe([keys[0], keys[2]]);
        transactions.Distinct().Count().ShouldBe(3);
        using var scope = this.services.CreateScope();
        var stored = scope.ServiceProvider.GetRequiredService<RelationalBlobDbContext>();
        (await stored.StorageBlobs.SingleAsync()).Name.ShouldBe(keys[1].Name);
        (await stored.StorageBlobChunks.CountAsync()).ShouldBe(2);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task UploadAsync_WhenInsertRaces_ClassifiesTheFailureByOverwriteMode(bool concurrency, bool createOnly)
    {
        var sut = this.CreateProvider(new BlobStoreOptions());
        this.saves.BeforeSave = dbContext =>
        {
            if (dbContext.ChangeTracker.Entries<StorageBlob>().Any(entry => entry.State == EntityState.Added))
            {
                throw concurrency
                    ? new DbUpdateConcurrencyException("Simulated concurrent blob update.")
                    : new DbUpdateException("UNIQUE constraint failed: __Storage_Blobs.ContainerHash, __Storage_Blobs.NameHash");
            }
        };

        var result = await sut.UploadAsync(new()
        {
            Key = Key,
            Content = new MemoryStream([1]),
            OverwriteMode = createOnly ? BlobOverwriteMode.FailIfExists : BlobOverwriteMode.Overwrite
        });

        result.HasError<BlobStoreConflictError>().ShouldBe(createOnly && !concurrency);
        result.HasError<BlobStoreLeaseError>().ShouldBe(!createOnly || concurrency);
        using var scope = this.services.CreateScope();
        var stored = scope.ServiceProvider.GetRequiredService<RelationalBlobDbContext>();
        (await stored.StorageBlobs.AnyAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task EntityFrameworkClient_WithRegisteredClock_TimestampsMutationsAndHidesExpiredBlob()
    {
        var start = new DateTimeOffset(2026, 7, 16, 10, 0, 0, TimeSpan.Zero);
        var clock = new FakeTimeProvider(start);
        var collection = new ServiceCollection();
        collection.AddSingleton<TimeProvider>(clock);
        collection.AddDbContext<RelationalBlobDbContext>(options => options.UseSqlite(this.connection));
        collection.AddBlobStorage().WithEntityFrameworkClient<RelationalBlobDbContext>("reports");
        using var services = collection.BuildServiceProvider();
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredKeyedService<IBlobStoreProvider>("reports");
        var sut = scope.ServiceProvider.GetRequiredService<IBlobStoreClientFactory>().CreateClient("reports");
        var expires = start.AddHours(1);

        var upload = await sut.UploadAsync(new() { Key = Key, Content = new MemoryStream([1]), ExpiresAt = expires });
        upload.IsSuccess.ShouldBeTrue();
        upload.Value.CreatedAt.ShouldBe(start);
        upload.Value.LastModifiedAt.ShouldBe(start);

        clock.Advance(TimeSpan.FromMinutes(1));
        var properties = await sut.UpdatePropertiesAsync(new() { Key = Key, ExpiresAt = expires });
        properties.IsSuccess.ShouldBeTrue();
        properties.Value.CreatedAt.ShouldBe(start);
        properties.Value.LastModifiedAt.ShouldBe(clock.GetUtcNow());

        clock.Advance(TimeSpan.FromMinutes(1));
        var dimensions = BlobDimensions.Create(new Dictionary<string, object> { ["year"] = 2026 }).Value;
        var updated = await sut.UpdateDimensionsAsync(new() { Key = Key, Dimensions = dimensions });
        updated.IsSuccess.ShouldBeTrue();
        updated.Value.CreatedAt.ShouldBe(start);
        updated.Value.LastModifiedAt.ShouldBe(clock.GetUtcNow());

        clock.SetUtcNow(expires.AddTicks(-1));
        (await sut.ExistsAsync(Key)).Value.ShouldBeTrue();
        clock.Advance(TimeSpan.FromTicks(1));
        (await sut.GetPropertiesAsync(Key)).HasError<BlobStoreNotFoundError>().ShouldBeTrue();
        (await sut.ExistsAsync(Key)).Value.ShouldBeFalse();

        var retention = Assert.IsAssignableFrom<IBlobStoreRetentionProvider>(provider);
        var swept = await retention.SweepExpiredAsync(new() { ExpiresOnOrBefore = expires, BatchSize = 10, MaxBatches = 1 });
        swept.IsSuccess.ShouldBeTrue();
        swept.Value.DeletedKeys.ShouldBe([Key]);
        swept.Value.CompletedAt.ShouldBe(expires);
    }

    [Fact]
    public async Task ExpiredRows_InSqlite_AreHiddenUntilReplacementRemovesDependents()
    {
        var options = new BlobStoreOptions { ChunkSize = 4, AllowFullScans = true };
        var sut = this.CreateProvider(options);
        var expired = new BlobKey("reports", "a-expired");
        var expiredInline = new BlobKey("reports", "b-expired");
        var live = new BlobKey("reports", "c-live");
        var past = DateTimeOffset.UtcNow.AddHours(-1);
        var dimensions = BlobDimensions.Create(new Dictionary<string, object> { ["year"] = 2026 }).Value;
        (await sut.UploadAsync(new() { Key = expired, Content = new MemoryStream([1, 2, 3, 4, 5]), ExpiresAt = past, Dimensions = dimensions }))
            .IsSuccess.ShouldBeTrue();
        (await sut.UploadAsync(new() { Key = expiredInline, Content = new MemoryStream([1]), ExpiresAt = past }))
            .IsSuccess.ShouldBeTrue();
        (await sut.UploadAsync(new() { Key = live, Content = new MemoryStream([2, 3, 4, 5, 6]) }))
            .IsSuccess.ShouldBeTrue();

        var direct = await sut.DownloadManyAsync([expired, expiredInline, live]);
        direct[expired].HasError<BlobStoreNotFoundError>().ShouldBeTrue();
        direct[expiredInline].HasError<BlobStoreNotFoundError>().ShouldBeTrue();
        direct.ContainsKey(live).ShouldBeFalse("the public client reads live large blobs through the single-read fallback");
        var client = new BlobStoreClient("ef", sut, options);
        var batch = await client.DownloadBytesManyAsync([expired, expiredInline, live]);
        batch.Value[2].Result.Value.Bytes.ShouldBe([2, 3, 4, 5, 6]);
        (await client.UpdateDimensionsAsync(new() { Key = expired, Dimensions = BlobDimensions.Empty }))
            .HasError<BlobStoreNotFoundError>().ShouldBeTrue();
        var page = await client.ListPageAsync(
            new BlobFilterScope("reports", AllowFullScan: true), new FilterModel { PageSize = 1 }, null);
        page.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, page.Errors.Select(error => error.Message)));
        page.Value.Items.Select(item => item.Key).ShouldBe([live]);

        using var scope = this.services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RelationalBlobDbContext>();
        var oldId = await context.StorageBlobs.Where(blob => blob.Name == expired.Name).Select(blob => blob.Id).SingleAsync();
        (await context.StorageBlobChunks.CountAsync(chunk => chunk.BlobId == oldId)).ShouldBe(2);
        (await context.Set<StorageBlobDimension>().CountAsync(value => value.BlobId == oldId)).ShouldBe(1);
        (await sut.UploadAsync(new()
        {
            Key = expired,
            Content = new MemoryStream([7]),
            OverwriteMode = BlobOverwriteMode.FailIfExists
        })).IsSuccess.ShouldBeTrue();
        (await context.StorageBlobs.AnyAsync(blob => blob.Id == oldId)).ShouldBeFalse();
        (await context.StorageBlobChunks.AnyAsync(chunk => chunk.BlobId == oldId)).ShouldBeFalse();
        (await context.Set<StorageBlobDimension>().AnyAsync(value => value.BlobId == oldId)).ShouldBeFalse();

        var sweep = await sut.SweepExpiredAsync(new() { ExpiresOnOrBefore = past, BatchSize = 10, MaxBatches = 1 });
        sweep.IsSuccess.ShouldBeTrue();
        sweep.Value.DeletedKeys.ShouldBe([expiredInline]);
    }

    [Fact]
    public async Task Properties_WithEveryScalarAndPrivateTransforms_PersistIndependently()
    {
        var sut = this.CreateProvider(new());
        var properties = Application.UnitTests.Storage.StoragePropertySamples.Create();
        var uploaded = await sut.UploadAsync(new() { Key = Key, Content = new MemoryStream([1]), Properties = properties, TransformMetadata = new() { ["system"] = "value" } });
        uploaded.IsSuccess.ShouldBeTrue();
        var read = await sut.GetPropertiesAsync(Key);
        Application.UnitTests.Storage.StoragePropertySamples.AssertEqual(read.Value.Properties, properties);
        read.Value.TransformMetadata.Get<string>("system").ShouldBe("value");
        (await sut.UpdatePropertiesAsync(new() { Key = Key, Properties = new() })).Value.TransformMetadata.Get<string>("system").ShouldBe("value");
        (await sut.GetPropertiesAsync(Key)).Value.Properties.Count.ShouldBe(0);
        (await sut.UploadAsync(new() { Key = Key, Content = new MemoryStream([2]) })).Value.TransformMetadata.Count.ShouldBe(0);
    }

    [Fact]
    public async Task GetPropertiesAsync_AfterDatabaseRoundTrip_PreservesStringOwnershipProperty()
    {
        var sut = this.CreateProvider(new BlobStoreOptions());
        var owner = Guid.NewGuid().ToString("N");
        var uploaded = await sut.UploadAsync(new()
        {
            Key = Key,
            Content = new MemoryStream([1, 2, 3]),
            Properties = new() { ["bdk_diagnostic_owner"] = owner }
        });
        uploaded.IsSuccess.ShouldBeTrue();
        uploaded.Value.Properties.Get<string>("bdk_diagnostic_owner").ShouldBe(owner);

        var result = await sut.GetPropertiesAsync(Key);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Properties.Get<string>("bdk_diagnostic_owner").ShouldBe(owner);

        var page = await sut.ListPageAsync(new() { Container = Key.Container, Prefix = Key.Name });
        page.IsSuccess.ShouldBeTrue();
        page.Value.Items.Single().Properties.Get<string>("bdk_diagnostic_owner").ShouldBe(owner);

        var download = await sut.DownloadAsync(Key);
        download.IsSuccess.ShouldBeTrue();
        await using var content = download.Value.Content;
        download.Value.Info.Properties.Get<string>("bdk_diagnostic_owner").ShouldBe(owner);
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
        ids.ShouldBe(ids.OrderBy(id => id).ToList());
        ids.ShouldAllBe(id => id.Version == 7);
    }

    [Fact]
    public void CreateId_ReturnsVersion7Guid()
    {
        // Arrange & Act
        var id = StorageBlob.CreateId();

        // Assert
        id.Version.ShouldBe(7);
        new StorageBlob().Id.Version.ShouldBe(7);
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

    private static async Task<string> ReadAllAsync(Result<StoredBlobDownload> result)
    {
        await using var download = result.Value;
        using var target = new MemoryStream();
        await download.Content.CopyToAsync(target);

        return Encoding.UTF8.GetString(target.ToArray());
    }

    private static StoredBlobUpload CreateUpload(string content, BlobKey key = null) =>
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

    private sealed class SaveObserver : SaveChangesInterceptor
    {
        public Action<DbContext> BeforeSave { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            this.BeforeSave?.Invoke(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
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
