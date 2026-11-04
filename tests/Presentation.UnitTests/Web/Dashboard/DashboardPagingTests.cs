// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Dashboard;

using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Presentation;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using BridgingIT.DevKit.Presentation.Web.Messaging.Dashboard.Pages;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using RazorSlices;

/// <summary>Verifies that dashboard continuation requests retain filter scope and remain bounded.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~DashboardPagingTests</code></example>
public class DashboardPagingTests
{
    [Fact]
    public async Task Jobs_AllMatchingRegistrations_RenderBeyondQueryBatchLimitWithoutPagingControls()
    {
        var service = Substitute.For<BridgingIT.DevKit.Application.Jobs.IJobSchedulerQueryService>();
        var jobs = Enumerable.Range(0, 1003).Select(i => new BridgingIT.DevKit.Application.Jobs.JobSchedulerJobModel { JobName = $"job-{i:D4}" }).ToArray();
        var pages = new List<int>();
        service.SearchAsync(Arg.Any<FilterModel>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var filter = call.Arg<FilterModel>();
            pages.Add(filter.Page);
            return ResultPaged<BridgingIT.DevKit.Application.Jobs.JobSchedulerJobModel>.Success(
                jobs.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize), jobs.Length, filter.Page, filter.PageSize);
        });
        service.GetDashboardSummaryAsync(Arg.Any<BridgingIT.DevKit.Application.Jobs.JobSchedulerDashboardSummaryRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<BridgingIT.DevKit.Application.Jobs.JobSchedulerDashboardSummaryModel>.Success(new()));
        var skips = new List<int>();
        service.QueryTriggersAsync(Arg.Any<BridgingIT.DevKit.Application.Jobs.JobSchedulerTriggerQueryRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var request = call.Arg<BridgingIT.DevKit.Application.Jobs.JobSchedulerTriggerQueryRequest>();
            skips.Add(request.Skip);
            var triggers = Enumerable.Range(request.Skip, Math.Min(request.Take, 501 - request.Skip))
                .Select(i => new BridgingIT.DevKit.Application.Jobs.JobSchedulerTriggerModel { JobName = "job-1002", TriggerName = $"trigger-{i}" });
            return ResultPaged<BridgingIT.DevKit.Application.Jobs.JobSchedulerTriggerModel>.Success(triggers, 501, 1, request.Take);
        });
        service.SearchOccurrencesAsync(Arg.Any<FilterModel>(), Arg.Any<CancellationToken>())
            .Returns(ResultPaged<BridgingIT.DevKit.Application.Jobs.JobSchedulerOccurrenceModel>.Success([], 0, 1, 50));
        var services = new ServiceCollection(); services.AddLogging(); services.AddSingleton(service);
        using var provider = services.BuildServiceProvider();
        await using var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.QueryString = new("?jobTake=1&jobCursor=job-1000&jobBoundary=job-1001");
        context.Response.Body = body;

        await Results.RazorSlice<BridgingIT.DevKit.Presentation.Web.Jobs.Dashboard.Pages.Data>().ExecuteAsync(context);

        var html = System.Text.Encoding.UTF8.GetString(body.ToArray());
        pages.ShouldBe(new[] { 1, 2 });
        skips.ShouldBe(new[] { 0, 500 });
        html.ShouldContain("job-0000");
        html.ShouldContain("job-1002");
        html.ShouldNotContain("Load more");
        html.ShouldNotContain("Rows per load");
        html.ShouldNotContain("data-dashboard-query-error");
    }

    /// <summary>Jobs filters constrain the owning search before bounded continuation is applied.</summary>
    /// <example><code>await suite.Jobs_Filters_AreAppliedBeforePaging();</code></example>
    [Fact]
    public async Task Jobs_Filters_AreAppliedBeforePaging()
    {
        var service = Substitute.For<BridgingIT.DevKit.Application.Jobs.IJobSchedulerQueryService>();
        FilterModel captured = null;
        service.SearchAsync(Arg.Any<FilterModel>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            captured = call.Arg<FilterModel>();
            return ResultPaged<BridgingIT.DevKit.Application.Jobs.JobSchedulerJobModel>.Success([], 0, 1, 11);
        });
        var services = new ServiceCollection(); services.AddLogging(); services.AddSingleton(service);
        using var provider = services.BuildServiceProvider();
        await using var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.QueryString = new("?jobName=alive&jobGroup=ops&jobEnabled=true&jobPaused=false&jobTake=10&jobCursor=alpha");
        context.Response.Body = body;

        await Results.RazorSlice<BridgingIT.DevKit.Presentation.Web.Jobs.Dashboard.Pages.Data>().ExecuteAsync(context);

        captured.PageSize.ShouldBe(1000);
        captured.Filters.ShouldContain(filter => filter.Field == "JobName" && filter.Operator == FilterOperator.Contains && (string)filter.Value == "alive");
        captured.Filters.ShouldContain(filter => filter.Field == "Group" && (string)filter.Value == "ops");
        captured.Filters.ShouldContain(filter => filter.Field == "EffectiveEnabled" && (bool)filter.Value);
        captured.Filters.ShouldContain(filter => filter.Field == "Paused" && !(bool)filter.Value);
        captured.Filters.ShouldNotContain(filter => filter.Operator == FilterOperator.GreaterThan || filter.Operator == FilterOperator.GreaterThanOrEqual);
    }

    /// <summary>Occurrence criteria are applied before seeking the next bounded batch.</summary>
    /// <example><code>await suite.Occurrences_Filters_AreAppliedBeforePaging();</code></example>
    [Fact]
    public async Task Occurrences_Filters_AreAppliedBeforePaging()
    {
        var service = Substitute.For<BridgingIT.DevKit.Application.Jobs.IJobSchedulerQueryService>();
        FilterModel captured = null;
        service.SearchOccurrencesAsync(Arg.Any<FilterModel>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            captured = call.Arg<FilterModel>();
            return ResultPaged<BridgingIT.DevKit.Application.Jobs.JobSchedulerOccurrenceModel>.Success([], 0, 1, 11);
        });
        var services = new ServiceCollection(); services.AddLogging(); services.AddSingleton(service);
        using var provider = services.BuildServiceProvider();
        await using var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = provider };
        var cursorId = Guid.NewGuid();
        var cursor = DashboardHistoryCursor.Encode(DateTimeOffset.UtcNow, cursorId);
        context.Request.QueryString = new($"?view=occurrences&occurrenceJob=alive&occurrenceTrigger=manual&occurrenceStatus=Pending&occurrenceCorrelation=trace&occurrenceTake=10&occurrenceCursor={cursor}");
        context.Response.Body = body;

        await Results.RazorSlice<BridgingIT.DevKit.Presentation.Web.Jobs.Dashboard.Pages.Data>().ExecuteAsync(context);

        await service.DidNotReceive().SearchAsync(Arg.Any<FilterModel>(), Arg.Any<CancellationToken>());
        captured.Page.ShouldBe(1);
        captured.PageSize.ShouldBe(11);
        captured.Filters.ShouldContain(filter => filter.Field == "JobName" && filter.Operator == FilterOperator.Contains && (string)filter.Value == "alive");
        captured.Filters.ShouldContain(filter => filter.Field == "TriggerName" && (string)filter.Value == "manual");
        captured.Filters.ShouldContain(filter => filter.Field == "CorrelationId" && (string)filter.Value == "trace");
        captured.Filters.ShouldContain(filter => filter.Field == "Status" && (JobOccurrenceStatus)filter.Value == JobOccurrenceStatus.Pending);
        captured.Orderings.Select(order => order.Field).ShouldBe(new[] { "CreatedDate", "OccurrenceId" });
        captured.Filters.ShouldContain(filter => filter.Filters.Count > 0);
    }

    /// <summary>Operational routes omit chart owners and Diagnostics stays under a configured dashboard root.</summary>
    /// <example><code>await suite.Views_CustomRoot_SeparatesOperationalListsAndDiagnostics("jobs", false);</code></example>
    [Theory]
    [InlineData("jobs", false)]
    [InlineData("jobs/occurrences", false)]
    [InlineData("jobs/diagnostics", true)]
    [InlineData("messaging", false)]
    [InlineData("messaging/messages", false)]
    [InlineData("messaging/diagnostics", true)]
    [InlineData("queueing", false)]
    [InlineData("queueing/messages", false)]
    [InlineData("queueing/diagnostics", true)]
    [InlineData("orchestrations", false)]
    [InlineData("orchestrations/instances", false)]
    [InlineData("orchestrations/diagnostics", true)]
    public async Task Views_CustomRoot_SeparatesOperationalListsAndDiagnostics(string route, bool diagnostics)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddDashboard(options => options.AllowAnonymous().WithGroupPath("/ops")
            .WithPluginAssemblyContaining<BridgingIT.DevKit.Presentation.Web.Jobs.Dashboard.DashboardEndpoints>()
            .WithPluginAssemblyContaining<BridgingIT.DevKit.Presentation.Web.Messaging.Dashboard.DashboardEndpoints>()
            .WithPluginAssemblyContaining<BridgingIT.DevKit.Presentation.Web.Queueing.Dashboard.DashboardEndpoints>()
            .WithPluginAssemblyContaining<BridgingIT.DevKit.Presentation.Web.Orchestrations.Dashboard.DashboardEndpoints>());
        await using var app = builder.Build();
        app.MapEndpoints();
        await app.StartAsync();

        var html = await app.GetTestClient().GetStringAsync($"/ops/{route}?dashboardView=window");

        html.ShouldContain("data-dashboard-view");
        html.ShouldContain($"/ops/{route.Split('/')[0]}/diagnostics");
        html.ShouldNotContain("href=\"/_bdk/dashboard/");
        if (diagnostics) html.ShouldContain("-realtime-graph\"");
        else html.ShouldNotContain("-realtime-graph\"");
    }

    /// <summary>A continuation uses strict identity seeking rather than increasing Take or shifting an offset.</summary>
    /// <example><code>await suite.Render_Continuation_UsesAppliedScopeAndBoundedSeek(10);</code></example>
    [Theory]
    [InlineData(10)]
    [InlineData(40)]
    [InlineData(250)]
    [InlineData(500)]
    public async Task Render_Continuation_UsesAppliedScopeAndBoundedSeek(int size)
    {
        var service = Substitute.For<IMessageBrokerService>();
        FilterModel captured = null;
        service.GetMessageStatsAsync().ReturnsForAnyArgs(new BrokerMessageStats());
        service.GetSubscriptionsAsync().ReturnsForAnyArgs(Array.Empty<BrokerMessageSubscriptionInfo>());
        service.GetWaitingMessagesAsync().ReturnsForAnyArgs(Array.Empty<BrokerMessageInfo>());
        service.SearchMessagesAsync(Arg.Any<FilterModel>(), Arg.Any<bool?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                captured = call.Arg<FilterModel>();
                return ResultPaged<BrokerMessageInfo>.Success([], 0, 1, size + 1);
            });
        var services = new ServiceCollection(); services.AddLogging(); services.AddSingleton(service);
        using var provider = services.BuildServiceProvider();
        await using var body = new MemoryStream();
        var cursor = DashboardHistoryCursor.Encode(DateTimeOffset.UtcNow.AddMinutes(-1), Guid.Parse("00000010-0000-0000-0000-000000000000"));
        var boundary = DashboardHistoryCursor.Encode(DateTimeOffset.UtcNow, Guid.Parse("00000020-0000-0000-0000-000000000000"));
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.Path = "/messaging/content";
        context.Request.QueryString = new($"?view=messages&type=Invoice&take={size}&cursor={cursor}&boundary={boundary}");
        context.Response.Body = body;

        await Results.RazorSlice<Data>().ExecuteAsync(context);

        captured.Page.ShouldBe(1);
        captured.PageSize.ShouldBe(size + 1);
        captured.Filters.ShouldContain(filter => filter.Field == "Type" && filter.Operator == FilterOperator.Contains && (string)filter.Value == "Invoice");
        captured.Orderings.Select(order => order.Field).ShouldBe(new[] { "CreatedDate", "Id" });
        captured.Filters.Count(filter => filter.Filters.Count > 0).ShouldBe(2);
        body.Position = 0;
        var html = await new StreamReader(body).ReadToEndAsync();
        html.ShouldContain($"value=\"{size}\" selected=\"selected\"");
        html.ShouldContain("Rows per load");
        html.ShouldContain("Load more");
        html.ShouldNotContain("data-dashboard-query-error");
    }
}
