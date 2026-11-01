// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Auditing;

using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Presentation.Web.Auditing;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using AuditPageProvider = BridgingIT.DevKit.Presentation.Web.Auditing.Dashboard.DashboardPageProvider;

public sealed class AuditDashboardTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AddDashboard_BothRegistrationOrders_DiscoverOneAuditPageAndCard(bool dashboardFirst)
    {
        var collection = new ServiceCollection();
        collection.AddLogging();
        if (dashboardFirst) collection.AddDashboard(options => options.WithGroupPath("/ops").AllowAnonymous());
        collection.AddAuditing();
        if (!dashboardFirst) collection.AddDashboard(options => options.WithGroupPath("/ops").AllowAnonymous());
        using var services = collection.BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };

        var page = http.GetDashboardPages().Where(item => item.Key == "auditing").ShouldHaveSingleItem();

        services.GetServices<IDashboardPageProvider>().OfType<AuditPageProvider>().ShouldHaveSingleItem();
        page.Url.ShouldBe("/ops/auditing");
        page.Group.ShouldBe("bdk");
        page.RememberedViews["executions"].ShouldBe("/ops/auditing");
        page.RememberedViews["diagnostics"].ShouldBe("/ops/auditing/diagnostics");
        var card = await page.Card(http);
        card.Title.ShouldBe("Auditing");
        card.FeatureKey.ShouldBe("auditing");
        card.Group.ShouldBe("bdk");
        services.GetService<AuditingEndpointsOptions>().ShouldBeNull();
        services.GetService<AuditHttpOptions>().ShouldBeNull();
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void GetPages_MissingAuditingOrDisabledDashboard_OffersNoAuditPage(bool auditing, bool enabled)
    {
        var collection = new ServiceCollection();
        if (auditing) collection.AddAuditing();
        using var services = collection.BuildServiceProvider();
        var provider = new AuditPageProvider(new() { Enabled = enabled });

        provider.GetPages(new DefaultHttpContext { RequestServices = services }).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("rename", 1)]
    [InlineData("stomers.re", 1)]
    [InlineData("RENAME", 1)]
    [InlineData("missing", 0)]
    public async Task BuildAsync_PartialBusinessKey_FiltersSearchAndExport(string key, int expected)
    {
        await using var services = AuditWebTestServices.Create();
        await using var scope = services.CreateAsyncScope();
        await AuditWebTestServices.CaptureAsync(scope.ServiceProvider);
        var http = AuditWebTestServices.Context(scope.ServiceProvider, "?key=" + key);

        var search = await AuditDashboardModelBuilder.BuildAsync(http, "/ops/auditing");
        var exportFilter = AuditFilterJson.Parse(System.Text.Encoding.UTF8.GetBytes(search.FilterJson)).Value;
        var exported = await scope.ServiceProvider.GetRequiredService<IAuditQueryService>().SearchAsync(exportFilter);

        search.Records.IsSuccess.ShouldBeTrue();
        search.Records.TotalCount.ShouldBe(expected);
        exported.IsSuccess.ShouldBeTrue();
        exported.TotalCount.ShouldBe(expected);
    }

    [Theory]
    [InlineData(250)]
    [InlineData(500)]
    public async Task BuildAsync_LargePageSize_PreservesRequestedSize(int pageSize)
    {
        await using var services = AuditWebTestServices.Create();
        await using var scope = services.CreateAsyncScope();
        await AuditWebTestServices.CaptureAsync(scope.ServiceProvider);
        var http = AuditWebTestServices.Context(scope.ServiceProvider, "?pageSize=" + pageSize);

        var model = await AuditDashboardModelBuilder.BuildAsync(http, "/ops/auditing");

        model.Records.IsSuccess.ShouldBeTrue();
        model.Records.PageSize.ShouldBe(pageSize);
        model.Records.TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task BuildAsync_SearchDetailAndDiagnostics_UseReadModelsWithoutRecordingReview()
    {
        await using var services = AuditWebTestServices.Create();
        await using var scope = services.CreateAsyncScope();
        var id = await AuditWebTestServices.CaptureAsync(scope.ServiceProvider);
        var http = AuditWebTestServices.Context(scope.ServiceProvider, "?page=2&key=customers.rename");

        var search = await AuditDashboardModelBuilder.BuildAsync(http, "/ops/auditing");
        var detail = await AuditDashboardModelBuilder.BuildAsync(http, "/ops/auditing", id);
        var diagnostics = await AuditDashboardModelBuilder.BuildAsync(http, "/ops/auditing", diagnostics: true);

        search.Records.TotalCount.ShouldBe(1);
        AuditFilterJson.Parse(System.Text.Encoding.UTF8.GetBytes(search.FilterJson)).Value.Page.ShouldBe(0);
        search.DownloadTicket.ShouldNotBeNullOrEmpty();
        detail.HasDetail.ShouldBeTrue();
        detail.Details.Value.Record.AuditId.ShouldBe(id);
        detail.Details.Value.Record.Actions.ShouldHaveSingleItem().DisplayName.ShouldBe("Save customer");
        diagnostics.Diagnostics.ShouldNotBeNull();
        (await scope.ServiceProvider.GetRequiredService<IAuditQueryService>().SearchAsync()).TotalCount.ShouldBe(1);
    }
}
