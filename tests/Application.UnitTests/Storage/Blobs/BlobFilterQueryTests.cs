// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using System.Text.Json;
using Application.Storage;

public sealed class BlobFilterQueryTests
{
    private static readonly BlobStoreProviderCapabilities Capabilities = new()
    {
        SupportsFilterModel = true,
        SupportsPrefixListing = true,
        Dimensions = new() { SupportsQueries = true, SupportsDecimalComparisons = true }
    };

    [Fact]
    public void Prepare_MixedNestedCriteria_BindsNormalizedTypedQueryToContinuation()
    {
        using var json = JsonDocument.Parse("""{"type":"integer","value":2026}""");
        var scope = new BlobFilterScope("reports");
        var filter = new FilterModel
        {
            Filters =
            [
                new("Name", FilterOperator.StartsWith, "issued/") { Logic = FilterLogicOperator.Or },
                new() { Filters = [new("Dimensions.Year", FilterOperator.Equal, json.RootElement)] }
            ]
        };
        var options = new BlobStoreOptions { DefaultTake = 23 };
        var protector = new HmacContinuationTokenProtector(new byte[32]);

        var first = BlobFilterQuery.Prepare(scope, filter, null, "ef", "reports", options, Capabilities, protector);
        first.IsSuccess.ShouldBeTrue();
        first.Value.PageSize.ShouldBe(23);
        first.Value.Query.Criteria.IsAny.ShouldBeTrue();
        var token = StorageFilterQuery.WrapPosition(JsonSerializer.Serialize(new[] { "issued/1", new string('a', 64) }),
            BlobFilterQuery.Purpose, "ef", "reports", null, first.Value.Hash, protector, BlobFilterQuery.IsValidPosition).Value;
        filter.Filters[1].Filters[0].Field = "Dimensions.year";
        filter.Filters[1].Filters[0].Value = 2026;

        var second = BlobFilterQuery.Prepare(scope, filter, token, "ef", "reports", options, Capabilities, protector);
        second.IsSuccess.ShouldBeTrue();
        second.Value.Query.Position.First().ShouldBe("issued/1");
        BlobFilterQuery.Prepare(scope, filter, token, "ef", "other", options, Capabilities, protector)
            .HasError<BlobStoreInvalidContinuationTokenError>().ShouldBeTrue();
        BlobFilterQuery.Prepare(scope, filter, token + "a", "ef", "reports", options, Capabilities, protector)
            .HasError<BlobStoreInvalidContinuationTokenError>().ShouldBeTrue();

        filter.PageSize = 21;
        BlobFilterQuery.Prepare(scope, filter, token, "ef", "reports", options, Capabilities, protector)
            .HasError<BlobStoreInvalidContinuationTokenError>().ShouldBeTrue();
    }

    [Fact]
    public void Prepare_UnanchoredOrBranch_RequiresFullScanApproval()
    {
        var filter = new FilterModel
        {
            Filters =
            [
                new("Name", FilterOperator.Equal, "invoice") { Logic = FilterLogicOperator.Or },
                new("Dimensions.Year", FilterOperator.NotEqual, 2026)
            ]
        };
        BlobFilterQuery.Prepare(new("reports"), filter, null, "ef", "reports", new(), Capabilities, null)
            .HasError<BlobStoreQueryTooBroadError>().ShouldBeTrue();
        BlobFilterQuery.Prepare(new("reports", "issued/"), filter, null, "ef", "reports", new(), Capabilities, null)
            .IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ListPageAsync_InMemoryClient_RejectsKeyOnlyFilter()
    {
        IBlobStoreClient sut = new BlobStoreClient("memory", new InMemoryBlobStoreProvider());
        var result = await sut.ListPageAsync(new("reports"), new() { Filters = [new("Name", FilterOperator.Equal, "invoice")] }, null);
        result.HasError<BlobStoreQueryNotSupportedError>().ShouldBeTrue();
        var native = await sut.ListPageAsync(null, default);
        native.HasError<BlobStoreValidationError>().ShouldBeTrue();
    }

    [Theory]
    [InlineData("provider-token")]
    [InlineData("null")]
    [InlineData("""["only-a-name"]""")]
    [InlineData("""["issued/1","invalid-hash"]""")]
    public async Task ListPageAsync_MalformedProviderPosition_ReturnsProviderError(string position)
    {
        var provider = Substitute.For<IBlobStoreProvider>();
        provider.Capabilities.Returns(Capabilities);
        provider.ListPageAsync(Arg.Any<BlobFilterQuery>(), 1, Arg.Any<CancellationToken>())
            .Returns(Result<StoredBlobPage>.Success(new() { ContinuationToken = position }));
        var sut = new BlobStoreClient("ef", provider);

        var result = await sut.ListPageAsync(new("reports", "issued/"), new() { PageSize = 1 }, null);

        result.HasError<BlobStoreProviderError>().ShouldBeTrue();
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(25, 25)]
    public async Task ListPageAsync_FilteredListing_ForwardsScopeAndPagingToBehaviors(int pageSize, int? expectedTake)
    {
        var inner = Substitute.For<IBlobStoreClient>();
        var scope = new BlobFilterScope("reports", "issued/", AllowFullScan: true);
        var filter = new FilterModel { PageSize = pageSize, Filters = [new("Dimensions.Year", FilterOperator.Equal, 2026)] };
        var expected = Result<BlobPage>.Success(new() { ContinuationToken = "next" });
        using var cancellation = new CancellationTokenSource();
        var cancellationToken = cancellation.Token;
        inner.ListPageAsync(scope, filter, "token", cancellationToken).Returns(expected);
        var sut = new InspectListingBehavior(inner);

        var result = await sut.ListPageAsync(scope, filter, "token", cancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ContinuationToken.ShouldBe("next");
        await inner.Received(1).ListPageAsync(scope, filter, "token", cancellationToken);
        sut.Operation.ShouldBe("list_page");
        sut.Context.Query.Container.ShouldBe(scope.Container);
        sut.Context.Query.Prefix.ShouldBe(scope.Prefix);
        sut.Context.Query.AllowFullScan.ShouldBeTrue();
        sut.Context.Query.Take.ShouldBe(expectedTake);
        sut.Context.Query.ContinuationToken.ShouldBe("token");
        sut.Context.Query.DimensionFilter.ShouldBeNull();
    }

    private sealed class InspectListingBehavior(IBlobStoreClient inner) : BlobStoreClientBehaviorBase(inner)
    {
        public string Operation { get; private set; }

        public BlobStoreOperationContext Context { get; private set; }

        protected override Task<Result<T>> ExecuteAsync<T>(string operation, BlobStoreOperationContext context,
            Func<CancellationToken, Task<Result<T>>> next, CancellationToken cancellationToken)
        {
            this.Operation = operation;
            this.Context = context;
            return next(cancellationToken);
        }

        protected override Task<Result> ExecuteAsync(string operation, BlobStoreOperationContext context,
            Func<CancellationToken, Task<Result>> next, CancellationToken cancellationToken) => next(cancellationToken);
    }
}
