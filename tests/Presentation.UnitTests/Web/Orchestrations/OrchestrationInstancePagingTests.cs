namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Orchestrations;

using System.Net;
using System.Text.RegularExpressions;
using BridgingIT.DevKit.Application.Orchestrations;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

public class OrchestrationInstancePagingTests
{
    [Fact]
    public async Task Instances_Continuation_SeeksTiesAndExcludesNewerInstances()
    {
        var started = new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
        var rows = Enumerable.Range(1, 4).Select(i => new OrchestrationInstanceModel
        {
            InstanceId = Guid.Parse($"{i:D8}-0000-0000-0000-000000000000"), OrchestrationName = "Orders",
            Status = "Completed", CurrentState = "Done", StartedUtc = started, LastUpdatedUtc = started
        }).ToList();
        rows.Add(new() { InstanceId = Guid.NewGuid(), OrchestrationName = "Orders extended", Status = "Completed", StartedUtc = started });
        var query = Substitute.For<IOrchestrationQueryService>();
        var sizes = new List<int>();
        query.SearchAsync(Arg.Any<FilterModel>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var filter = call.Arg<FilterModel>();
            filter.Page.ShouldBe(1);
            sizes.Add(filter.PageSize);
            var matches = rows.AsQueryable().ApplyFilter(filter);
            return ResultPaged<OrchestrationInstanceModel>.Success(matches.ApplyOrder(filter).ApplyPage(filter).ToArray(), matches.Count(), 1, filter.PageSize);
        });
        query.GetMetricsAsync(Arg.Any<OrchestrationMetricsRequest>(), Arg.Any<CancellationToken>()).Returns(Result<OrchestrationMetricsModel>.Success(new()));
        query.GetContextAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Result<OrchestrationContextSnapshotModel>.Success(new()));
        query.GetHistoryAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Result<IReadOnlyList<OrchestrationHistoryModel>>.Success([]));
        query.GetSignalsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Result<IReadOnlyList<OrchestrationSignalModel>>.Success([]));
        query.GetTimersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Result<IReadOnlyList<OrchestrationTimerModel>>.Success([]));
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddDashboard(options => options.AllowAnonymous().WithGroupPath("/ops")
            .WithPluginAssemblyContaining<Presentation.Web.Orchestrations.Dashboard.DashboardEndpoints>());
        builder.Services.AddSingleton(query);
        await using var app = builder.Build();
        app.MapEndpoints();
        await app.StartAsync();
        var client = app.GetTestClient();

        var first = await client.GetStringAsync("/ops/orchestrations/instances?take=2&name=Orders&status=Completed");
        first.ShouldNotContain("data-dashboard-query-error>");
        first.ShouldNotContain("orchestrations-take-filter");
        first.IndexOf("Rows per load", StringComparison.Ordinal).ShouldBeGreaterThan(first.IndexOf("</table>", StringComparison.Ordinal));
        var cursor = Attribute(first, "data-next-cursor");
        var boundary = Attribute(first, "data-boundary");
        cursor.ShouldNotBeNullOrWhiteSpace();
        var firstIds = Ids(first);
        firstIds.Length.ShouldBe(2);
        rows.Add(new() { InstanceId = Guid.NewGuid(), OrchestrationName = "Orders", Status = "Completed", StartedUtc = started.AddSeconds(1) });

        var next = await client.GetStringAsync($"/ops/orchestrations/content?view=instances&take=2&name=Orders&status=Completed&cursor={Uri.EscapeDataString(cursor)}&boundary={Uri.EscapeDataString(boundary)}");

        next.ShouldNotContain("data-dashboard-query-error>");
        var nextIds = Ids(next);
        nextIds.Length.ShouldBe(2);
        firstIds.Intersect(nextIds).ShouldBeEmpty();
        firstIds.Concat(nextIds).Distinct().Count().ShouldBe(4);
        Attribute(next, "data-next-cursor").ShouldBeNullOrEmpty();
        Regex.Match(next, "<button[^>]*data-dashboard-load-more[^>]*>").Value.ShouldContain("disabled");
        next.ShouldContain("2 rows loaded");
        sizes.ShouldBe(new[] { 3, 3 });
    }

    private static string Attribute(string html, string attribute) => WebUtility.HtmlDecode(Regex.Match(html, attribute + "=\"([^\"]*)\"").Groups[1].Value);

    private static string[] Ids(string html) => Regex.Matches(html, "<tr data-dashboard-row-id=\"([^\"]+)\"").Select(match => match.Groups[1].Value).ToArray();
}
