// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.UnitTests.Web;

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

        html.ShouldContain("data-dashboard-query-error");
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

        html.ShouldContain("data-dashboard-query-error");
        await sut.DidNotReceive().FindJsonPageAsync(Arg.Any<DocumentQuery>(), Arg.Any<CancellationToken>());
        await sut.DidNotReceive().CountAsync(Arg.Any<DocumentCountQuery>(), Arg.Any<CancellationToken>());
    }

    private static async Task<string> RenderBlobAsync(IBlobStoreClient client, string filter)
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
        return await http.GetStringAsync("/_bdk/dashboard/storage/blobs/content?store=reports&container=records&continuationToken=next&dimensionFilter=" + Uri.EscapeDataString(filter));
    }

    private static async Task<string> RenderDocumentAsync(IDocumentStoreClientAccessor accessor, string filter, bool supported)
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
        return await http.GetStringAsync("/_bdk/dashboard/storage/documents/content?clientId=reports&continuationToken=next&dimensionFilter=" + Uri.EscapeDataString(filter));
    }

    public class Record { }
}
