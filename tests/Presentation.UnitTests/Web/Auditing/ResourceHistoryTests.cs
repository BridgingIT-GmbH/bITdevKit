// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Auditing;

using System.Text.Json;
using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Presentation.Web.Auditing;
using Microsoft.Extensions.DependencyInjection;

public sealed class ResourceHistoryTests
{
    [Fact]
    public async Task LoadAsync_HistoryRead_SuppressesOnlyTheQueryAndPreservesSurroundingBusinessActions()
    {
        await using var services = AuditWebTestServices.Create();
        await using var scope = services.CreateAsyncScope();
        var id = await AuditWebTestServices.CaptureAsync(scope.ServiceProvider);
        var audit = scope.ServiceProvider.GetRequiredService<IAuditContext>();
        var queries = scope.ServiceProvider.GetRequiredService<IAuditQueryService>();
        var root = audit.BeginScope(options => options.Key("customers.review"));
        await using (var before = audit.BeginScope(options => options.Key("before-history"))) before.Complete();

        var model = await ResourceHistoryModel.LoadAsync(queries, new("Customer", "C-42"));

        model.History.TotalCount.ShouldBe(1);
        model.History.Value.Single().Record.AuditId.ShouldBe(id);
        model.History.Value.Single().RootResourceMatched.ShouldBeTrue();
        audit.Current.ShouldBeSameAs(root);
        await using (var after = audit.BeginScope(options => options.Key("after-history"))) after.Complete();
        root.Complete();
        await root.DisposeAsync();
        var recorded = await services.GetRequiredService<IAuditingStorageProvider>().FindAsync(root.AuditId);
        recorded.Value.Actions.Select(action => action.Key).ShouldBe(["before-history", "after-history"]);
    }

    [Theory]
    [InlineData("/customers/C-42", true)]
    [InlineData("https://example.test/history", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("//example.test/history", false)]
    [InlineData("/\\example.test/history", false)]
    [InlineData("/history\r\nforged", false)]
    public void GetDetailLink_HostSuppliedDestination_AllowsOnlySafeLinks(string destination, bool allowed)
    {
        var model = new ResourceHistoryModel { DetailLink = _ => destination };

        model.GetDetailLink(new()).ShouldBe(allowed ? destination : null);
    }

    [Fact]
    public void FromRequest_ClientOrAndBusinessControls_CombineWithOuterAnd()
    {
        var client = new FilterModel { Filters = [new("Key", FilterOperator.Equal, "first") { Logic = FilterLogicOperator.Or }, new("Key", FilterOperator.Equal, "second")] };
        var http = AuditWebTestServices.Context(null, "?filter=" + Uri.EscapeDataString(JsonSerializer.Serialize(client, AuditFilterJson.CreateOptions())) + "&actor=U-42&actorRole=initiating&lifecycle=Archived&page=2&pageSize=25");

        var filter = AuditReviewFilter.FromRequest(http);

        filter.Filters.Count.ShouldBe(3);
        filter.Filters[0].Filters.Count.ShouldBe(2);
        filter.Filters[1].Field.ShouldBe("InitiatingUserId");
        filter.Filters[2].Field.ShouldBe("StorageState");
        filter.Page.ShouldBe(2);
        filter.PageSize.ShouldBe(25);
    }

    [Theory]
    [InlineData("?fromUtc=invalid")]
    [InlineData("?lifecycle=invalid")]
    public void FromRequest_InvalidReviewControls_RejectsSelection(string query)
    {
        var http = AuditWebTestServices.Context(null, query);

        Should.Throw<AuditExportException>(() => AuditReviewFilter.FromRequest(http)).Code.ShouldBe("AuditFilterInvalid");
    }
}
