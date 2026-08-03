// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web;

using BridgingIT.DevKit.Presentation.Web.Dashboard;
using BlobDashboardEndpoints = BridgingIT.DevKit.Presentation.Web.Storage.Blobs.Dashboard.DashboardEndpoints;
using BlobDashboardPageProvider = BridgingIT.DevKit.Presentation.Web.Storage.Blobs.Dashboard.DashboardPageProvider;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public sealed class BlobStorageDashboardTests
{
    [Fact]
    public void GetPages_WhenBlobStorageIsNotRegistered_HidesBlobPage()
    {
        // Arrange
        var options = new DashboardEndpointsOptionsBuilder().Build();
        var services = new ServiceCollection();
        services.AddSingleton(options);
        var context = CreateHttpContext(services);
        var sut = new BlobDashboardPageProvider(options);

        // Act
        var pages = sut.GetPages(context).ToArray();

        // Assert
        pages.ShouldBeEmpty();
    }

    [Fact]
    public void GetPages_WhenBlobClientIsRegistered_ShowsBlobPage()
    {
        // Arrange
        var options = new DashboardEndpointsOptionsBuilder().Build();
        var services = new ServiceCollection();
        services.AddSingleton(options);
        services.AddBlobStorage()
            .WithInMemoryClient("reports");
        var context = CreateHttpContext(services);
        var sut = new BlobDashboardPageProvider(options);

        // Act
        var page = sut.GetPages(context).Single();

        // Assert
        page.Key.ShouldBe("storage.blobs");
        page.Title.ShouldBe("Blobs");
        page.Url.ShouldBe("/_bdk/dashboard/storage/blobs");
        page.Group.ShouldBe("bdk");
        page.Card.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetPages_WhenBlobClientIsRegistered_ExposesReadableIndexCard()
    {
        // Arrange
        var options = new DashboardEndpointsOptionsBuilder().Build();
        var services = new ServiceCollection();
        services.AddSingleton(options);
        services.AddBlobStorage()
            .WithInMemoryClient("reports");
        var context = CreateHttpContext(services);
        var sut = new BlobDashboardPageProvider(options);
        var page = sut.GetPages(context).Single();

        // Act
        var card = await page.Card(context);

        // Assert
        card.Title.ShouldBe("Blobs");
        card.Subtitle.ShouldBe("Blob clients");
        card.Value.ShouldBe("1");
        card.Detail.ShouldBe("reports");
        card.Url.ShouldBe("/_bdk/dashboard/storage/blobs");
    }

    [Fact]
    public async Task Map_WithScopeValidationEnabled_DoesNotResolveScopedBlobFactoryFromRootProvider()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.Host.UseDefaultServiceProvider(options =>
        {
            options.ValidateScopes = true;
            options.ValidateOnBuild = true;
        });

        var dashboardOptions = new DashboardEndpointsOptionsBuilder().Build();
        builder.Services.AddRouting();
        builder.Services.AddSingleton(dashboardOptions);
        builder.Services.AddBlobStorage()
            .WithInMemoryClient("reports");

        await using var app = builder.Build();
        var sut = new BlobDashboardEndpoints(dashboardOptions);

        // Act
        var action = () => sut.Map(app);

        // Assert
        action.ShouldNotThrow();
        var routes = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>().Select(endpoint => endpoint.RoutePattern.RawText).ToArray();
        routes.ShouldContain(route => route.EndsWith("/storage/blobs/actions/upload", StringComparison.Ordinal));
        routes.ShouldContain(route => route.EndsWith("/storage/blobs/actions/delete", StringComparison.Ordinal));
    }

    private static DefaultHttpContext CreateHttpContext(IServiceCollection services) =>
        new()
        {
            RequestServices = services.BuildServiceProvider()
        };
}
