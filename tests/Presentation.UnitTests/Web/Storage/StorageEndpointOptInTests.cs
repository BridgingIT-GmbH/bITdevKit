// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.UnitTests.Web;

using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Presentation.Web.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

public sealed class StorageEndpointOptInTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(3, false)]
    [InlineData(4, true)]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(5, false)]
    [InlineData(6, true)]
    [InlineData(6, false)]
    public async Task MapEndpoints_WithAssemblyDiscovery_MapsOnlyExplicitlyEnabledStorageGroups(int mode, bool scanFirst)
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        if (scanFirst) builder.Services.AddEndpoints(typeof(BlobStorageMaintenanceEndpoints).Assembly);
        var blobs = builder.Services.AddBlobStorage().WithInMemoryClient("reports");
        var documents = builder.Services.AddDocumentStorage().WithProvider<TestDocument>(_ => new InMemoryDocumentStoreProvider());
        if (mode == 1)
        {
            blobs.AddMaintenanceEndpoints(enabled: false).AddReadEndpoints(enabled: false);
            documents.AddMaintenanceEndpoints(enabled: false).AddReadEndpoints(enabled: false);
        }

        if (mode is 2 or 5) blobs.AddMaintenanceEndpoints();
        if (mode is 3 or 5) blobs.AddReadEndpoints();
        if (mode is 4 or 5) documents.AddMaintenanceEndpoints();
        if (mode is 5 or 6) documents.AddReadEndpoints();
        if (!scanFirst) builder.Services.AddEndpoints(typeof(BlobStorageMaintenanceEndpoints).Assembly);

        // Other discovered groups have separate dashboard and file-storage test hosts.
        Type[] endpointTypes = [typeof(BlobStorageMaintenanceEndpoints), typeof(BlobStorageReadEndpoints), typeof(DocumentStorageMaintenanceEndpoints), typeof(DocumentStorageReadEndpoints)];
        foreach (var descriptor in builder.Services.Where(item => item.ServiceType == typeof(IEndpoints) && !endpointTypes.Contains(item.ImplementationType)).ToArray())
        {
            builder.Services.Remove(descriptor);
        }

        await using var sut = builder.Build();

        // Act
        sut.MapEndpoints();
        var routes = ((IEndpointRouteBuilder)sut).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>().Select(endpoint => endpoint.RoutePattern.RawText).ToArray();

        // Assert
        routes.Contains("/_bdk/api/storage/blobs/clients").ShouldBe(mode is 2 or 5);
        routes.Contains("/_bdk/api/storage/blobs/{storeName}/content").ShouldBe(mode is 3 or 5);
        routes.Contains("/_bdk/api/storage/documents/clients").ShouldBe(mode is 4 or 5);
        var contentReads = ((IEndpointRouteBuilder)sut).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>().Where(endpoint => endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains("GET") == true)
            .Select(endpoint => endpoint.RoutePattern.RawText).ToArray();
        contentReads.Contains("/_bdk/api/storage/documents/{clientId}/documents").ShouldBe(mode is 5 or 6);
    }

    [Fact]
    public async Task MapEndpoints_WithExistingOptionsAndDefaultFeatureSetup_PreservesConfiguredPaths()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(new BlobStorageMaintenanceEndpointsOptions { GroupPath = "/ops/blobs" });
        builder.Services.AddSingleton(new BlobStorageReadEndpointsOptions { GroupPath = "/public/blobs" });
        builder.Services.AddSingleton(new DocumentStorageMaintenanceEndpointsOptions { GroupPath = "/ops/documents" });
        builder.Services.AddSingleton(new DocumentStorageReadEndpointsOptions { GroupPath = "/public/documents" });
        builder.Services.AddBlobStorage().WithInMemoryClient("reports").AddMaintenanceEndpoints().AddReadEndpoints();
        builder.Services.AddDocumentStorage().WithProvider<TestDocument>(_ => new InMemoryDocumentStoreProvider()).AddMaintenanceEndpoints().AddReadEndpoints();
        await using var sut = builder.Build();

        // Act
        sut.MapEndpoints();
        var routes = ((IEndpointRouteBuilder)sut).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>().Select(endpoint => endpoint.RoutePattern.RawText).ToArray();

        // Assert
        routes.ShouldContain("/ops/blobs/clients");
        routes.ShouldContain("/public/blobs/{storeName}/content");
        routes.ShouldContain("/ops/documents/clients");
        routes.ShouldContain("/public/documents/{clientId}/documents");
        routes.ShouldNotContain(route => route.StartsWith("/_bdk/api/storage", StringComparison.Ordinal));
    }

    public sealed class TestDocument
    {
        public string Name { get; set; }
    }
}
