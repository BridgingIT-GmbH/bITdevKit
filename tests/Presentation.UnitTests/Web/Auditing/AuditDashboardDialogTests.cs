// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Auditing;

using System.Net;
using System.Text.RegularExpressions;
using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Presentation;
using BridgingIT.DevKit.Presentation.Web.Auditing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using AuditEndpoints = BridgingIT.DevKit.Presentation.Web.Auditing.Dashboard.DashboardEndpoints;

public class AuditDashboardDialogTests
{
    [Fact]
    public async Task Details_ExistingExecution_RendersScopedFragmentAndRelatedLinksWithoutRecordingReview()
    {
        await using var app = await CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var id = await AuditWebTestServices.CaptureAsync(scope.ServiceProvider);
        var relatedId = Guid.NewGuid();
        var provider = scope.ServiceProvider.GetRequiredService<IAuditingStorageProvider>();
        (await provider.AppendAsync(new()
        {
            AuditId = relatedId, OriginAuditId = id, CausationAuditId = id, Key = "customers.followup", CorrelationId = "review-correlation",
            DisplayName = "<script>unsafe label</script>", StartedUtc = DateTimeOffset.UtcNow.AddMinutes(-1), CompletedUtc = DateTimeOffset.UtcNow
        })).IsSuccess.ShouldBeTrue();
        var observations = app.Services.GetRequiredService<AuditRuntime>().SnapshotObservations();
        var client = app.GetTestClient();

        var response = await client.GetAsync($"/ops/auditing/{id}/details");
        var html = await response.Content.ReadAsStringAsync();
        var page = await client.GetStringAsync("/ops/auditing?key=rename&pageSize=250");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.CacheControl.NoStore.ShouldBeTrue();
        html.ShouldContain("data-audit-detail-fragment");
        html.ShouldContain("Rename customer");
        html.ShouldContain("Save customer");
        html.ShouldContain("Changes");
        html.ShouldContain($"data-audit-detail-url=\"/ops/auditing/{relatedId}/details\"");
        html.ShouldContain("&lt;script&gt;unsafe label&lt;/script&gt;");
        html.ShouldNotContain("<script>");
        html.ShouldNotContain("<!DOCTYPE");
        html.ShouldNotContain("audit-filters");
        html.ShouldNotContain("audit-download");
        page.ShouldContain("id=\"audit-detail-modal\"");
        page.ShouldNotContain("audit-detail-page");
        page.ShouldNotContain("audit-verified");
        page.ShouldContain($"data-audit-detail-url=\"/ops/auditing/{id}/details\"");
        var relatedHtml = await client.GetStringAsync($"/ops/auditing/{relatedId}/details");
        relatedHtml.ShouldContain("data-dashboard-correlation");
        relatedHtml.ShouldContain("/ops/logentries?");
        relatedHtml.ShouldContain("review-correlation");
        app.Services.GetService<AuditingEndpointsOptions>().ShouldBeNull();
        app.Services.GetRequiredService<AuditRuntime>().SnapshotObservations().ShouldBe(observations);
        (await scope.ServiceProvider.GetRequiredService<IAuditQueryService>().SearchAsync()).TotalCount.ShouldBe(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Details_MissingOrExcludedExecution_RendersUnavailableWithoutLeakingEvidence(bool excluded)
    {
        await using var app = await CreateAsync(excluded);
        await using var scope = app.Services.CreateAsyncScope();
        var id = await AuditWebTestServices.CaptureAsync(scope.ServiceProvider);

        var html = await app.GetTestClient().GetStringAsync($"/ops/auditing/{(excluded ? id : Guid.NewGuid())}/details");

        html.ShouldContain("data-audit-detail-fragment");
        html.ShouldContain("This execution is unavailable.");
        html.ShouldNotContain("Rename customer");
        html.ShouldNotContain("Save customer");
        html.ShouldNotContain(id.ToString());
    }

    [Fact]
    public async Task Data_FilteredExecutions_ReturnsNextPageAndTerminalFragmentUnderCustomPath()
    {
        await using var app = await CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var provider = scope.ServiceProvider.GetRequiredService<IAuditingStorageProvider>();
        var ids = Enumerable.Range(0, 27).Select(_ => Guid.NewGuid()).ToArray();
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 29; i++)
        {
            (await provider.AppendAsync(new()
            {
                AuditId = i < ids.Length ? ids[i] : Guid.NewGuid(),
                Key = i == 27 ? "orders.create" : "customers.rename",
                ModuleName = i == 28 ? "orders" : "customers",
                Outcome = AuditOutcome.Succeeded,
                StartedUtc = now.AddMinutes(-i - 1),
                CompletedUtc = now
            })).IsSuccess.ShouldBeTrue();
        }

        var observations = app.Services.GetRequiredService<AuditRuntime>().SnapshotObservations();
        var client = app.GetTestClient();
        const string query = "key=rename&module=customers&outcome=Succeeded&pageSize=25";

        var page = await client.GetStringAsync($"/ops/auditing?{query}");
        var response = await client.GetAsync($"/ops/auditing/data?{query}&page=2");
        var next = await response.Content.ReadAsStringAsync();
        var firstIds = Regex.Matches(page, "data-audit-id=\"([^\"]+)\"").Select(match => Guid.Parse(match.Groups[1].Value)).ToArray();
        var nextIds = Regex.Matches(next, "data-audit-id=\"([^\"]+)\"").Select(match => Guid.Parse(match.Groups[1].Value)).ToArray();

        firstIds.ShouldBe(ids.Take(25));
        nextIds.ShouldBe(ids.Skip(25));
        page.ShouldContain("data-url=\"/ops/auditing/data\"");
        page.ShouldContain("data-next-cursor=\"2\"");
        page.ShouldContain("Load more</button>");
        page.ShouldNotContain("Previous page");
        page.ShouldNotContain("Next page");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.CacheControl.NoStore.ShouldBeTrue();
        response.Content.Headers.ContentType.MediaType.ShouldBe("text/html");
        next.ShouldContain("id=\"audit-load-more\" data-page=\"2\"");
        next.ShouldContain("data-next-cursor=\"3\"");
        next.ShouldContain("disabled=\"disabled\"");
        next.ShouldContain("id=\"audit-size\"");
        next.ShouldNotContain("<!DOCTYPE");
        next.ShouldNotContain("audit-detail-modal");
        next.ShouldContain($"data-audit-detail-url=\"/ops/auditing/{ids[25]}/details\"");
        app.Services.GetRequiredService<AuditRuntime>().SnapshotObservations().ShouldBe(observations);
    }

    private static async Task<WebApplication> CreateAsync(bool exclude = false)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var auditing = builder.Services.AddAuditing();
        if (exclude) auditing.WithReadScope((_, _) => Task.FromResult(Result<AuditReadScope>.Success(new(
            new() { Filters = [new("Key", FilterOperator.Equal, "permitted.execution")] }, "scope-v1"))));
        builder.Services.AddDashboard(options => options.AllowAnonymous().WithGroupPath("/ops").WithPluginAssemblyContaining<AuditEndpoints>());
        var app = builder.Build();
        app.MapEndpoints();
        await app.StartAsync();
        return app;
    }
}
