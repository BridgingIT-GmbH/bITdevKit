// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Storage;

using Application.Storage;
using Infrastructure.EntityFramework;
using Infrastructure.EntityFramework.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Data.Common;

public sealed partial class StorageDimensionsMutationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BlobCommit_LostAcknowledgement_IsNotRetriedAndInvalidatesCachedMetadata(bool replaceContent)
    {
        var fault = new LostDimensionCommit();
        await using var services = new ServiceCollection().AddDbContext<DimensionsContext>(options => options.UseSqlite(this.connection).AddInterceptors(fault)).BuildServiceProvider();
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = new InMemoryCacheProvider(NullLoggerFactory.Instance, memory);
        var provider = new EntityFrameworkBlobStoreProvider<DimensionsContext>(services.GetRequiredService<IServiceScopeFactory>());
        var sut = new BlobStoreClient("ef", provider, decorate: inner => new RetryBlobStoreClientBehavior(new CacheBlobStoreClientBehavior(null, inner, cache), new() { Attempts = 3 }));
        var key = new BlobKey("archive", "commit.txt");
        var created = await sut.UploadTextAsync(key, "payload", new() { Dimensions = BlobDimensions.Create(new Dictionary<string, object> { ["year"] = 2026 }).Value });
        created.IsSuccess.ShouldBeTrue();
        (await sut.DownloadTextAsync(key)).Value.Info.Dimensions["year"].Value.ShouldBe(2026);
        fault.FailAt = fault.Commits + 1;
        var replacement = BlobDimensions.Create(new Dictionary<string, object> { ["NewName"] = 2027 }).Value;
        var failed = replaceContent
            ? await sut.UploadTextAsync(key, "new payload", new() { IfMatchETag = created.Value.ETag, Dimensions = replacement })
            : await sut.UpdateDimensionsAsync(new() { Key = key, IfMatchETag = created.Value.ETag, Dimensions = replacement });
        failed.HasError<BlobStoreIndeterminateOperationError>().ShouldBeTrue();
        fault.Commits.ShouldBe(fault.FailAt);
        var reconciled = await sut.DownloadTextAsync(key);
        reconciled.Value.Info.Dimensions["NewName"].Value.ShouldBe(2027);
        reconciled.Value.Text.ShouldBe(replaceContent ? "new payload" : "payload");
        reconciled.Value.Info.ETag.ShouldNotBe(created.Value.ETag);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DocumentCommit_LostAcknowledgement_IsNotRetriedAndInvalidatesCachedMetadata(bool replaceContent)
    {
        var fault = new LostDimensionCommit();
        await using var services = new ServiceCollection().AddDbContext<DimensionsContext>(options => options.UseSqlite(this.connection).AddInterceptors(fault)).BuildServiceProvider();
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = new InMemoryCacheProvider(NullLoggerFactory.Instance, memory);
        var provider = new EntityFrameworkDocumentStoreProvider<DimensionsContext>(services.GetRequiredService<IServiceScopeFactory>());
        var cached = new CacheDocumentStoreClientBehavior<Invoice>(null, new DocumentStoreClient<Invoice>(provider), cache);
        var sut = new RetryDocumentStoreClientBehavior<Invoice>(null, cached, new() { Attempts = 3 });
        var key = new DocumentKey("archive", "commit");
        var created = await sut.UpsertAsync(key, new() { Amount = 7 }, new() { Dimensions = DocumentDimensions.Create(new Dictionary<string, object> { ["year"] = 2026 }).Value });
        created.IsSuccess.ShouldBeTrue();
        (await sut.GetAsync(key)).Value.Dimensions["year"].Value.ShouldBe(2026);
        fault.FailAt = fault.Commits + 1;
        var replacement = DocumentDimensions.Create(new Dictionary<string, object> { ["NewName"] = 2027 }).Value;
        var failed = replaceContent
            ? await sut.UpsertAsync(key, new() { Amount = 8 }, new() { IfMatchETag = created.Value.ETag, Dimensions = replacement })
            : await sut.UpdateDimensionsAsync(new() { Key = key, IfMatchETag = created.Value.ETag, Dimensions = replacement });
        failed.HasError<DocumentStoreIndeterminateOperationError>().ShouldBeTrue();
        fault.Commits.ShouldBe(fault.FailAt);
        var reconciled = await sut.GetAsync(key);
        reconciled.Value.Dimensions["NewName"].Value.ShouldBe(2027);
        reconciled.Value.Value.Amount.ShouldBe(replaceContent ? 8 : 7);
        reconciled.Value.ETag.ShouldNotBe(created.Value.ETag);
    }

    [Fact]
    public async Task DocumentBulk_LostSecondChunkAcknowledgement_RetainsAcknowledgedPrefixWithoutReplay()
    {
        var fault = new LostDimensionCommit { FailAt = 2 };
        await using var services = new ServiceCollection().AddDbContext<DimensionsContext>(options => options.UseSqlite(this.connection).AddInterceptors(fault)).BuildServiceProvider();
        var provider = new EntityFrameworkDocumentStoreProvider<DimensionsContext>(services.GetRequiredService<IServiceScopeFactory>());
        var sut = new DocumentStoreClient<Invoice>(provider);
        var values = DocumentDimensions.Create(new Dictionary<string, object> { ["year"] = 2026 }).Value;
        var writes = Enumerable.Range(0, 501).Select(index => new DocumentWrite<Invoice>(new("bulk", index.ToString("D4")), new() { Amount = index }, new() { Dimensions = values })).ToArray();
        var failed = await sut.UpsertManyAsync(writes);
        failed.HasError<DocumentStoreIndeterminateOperationError>().ShouldBeTrue();
        failed.Value.Items.Count.ShouldBe(500);
        fault.Commits.ShouldBe(2);
        var reconciled = await sut.GetAsync(new("bulk", "0500"));
        reconciled.IsSuccess.ShouldBeTrue();
        reconciled.Value.Value.Amount.ShouldBe(500);
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DimensionsContext>();
        (await context.Set<StorageDocumentDimensionName>().CountAsync()).ShouldBe(1);
        (await context.StorageDocuments.CountAsync()).ShouldBe(501);
    }

    private sealed class LostDimensionCommit : DbTransactionInterceptor
    {
        public int Commits { get; private set; }

        public int FailAt { get; set; } = -1;

        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            this.Commits++;
            if (this.Commits == this.FailAt) throw new LostDimensionAcknowledgement();
            return Task.CompletedTask;
        }
    }

    private sealed class LostDimensionAcknowledgement : DbException;
}
