// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.UnitTests.Web;

using System.Net;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using BlobEndpoints = BridgingIT.DevKit.Presentation.Web.Storage.Blobs.Dashboard.DashboardEndpoints;
using DocumentEndpoints = BridgingIT.DevKit.Presentation.Web.Storage.Documents.Dashboard.DashboardEndpoints;

public class StorageDimensionsDashboardTests
{
    private const string Filter = """{"all":[{"name":"Year","operator":"Equal","operand":{"type":"integer","value":2026}},{"name":"Approved","operator":"IsNotNull"}]}""";

    [Fact]
    public async Task BlobContent_ValidFilter_ForwardsFilterAndContinuation()
    {
        // Arrange
        var sut = Substitute.For<IBlobStoreClient>();
        sut.DimensionCapabilities.Returns(new BlobDimensionCapabilities { SupportsDimensions = true, SupportsQueries = true });
        sut.ListPageAsync(Arg.Any<BlobQuery>(), Arg.Any<CancellationToken>()).Returns(Result<BlobPage>.Success(new()));

        // Act
        var html = await RenderBlobAsync(sut, Filter);

        // Assert
        html.ShouldNotContain("data-dashboard-query-error");
        html.ShouldContain("data-dimension-filter-valid=\"true\"");
        html.ShouldContain("data-dimension-conditions");
        html.ShouldNotContain("data-applied-dimensions");
        await sut.Received(1).ListPageAsync(Arg.Is<BlobQuery>(q => q.ContinuationToken == "next" && q.Container == "records"
            && q.DimensionFilter.Children.Count == 2 && q.DimensionFilter.Children[0].Predicate.Name == "Year"), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false, Filter)]
    [InlineData(true, "{")]
    [InlineData(true, """{"name":"Amount","operator":"GreaterThan","operand":{"type":"decimal","value":"12.5"}}""")]
    public async Task BlobContent_UnsupportedOrInvalidFilter_DoesNotList(bool supported, string filter)
    {
        var sut = Substitute.For<IBlobStoreClient>();
        sut.DimensionCapabilities.Returns(new BlobDimensionCapabilities { SupportsDimensions = supported, SupportsQueries = supported, SupportsDecimalComparisons = false });

        var html = await RenderBlobAsync(sut, filter);

        html.ShouldContain("role=\"alert\" data-dashboard-query-error");
        await sut.DidNotReceive().ListPageAsync(Arg.Any<BlobQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DocumentContent_ValidFilter_UsesSameFilterForRowsAndCount()
    {
        var sut = Substitute.For<IDocumentStoreClientAccessor>();
        sut.FindJsonPageAsync(Arg.Any<DocumentQuery>(), Arg.Any<CancellationToken>()).Returns(Result<DocumentJsonPage>.Success(new()));
        sut.CountAsync(Arg.Any<DocumentCountQuery>(), Arg.Any<CancellationToken>()).Returns(Result<long>.Success(0));

        var html = await RenderDocumentAsync(sut, Filter, true);

        html.ShouldNotContain("data-dashboard-query-error");
        html.ShouldContain("data-dimension-conditions");
        html.ShouldNotContain("data-applied-dimensions");
        await sut.Received(1).FindJsonPageAsync(Arg.Is<DocumentQuery>(q => q.ContinuationToken == "next" && q.DimensionFilter.Children[0].Predicate.Name == "Year"), Arg.Any<CancellationToken>());
        await sut.Received(1).CountAsync(Arg.Is<DocumentCountQuery>(q => q.DimensionFilter.Children.Count == 2 && q.DimensionFilter.Children[0].Predicate.Name == "Year"), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false, Filter)]
    [InlineData(true, "{")]
    [InlineData(true, """{"name":"Amount","operator":"GreaterThan","operand":{"type":"decimal","value":"12.5"}}""")]
    public async Task DocumentContent_UnsupportedOrInvalidFilter_DoesNotRead(bool supported, string filter)
    {
        var sut = Substitute.For<IDocumentStoreClientAccessor>();

        var html = await RenderDocumentAsync(sut, filter, supported);

        html.ShouldContain("role=\"alert\" data-dashboard-query-error");
        await sut.DidNotReceive().FindJsonPageAsync(Arg.Any<DocumentQuery>(), Arg.Any<CancellationToken>());
        await sut.DidNotReceive().CountAsync(Arg.Any<DocumentCountQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BlobPage_WithStoredDimensions_RendersUploadFieldsAndIncludesValuesForDetails()
    {
        var client = Substitute.For<IBlobStoreClient>();
        client.DimensionCapabilities.Returns(new BlobDimensionCapabilities { SupportsDimensions = true, SupportsMetadataUpdates = true });
        var dimensions = BlobDimensions.Create(new Dictionary<string, object> { ["token"] = "fixture-42", ["amount"] = 999999999999.999999m });
        client.ListPageAsync(Arg.Any<BlobQuery>(), Arg.Any<CancellationToken>()).Returns(Result<BlobPage>.Success(new()
        {
            Items = [new BlobInfo { Key = new("records", "report.txt"), Dimensions = dimensions.Value }]
        }));

        var html = WebUtility.HtmlDecode(await RenderBlobAsync(client, "", fullPage: true));

        html.ShouldContain("data-blobs-upload-properties");
        html.ShouldContain("data-storage-dimension-fields");
        html.ShouldContain("data-supports-dimensions=\"true\"");
        html.ShouldContain("data-supports-dimension-updates=\"true\"");
        html.ShouldContain("data-dimension-columns");
        html.ShouldContain("data-preview-url=\"/_bdk/dashboard/storage/blobs/preview\"");
        html.ShouldContain("data-blobs-action=\"/_bdk/dashboard/storage/blobs/actions/metadata\"");
        html.ShouldContain("Save changes");
        html.ShouldContain("""data-dimensions="{"token":{"type":"string","value":"fixture-42"},"amount":{"type":"decimal","value":"999999999999.999999"}}""");
    }

    [Fact]
    public async Task DocumentPage_WithSelectedDocument_RendersEditorAndIncludesValuesForDetails()
    {
        var accessor = Substitute.For<IDocumentStoreClientAccessor>();
        accessor.CountAsync(Arg.Any<DocumentCountQuery>(), Arg.Any<CancellationToken>()).Returns(Result<long>.Success(1));
        var key = new DocumentKey("records", "report");
        var dimensions = DocumentDimensions.Create(new Dictionary<string, object> { ["token"] = "fixture-42", ["approved"] = false, ["status"] = null });
        var entry = new DocumentJsonEntry
        {
            Info = new DocumentInfo { Key = key, Dimensions = dimensions.Value },
            Content = "{}"
        };
        accessor.FindJsonPageAsync(Arg.Any<DocumentQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<DocumentJsonPage>.Success(new() { Items = [entry] }));
        accessor.GetEntryJsonAsync(key, Arg.Any<CancellationToken>()).Returns(Result<DocumentJsonEntry>.Success(entry));

        var html = WebUtility.HtmlDecode(await RenderDocumentAsync(accessor, "", true, showDetails: true));

        html.ShouldContain("id=\"documents-details-properties\"");
        html.ShouldContain("data-storage-dimension-fields");
        html.ShouldContain("data-supports-dimensions=\"true\"");
        html.ShouldContain("data-dimension-columns");
        const string typedDimensions = """{"token":{"type":"string","value":"fixture-42"},"approved":{"type":"boolean","value":false},"status":{"type":"null","value":null}}""";
        html.ShouldContain($"data-dimensions=\"{typedDimensions}\"");
        html.ShouldContain($"data-documents-selected-dimensions>{typedDimensions}</textarea>");
    }

    private static async Task<string> RenderBlobAsync(IBlobStoreClient client, string filter, bool fullPage = false)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var options = new DashboardEndpointsOptionsBuilder().AllowAnonymous().Build();
        builder.Services.AddSingleton(options);
        builder.Services.AddBlobStorage().WithInMemoryClient("reports");
        var factory = Substitute.For<IBlobStoreClientFactory>();
        factory.GetRegistrations().Returns([new BlobStoreClientRegistration { Name = "reports", ProviderName = "test" }]);
        factory.CreateClient("reports").Returns(client);
        builder.Services.AddSingleton(factory);
        await using var app = builder.Build();
        new BlobEndpoints(options).Map(app);
        await app.StartAsync();
        using var http = app.GetTestClient();
        var path = fullPage ? "/_bdk/dashboard/storage/blobs" : "/_bdk/dashboard/storage/blobs/content";
        return await http.GetStringAsync(path + "?store=reports&container=records&continuationToken=next&dimensionFilter=" + Uri.EscapeDataString(filter));
    }

    private static async Task<string> RenderDocumentAsync(IDocumentStoreClientAccessor accessor, string filter, bool supported, bool showDetails = false)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var options = new DashboardEndpointsOptionsBuilder().AllowAnonymous().Build();
        builder.Services.AddSingleton(options);
        builder.Services.AddDocumentStorage().WithProvider<Record>(_ => new InMemoryDocumentStoreProvider());
        var factory = Substitute.For<IDocumentStoreClientFactory>();
        factory.GetDescriptors().Returns([new("reports", typeof(Record), "Record", "test", new()
        {
            Dimensions = new() { SupportsDimensions = supported, SupportsQueries = supported, SupportsDecimalComparisons = false }
        })]);
        factory.Create("reports").Returns(accessor);
        builder.Services.AddSingleton(factory);
        await using var app = builder.Build();
        new DocumentEndpoints(options).Map(app);
        await app.StartAsync();
        using var http = app.GetTestClient();
        var path = showDetails ? "/_bdk/dashboard/storage/documents" : "/_bdk/dashboard/storage/documents/content";
        var selection = showDetails ? "&selectedPartitionKey=records&selectedRowKey=report" : "";
        return await http.GetStringAsync(path + "?clientId=reports&continuationToken=next&dimensionFilter=" + Uri.EscapeDataString(filter) + selection);
    }

    public class Record { }
}
