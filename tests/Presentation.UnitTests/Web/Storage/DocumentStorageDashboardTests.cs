// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.UnitTests.Web;

using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using DocumentDashboardEndpoints = BridgingIT.DevKit.Presentation.Web.Storage.Documents.Dashboard.DashboardEndpoints;
using DocumentDashboardPageProvider = BridgingIT.DevKit.Presentation.Web.Storage.Documents.Dashboard.DashboardPageProvider;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public sealed class DocumentStorageDashboardTests
{
    [Fact]
    public void GetPages_WhenDocumentStorageIsNotRegistered_HidesDocumentPage()
    {
        var options = new DashboardEndpointsOptionsBuilder().Build();
        var services = new ServiceCollection().AddSingleton(options);
        var sut = new DocumentDashboardPageProvider(options);

        var pages = sut.GetPages(CreateHttpContext(services)).ToArray();

        pages.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetPages_WhenDocumentClientIsRegistered_ExposesDocumentPageAndCard()
    {
        var options = new DashboardEndpointsOptionsBuilder().Build();
        var services = new ServiceCollection().AddSingleton(options);
        services.AddDocumentStorage()
            .WithProvider<PersonStub>(_ => new InMemoryDocumentStoreProvider(), name: "default");
        var context = CreateHttpContext(services);
        var sut = new DocumentDashboardPageProvider(options);

        var page = sut.GetPages(context).Single();
        var card = await page.Card(context);

        page.Key.ShouldBe("storage.documents");
        page.Url.ShouldBe("/_bdk/dashboard/storage/documents");
        card.Value.ShouldBe("1");
        card.Url.ShouldBe(page.Url);
    }

    [Fact]
    public async Task Map_WithScopeValidationEnabled_DoesNotResolveScopedDocumentFactoryFromRootProvider()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Host.UseDefaultServiceProvider(options =>
        {
            options.ValidateScopes = true;
            options.ValidateOnBuild = true;
        });
        var dashboardOptions = new DashboardEndpointsOptionsBuilder().Build();
        builder.Services.AddRouting();
        builder.Services.AddSingleton(dashboardOptions);
        builder.Services.AddDocumentStorage()
            .WithProvider<PersonStub>(_ => new InMemoryDocumentStoreProvider(), name: "default");

        await using var app = builder.Build();
        var sut = new DocumentDashboardEndpoints(dashboardOptions);

        var action = () => sut.Map(app);

        action.ShouldNotThrow();
        var routes = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>().Select(endpoint => endpoint.RoutePattern.RawText).ToArray();
        routes.ShouldContain(route => route.EndsWith("/storage/documents/actions/delete", StringComparison.Ordinal));
        routes.ShouldNotContain(route => route.EndsWith("/storage/documents/actions/delete-batch", StringComparison.Ordinal));
    }

    private static DefaultHttpContext CreateHttpContext(IServiceCollection services) => new()
    {
        RequestServices = services.BuildServiceProvider()
    };

    public sealed class PersonStub
    {
        public string Name { get; set; }
    }
}
