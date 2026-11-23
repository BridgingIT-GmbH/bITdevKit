// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using Application.Storage;

public sealed class DocumentFilterQueryTests
{
    private static readonly DocumentStoreProviderCapabilities Capabilities = new()
    {
        SupportsFilterModel = true,
        RowKeyPrefixMatch = DocumentQuerySupport.SupportedServerSide,
        Dimensions = new() { SupportsQueries = true }
    };

    [Fact]
    public void Prepare_ConfiguredPageSizeAndExplicitOrdering_UsesBoundedDefaultAndCompleteSeekOrder()
    {
        var filter = new FilterModel { Orderings = [new() { Field = "RowKey", Direction = OrderDirection.Descending }] };
        var options = new DocumentStoreOptions { DefaultTake = 1200, MaxTake = 1500 };
        var result = DocumentFilterQuery.Prepare(new("invoice"), filter, null, "ef", "billing", "type", options, Capabilities, null);
        result.IsSuccess.ShouldBeTrue();
        result.Value.PageSize.ShouldBe(1000);
        result.Value.Query.Order.Select(term => term.Field).ShouldBe(new[] { "RowKey", "PartitionKey", "PartitionKeyHash", "RowKeyHash" });
        result.Value.Query.Order[0].Descending.ShouldBeTrue();

        filter.PageSize = 1001;
        DocumentFilterQuery.Prepare(new("invoice"), filter, null, "ef", "billing", "type", options, Capabilities, null)
            .IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Prepare_TypedDimensionNullChecks_RequireSupportedFieldsAndNoOperand()
    {
        var filter = new FilterModel { Filters = [new("Dimensions.Optional", FilterOperator.IsNull, null)] };
        DocumentFilterQuery.Prepare(new(), filter, null, "ef", "billing", "type", new(), Capabilities, null)
            .Value.Query.Criteria.Dimension.Predicate.Operator.ShouldBe(DocumentDimensionOperator.IsNull);
        filter.Filters[0].Value = 3;
        DocumentFilterQuery.Prepare(new(), filter, null, "ef", "billing", "type", new(), Capabilities, null).IsFailure.ShouldBeTrue();
        filter.Filters[0].Field = "Properties.Optional";
        DocumentFilterQuery.Prepare(new("invoice"), filter, null, "ef", "billing", "type", new(), Capabilities, null).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Prepare_NonEfProvider_RejectsBothKeyAndDimensionFilters()
    {
        foreach (var field in new[] { "RowKey", "Dimensions.Reference" })
        {
            var filter = new FilterModel { Filters = [new(field, FilterOperator.Equal, "invoice")] };
            DocumentFilterQuery.Prepare(new("reports"), filter, null, "memory", "billing", "type", new(), new(), null)
                .HasError<DocumentStoreQueryNotSupportedError>().ShouldBeTrue();
        }
    }

    [Theory]
    [InlineData("provider-token")]
    [InlineData("null")]
    [InlineData("""["partition","row"]""")]
    [InlineData("""["partition","row","invalid-hash","invalid-hash"]""")]
    public async Task ListPageAsync_MalformedProviderPosition_ReturnsProviderError(string position)
    {
        var provider = Substitute.For<IDocumentStoreProvider>();
        provider.Capabilities.Returns(Capabilities);
        provider.ListPageAsync(Arg.Any<DocumentTypeIdentity>(), Arg.Any<DocumentFilterQuery>(), Arg.Any<DateTimeOffset>(), 1, Arg.Any<CancellationToken>())
            .Returns(Result<DocumentInfoPage>.Success(new() { ContinuationToken = position }));
        var sut = new DocumentStoreClient<Invoice>(provider);

        var result = await sut.ListPageAsync(new("invoices"), new() { PageSize = 1 }, null);

        result.HasError<DocumentStoreProviderError>().ShouldBeTrue();
    }

    public sealed class Invoice;
}
