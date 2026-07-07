// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.Azure.Storage;

using System.Linq.Expressions;
using Application.Storage;
using Infrastructure.Azure;

public class CosmosDocumentStoreProviderPagingTests
{
    [Fact]
    public async Task ListPageResultAsync_WithContinuation_UsesProjectedCosmosPageAndRestoresNativeToken()
    {
        var provider = new FakeCosmosSqlProvider();
        provider.EnqueueProjectedPage(
            [new CosmosStorageDocument { PartitionKey = "partition", RowKey = "row-1" }],
            "native-next");
        provider.EnqueueProjectedPage(
            [new CosmosStorageDocument { PartitionKey = "partition", RowKey = "row-2" }],
            null);
        var sut = new CosmosDocumentStoreProvider(provider);

        var firstPage = await sut.ListPageResultAsync<PersonStub>(
            DocumentQueries.Query()
                .ForKey("partition", "row-")
                .WithRowKeyPrefix()
                .Take(1)
                .Build());
        var secondPage = await sut.ListPageResultAsync<PersonStub>(
            DocumentQueries.Query()
                .ForKey("partition", "row-")
                .WithRowKeyPrefix()
                .Take(1)
                .ContinueWith(firstPage.Value.ContinuationToken)
                .Build());

        firstPage.IsSuccess.ShouldBeTrue();
        firstPage.Value.Items.Single().RowKey.ShouldBe("row-1");
        firstPage.Value.ContinuationToken.ShouldNotBeNullOrWhiteSpace();
        secondPage.IsSuccess.ShouldBeTrue();
        secondPage.Value.Items.Single().RowKey.ShouldBe("row-2");
        secondPage.Value.ContinuationToken.ShouldBeNull();
        provider.ItemPageCalls.ShouldBe(0);
        provider.ProjectedPageCalls.ShouldBe(2);
        provider.ContinuationTokens.ShouldBe([null, "native-next"]);
        provider.TakeValues.ShouldBe([1, 1]);
    }

    [Fact]
    public async Task CountResultAsync_WithDocuments_UsesProjectedCosmosPagesWithoutLoadingItems()
    {
        var provider = new FakeCosmosSqlProvider();
        provider.EnqueueProjectedPage(
            [
                new CosmosStorageDocument { PartitionKey = "partition", RowKey = "row-1" },
                new CosmosStorageDocument { PartitionKey = "partition", RowKey = "row-2" }
            ],
            "native-next");
        provider.EnqueueProjectedPage(
            [new CosmosStorageDocument { PartitionKey = "partition", RowKey = "row-3" }],
            null);
        var sut = new CosmosDocumentStoreProvider(provider);

        var result = await sut.CountResultAsync<PersonStub>(
            DocumentQueries.Count()
                .ForKey("partition", "row-")
                .WithRowKeyPrefix()
                .Build());

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(3);
        provider.ItemPageCalls.ShouldBe(0);
        provider.ProjectedPageCalls.ShouldBe(2);
        provider.ContinuationTokens.ShouldBe([null, "native-next"]);
    }

    [Fact]
    public async Task ExistsResultAsync_WithDocument_UsesProjectedCosmosPageWithSingleTake()
    {
        var provider = new FakeCosmosSqlProvider();
        provider.EnqueueProjectedPage(
            [new CosmosStorageDocument { PartitionKey = "partition", RowKey = "row-1" }],
            null);
        var sut = new CosmosDocumentStoreProvider(provider);

        var result = await sut.ExistsResultAsync<PersonStub>(new DocumentKey("partition", "row-1"));

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
        provider.ItemPageCalls.ShouldBe(0);
        provider.ProjectedPageCalls.ShouldBe(1);
        provider.TakeValues.ShouldBe([1]);
    }

    private sealed class FakeCosmosSqlProvider : ICosmosSqlProvider<CosmosStorageDocument>
    {
        private readonly Queue<(IReadOnlyCollection<CosmosStorageDocument> Items, string ContinuationToken)> projectedPages = [];

        public int ItemPageCalls { get; private set; }

        public int ProjectedPageCalls { get; private set; }

        public List<string> ContinuationTokens { get; } = [];

        public List<int?> TakeValues { get; } = [];

        public void EnqueueProjectedPage(IReadOnlyCollection<CosmosStorageDocument> items, string continuationToken)
        {
            this.projectedPages.Enqueue((items, continuationToken));
        }

        public Task<CosmosStorageDocument> ReadItemAsync(
            string id,
            object partitionKey = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CosmosStorageDocument> CreateItemAsync(
            CosmosStorageDocument item,
            object partitionKeyValue = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CosmosStorageDocument> UpsertItemAsync(
            CosmosStorageDocument item,
            object partitionKeyValue = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IEnumerable<CosmosStorageDocument>> ReadItemsAsync(
            Expression<Func<CosmosStorageDocument, bool>> expression,
            int? skip = null,
            int? take = null,
            Expression<Func<CosmosStorageDocument, object>> orderExpression = null,
            bool orderDescending = false,
            object partitionKeyValue = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IEnumerable<CosmosStorageDocument>> ReadItemsAsync(
            IEnumerable<Expression<Func<CosmosStorageDocument, bool>>> expressions = null,
            int? skip = null,
            int? take = null,
            Expression<Func<CosmosStorageDocument, object>> orderExpression = null,
            bool orderDescending = false,
            object partitionKeyValue = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<CosmosSqlPage<CosmosStorageDocument>>> ReadItemsPageResultAsync(
            Expression<Func<CosmosStorageDocument, bool>> expression,
            int? take = null,
            Expression<Func<CosmosStorageDocument, object>> orderExpression = null,
            bool orderDescending = false,
            object partitionKeyValue = null,
            string continuationToken = null,
            CancellationToken cancellationToken = default) =>
            this.ReadItemsPageResultAsync(
                expression is null ? null : [expression],
                take,
                orderExpression,
                orderDescending,
                partitionKeyValue,
                continuationToken,
                cancellationToken);

        public Task<Result<CosmosSqlPage<CosmosStorageDocument>>> ReadItemsPageResultAsync(
            IEnumerable<Expression<Func<CosmosStorageDocument, bool>>> expressions = null,
            int? take = null,
            Expression<Func<CosmosStorageDocument, object>> orderExpression = null,
            bool orderDescending = false,
            object partitionKeyValue = null,
            string continuationToken = null,
            CancellationToken cancellationToken = default)
        {
            this.ItemPageCalls++;
            return Task.FromResult(Result<CosmosSqlPage<CosmosStorageDocument>>.Success(new CosmosSqlPage<CosmosStorageDocument>()));
        }

        public Task<Result<CosmosSqlPage<TResult>>> ReadItemsPageResultAsync<TResult>(
            Expression<Func<CosmosStorageDocument, bool>> expression,
            Expression<Func<CosmosStorageDocument, TResult>> projection,
            int? take = null,
            Expression<Func<CosmosStorageDocument, object>> orderExpression = null,
            bool orderDescending = false,
            object partitionKeyValue = null,
            string continuationToken = null,
            CancellationToken cancellationToken = default) =>
            this.ReadItemsPageResultAsync(
                expression is null ? null : [expression],
                projection,
                take,
                orderExpression,
                orderDescending,
                partitionKeyValue,
                continuationToken,
                cancellationToken);

        public Task<Result<CosmosSqlPage<TResult>>> ReadItemsPageResultAsync<TResult>(
            IEnumerable<Expression<Func<CosmosStorageDocument, bool>>> expressions,
            Expression<Func<CosmosStorageDocument, TResult>> projection,
            int? take = null,
            Expression<Func<CosmosStorageDocument, object>> orderExpression = null,
            bool orderDescending = false,
            object partitionKeyValue = null,
            string continuationToken = null,
            CancellationToken cancellationToken = default)
        {
            this.ProjectedPageCalls++;
            this.ContinuationTokens.Add(continuationToken);
            this.TakeValues.Add(take);
            var page = this.projectedPages.Dequeue();
            var projector = projection.Compile();

            return Task.FromResult(Result<CosmosSqlPage<TResult>>.Success(new CosmosSqlPage<TResult>
            {
                Items = page.Items.Select(projector).ToList(),
                ContinuationToken = page.ContinuationToken
            }));
        }

        public Task<bool> DeleteItemAsync(
            string id,
            object partitionKeyValue = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
