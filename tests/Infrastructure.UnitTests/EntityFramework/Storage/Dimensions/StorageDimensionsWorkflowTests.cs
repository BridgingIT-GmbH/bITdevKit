// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Storage;

using Application.Storage;
using Infrastructure.EntityFramework.Storage;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

public sealed partial class StorageDimensionsMutationTests
{
    [Fact]
    public async Task PublicQueries_FilterBeforePagingAndCount_RejectChangedContinuationFilters()
    {
        var scopes = this.services.GetRequiredService<IServiceScopeFactory>();
        var blobs = new BlobStoreClient("Entity Framework", new EntityFrameworkBlobStoreProvider<DimensionsContext>(scopes));
        var documents = new DocumentStoreClient<Invoice>(new EntityFrameworkDocumentStoreProvider<DimensionsContext>(scopes));
        for (var index = 0; index < 12; index++)
        {
            using var content = new MemoryStream([1]);
            (await blobs.UploadAsync(new() { Key = new("queries", $"q{index:D2}"), Content = content,
                Dimensions = BlobDimensions.Create(new Dictionary<string, object> { ["year"] = index % 2 }).Value })).IsSuccess.ShouldBeTrue();
            (await documents.UpsertAsync(new("queries", $"q{index:D2}"), new() { Amount = index },
                new() { Dimensions = DocumentDimensions.Create(new Dictionary<string, object> { ["year"] = index % 2 }).Value })).IsSuccess.ShouldBeTrue();
        }

        var blobFilter = BlobDimensionFilter.Where(BlobDimensionPredicate.Create("year", BlobDimensionOperator.Equal, 1).Value);
        var docFilter = DocumentDimensionFilter.Where(DocumentDimensionPredicate.Create("year", DocumentDimensionOperator.Equal, 1).Value);
        var blobKeys = new List<string>();
        string token = null;
        do
        {
            var page = await blobs.ListPageAsync(new() { Container = "queries", Take = 2, DimensionFilter = blobFilter, ContinuationToken = token });
            page.IsSuccess.ShouldBeTrue(string.Join(";", page.Errors.Select(error => error.Message)));
            blobKeys.AddRange(page.Value.Items.Select(item => item.Key.Name));
            token = page.Value.ContinuationToken;
        } while (token is not null);

        blobKeys.ShouldBe(new[] { "q01", "q03", "q05", "q07", "q09", "q11" });
        var first = await documents.ListPageAsync(new() { Take = 2, DimensionFilter = docFilter });
        first.IsSuccess.ShouldBeTrue(string.Join(";", first.Errors.Select(error => error.Message)));
        first.Value.Items.Select(key => key.RowKey).ShouldBe(new[] { "q01", "q03" });
        var second = await documents.ListPageAsync(new() { Take = 2, DimensionFilter = docFilter, ContinuationToken = first.Value.ContinuationToken });
        second.Value.Items.Select(key => key.RowKey).ShouldBe(new[] { "q05", "q07" });
        (await documents.CountAsync(new() { DimensionFilter = docFilter })).Value.ShouldBe(6);
        var payloads = await documents.FindPageAsync(new() { DimensionFilter = docFilter, Take = 2 });
        payloads.Value.Items.Select(item => item.Value.Amount).ShouldBe(new[] { 1, 3 });
        var unknown = DocumentDimensionFilter.Where(DocumentDimensionPredicate.Create("newname", DocumentDimensionOperator.Equal, 1).Value);
        (await documents.CountAsync(new() { DimensionFilter = unknown })).Value.ShouldBe(0);
        (await documents.UpsertAsync(new("queries", "new-owner"), new() { Amount = 20 }, new() { Dimensions = DocumentDimensions.Create(new Dictionary<string, object> { ["NewName"] = 1 }).Value })).IsSuccess.ShouldBeTrue();
        (await documents.CountAsync(new() { DimensionFilter = unknown })).Value.ShouldBe(1);
        var changed = DocumentDimensionFilter.Where(DocumentDimensionPredicate.Create("year", DocumentDimensionOperator.Equal, 0).Value);
        (await documents.ListPageAsync(new() { Take = 2, DimensionFilter = changed, ContinuationToken = first.Value.ContinuationToken })).IsFailure.ShouldBeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BlobWorkflows_TransformsCacheAndTransfers_PreserveDimensionsAndCommittedMetadata(bool cacheInsideTransforms)
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = new InMemoryCacheProvider(NullLoggerFactory.Instance, memory);
        var keys = new DictionaryEncryptionKeyProvider("key", new Dictionary<string, byte[]> { ["key"] = Enumerable.Range(0, 32).Select(index => (byte)index).ToArray() });
        IBlobStoreClient Decorate(IBlobStoreClient inner)
        {
            if (cacheInsideTransforms) inner = new CacheBlobStoreClientBehavior(null, inner, cache);
            IBlobStoreClient transforms = new ContentTypeDetectionBlobStoreClientBehavior(new CompressionBlobStoreClientBehavior(new EncryptionBlobStoreClientBehavior(inner, keys)));
            return cacheInsideTransforms ? transforms : new CacheBlobStoreClientBehavior(null, transforms, cache);
        }

        var provider = new EntityFrameworkBlobStoreProvider<DimensionsContext>(this.services.GetRequiredService<IServiceScopeFactory>());
        var sut = new BlobStoreClient("ef", provider, decorate: Decorate);
        var dimensions = BlobDimensions.Create(new Dictionary<string, object> { ["FiscalYear"] = 2026 }).Value;
        var source = new BlobKey("archive", "source.txt");
        var target = new BlobKey("archive", "target.txt");
        var uploaded = await sut.UploadTextAsync(source, "hello", new() { Dimensions = dimensions });
        uploaded.IsSuccess.ShouldBeTrue(string.Join(";", uploaded.Errors.Select(error => error.Message)));
        uploaded.Value.Dimensions["fiscalyear"].Value.ShouldBe(2026);
        (await sut.DownloadTextAsync(source)).Value.Info.Dimensions["fiscalyear"].Value.ShouldBe(2026);
        var updated = await sut.UpdateDimensionsAsync(new() { Key = source, IfMatchETag = uploaded.Value.ETag,
            Dimensions = BlobDimensions.Create(new Dictionary<string, object> { ["FiscalYear"] = 2027 }).Value });
        updated.IsSuccess.ShouldBeTrue();
        updated.Value.Length.ShouldBe(uploaded.Value.Length);
        updated.Value.ContentHash.ShouldBe(uploaded.Value.ContentHash);
        var downloaded = await sut.DownloadTextAsync(source);
        downloaded.Value.Text.ShouldBe("hello");
        downloaded.Value.Info.ETag.ShouldBe(updated.Value.ETag);
        downloaded.Value.Info.Dimensions["fiscalyear"].Value.ShouldBe(2027);
        var copied = await sut.CopyToAsync(source, sut, target);
        copied.IsSuccess.ShouldBeTrue();
        copied.Value.Target.Dimensions["fiscalyear"].Value.ShouldBe(2027);
        var emptySource = new BlobKey("archive", "empty.txt");
        (await sut.UploadTextAsync(emptySource, "empty")).IsSuccess.ShouldBeTrue();
        (await sut.CopyToAsync(emptySource, sut, target)).Value.Target.Dimensions.Count.ShouldBe(0);
        var unsupported = new BlobStoreClient("memory", new InMemoryBlobStoreProvider());
        (await sut.CopyToAsync(source, unsupported, target)).HasError<BlobDimensionsNotSupportedError>().ShouldBeTrue();
        (await sut.CopyToAsync(emptySource, unsupported, target)).IsSuccess.ShouldBeTrue();
        var moved = await sut.MoveToAsync(source, sut, new("archive", "moved.txt"), new() { Dimensions = BlobDimensions.Empty });
        moved.IsSuccess.ShouldBeTrue();
        moved.Value.Target.Dimensions.Count.ShouldBe(0);
        (await sut.ExistsAsync(source)).Value.ShouldBeFalse();
    }

    [Fact]
    public async Task DocumentCache_DimensionsOnlyUpdate_InvalidatesCachedVersion()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = new InMemoryCacheProvider(NullLoggerFactory.Instance, memory);
        var provider = new EntityFrameworkDocumentStoreProvider<DimensionsContext>(this.services.GetRequiredService<IServiceScopeFactory>());
        var sut = new CacheDocumentStoreClientBehavior<Invoice>(null, new DocumentStoreClient<Invoice>(provider), cache);
        var key = new DocumentKey("customer", "cached");
        (await sut.UpsertAsync(key, new() { Amount = 7 }, new() { Dimensions = DocumentDimensions.Create(new Dictionary<string, object> { ["year"] = 2026 }).Value })).IsSuccess.ShouldBeTrue();
        var first = await sut.GetAsync(key);
        var updated = await sut.UpdateDimensionsAsync(new() { Key = key, IfMatchETag = first.Value.ETag,
            Dimensions = DocumentDimensions.Create(new Dictionary<string, object> { ["year"] = 2027 }).Value });
        updated.IsSuccess.ShouldBeTrue();
        var second = await sut.GetAsync(key);
        second.Value.ETag.ShouldBe(updated.Value.ETag);
        second.Value.Value.Amount.ShouldBe(7);
        second.Value.Dimensions["year"].Value.ShouldBe(2027);
    }
}
