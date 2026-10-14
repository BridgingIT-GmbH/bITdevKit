// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Profiling;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using BridgingIT.DevKit.Presentation.Web.Profiling.Dashboard;
using BridgingIT.DevKit.Presentation.Web.Profiling.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using ProfilingEndpoints = BridgingIT.DevKit.Presentation.Web.Profiling.Dashboard.DashboardEndpoints;

/// <summary>Verifies the actual retained-history Razor and JSON paths without a provider flush during reads.</summary>
public sealed class OperationProfilingDashboardTests
{
    /// <summary>Both lists offer the supported row choices and render a full 500-row page with a centered continuation control.</summary>
    /// <example><code>await suite.List_RowChoices_Accept500AndCenterLoadMore(false);</code></example>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task List_RowChoices_Accept500AndCenterLoadMore(bool requestsOnly)
    {
        await using var app = await CreateAsync();
        var kind = requestsOnly ? OperationProfilingKind.HttpRequest : OperationProfilingKind.Job;
        for (var index = 0; index < 501; index++) { Capture(app, "rows:work", kind); }

        await app.Services.GetRequiredService<OperationProfilingWriterService>().TickAsync();
        var path = "/admin/profiling/" + (requestsOnly ? "requests" : "operations");
        var html = await app.GetTestClient().GetStringAsync(path + "?limit=500");
        Regex.Matches(html, "data-operation-id=").Count.ShouldBe(500);
        var choices = Regex.Match(html, "<select id=\"operation-limit\"[^>]*>(.*?)</select>").Groups[1].Value;
        Regex.Matches(choices, "value=\"([^\"]+)\"").Select(match => match.Groups[1].Value).ShouldBe(new[] { "50", "100", "250", "500" });
        html.ShouldContain("class=\"d-flex justify-content-center mt-2\" data-operation-paging");
        html.ShouldContain("data-operation-load-more");
    }

    /// <summary>Both lists render a Tail switch and filtered deltas with complete counts and stable continuation URLs.</summary>
    /// <example><code>await suite.List_Tail_UsesPublicationCheckpointAndCompleteGroupCounts(false);</code></example>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task List_Tail_UsesPublicationCheckpointAndCompleteGroupCounts(bool requestsOnly)
    {
        await using var app = await CreateAsync();
        var kind = requestsOnly ? OperationProfilingKind.HttpRequest : OperationProfilingKind.Job;
        var initial = Capture(app, "tail-test", kind);
        var writer = app.Services.GetRequiredService<OperationProfilingWriterService>();
        await writer.TickAsync();
        using var client = app.GetTestClient();
        var path = "/admin/profiling/" + (requestsOnly ? "requests" : "operations");
        var page = await client.GetStringAsync(path + "?key=tail-test&groupBy=category&limit=10");
        page.ShouldContain("id=\"operation-tail\"");
        page.ShouldNotContain("id=\"operation-refresh-interval\"");
        var watermark = Regex.Match(page, "data-tail-watermark=\"([^\"]+)\"").Groups[1].Value;
        var tailUrl = WebUtility.HtmlDecode(Regex.Match(page, "data-tail-url=\"([^\"]+)\"").Groups[1].Value);
        tailUrl.ShouldStartWith(path + "/content?");
        var added = Enumerable.Range(0, 23).Select(_ => Capture(app, "tail-test", kind)).ToHashSet();
        Capture(app, "excluded", kind);
        await writer.TickAsync();
        var html = await client.GetStringAsync(tailUrl + "&afterCommitWatermark=" + watermark);
        var seen = new HashSet<Guid>();
        while (true)
        {
            foreach (Match row in Regex.Matches(html, "data-operation-id=\"([^\"]+)\"")) { seen.Add(Guid.Parse(row.Groups[1].Value)).ShouldBeTrue(); }

            html.ShouldNotContain("data-operation-id=\"" + initial + "\"");
            html.ShouldContain(">24 executions</a>");
            var next = Regex.Match(html, "data-operation-load-more data-next-url=\"([^\"]+)\"");
            if (!next.Success) { break; }

            var url = WebUtility.HtmlDecode(next.Groups[1].Value);
            url.ShouldContain("afterCommitWatermark=" + watermark);
            html = await client.GetStringAsync(url);
        }

        seen.SetEquals(added).ShouldBeTrue();
        var nextWatermark = Regex.Match(html, "data-tail-watermark=\"([^\"]+)\"").Groups[1].Value;
        var empty = await client.GetStringAsync(tailUrl + "&afterCommitWatermark=" + nextWatermark);
        empty.ShouldNotContain("data-operation-id=");
        empty.ShouldContain("data-operation-metrics");
    }

    /// <summary>Both lists expose filtered continuation fragments with stable row and group identities for appending.</summary>
    /// <example><code>await suite.List_LoadMore_PreservesSelectionAndReturnsEveryRecording(false, "Recent");</code></example>
    [Theory]
    [InlineData(false, "Recent")]
    [InlineData(true, "Recent")]
    [InlineData(false, "Slow")]
    [InlineData(true, "Slow")]
    [InlineData(false, "ByCount")]
    [InlineData(true, "ByCount")]
    public async Task List_LoadMore_PreservesSelectionAndReturnsEveryRecording(bool requestsOnly, string view)
    {
        // Arrange
        await using var app = await CreateAsync();
        var kind = requestsOnly ? OperationProfilingKind.HttpRequest : OperationProfilingKind.Job;
        var expected = new HashSet<Guid>();
        for (var index = 0; index < 23; index++)
        {
            expected.Add(Capture(app, "load-test:" + (view == "ByCount" ? index : index % 2), kind, "load-test"));
        }

        Capture(app, "load-test:excluded", kind, "another-correlation");
        Capture(app, "other-key", kind, "load-test");
        await app.Services.GetRequiredService<OperationProfilingWriterService>().TickAsync();
        using var client = app.GetTestClient();
        var path = "/admin/profiling/" + (requestsOnly ? "requests" : "operations");
        var selection = "?limit=10&view=" + view + "&key=load-test:&correlationId=load-test&groupBy=category";

        // Act
        var html = await client.GetStringAsync(path + selection);
        var seen = new HashSet<Guid>();
        var groupKeys = new HashSet<string>();
        var pages = 0;
        while (true)
        {
            pages++;
            foreach (Match row in Regex.Matches(html, "data-operation-id=\"([^\"]+)\""))
            {
                seen.Add(Guid.Parse(row.Groups[1].Value)).ShouldBeTrue();
            }

            foreach (Match group in Regex.Matches(html, "data-preserve-key=\"(group-[^\"]+)\""))
            {
                groupKeys.Add(group.Groups[1].Value);
            }

            var next = Regex.Match(html, "data-operation-load-more data-next-url=\"([^\"]+)\"");
            if (!next.Success) { break; }

            var url = WebUtility.HtmlDecode(next.Groups[1].Value);
            url.ShouldStartWith(path + "/content?");
            url.ShouldContain("cursor=");
            url.ShouldContain("correlationId=load-test");
            url.ShouldContain("groupBy=category");
            html.ShouldContain(">Load more</button>");
            html.ShouldNotContain("Next page");
            pages.ShouldBeLessThan(4);
            html = await client.GetStringAsync(url);
        }

        // Assert
        pages.ShouldBe(3);
        seen.SetEquals(expected).ShouldBeTrue();
        groupKeys.Count.ShouldBe(view == "ByCount" ? 23 : 2);
        html.ShouldNotContain("data-operation-load-more");
    }

    /// <summary>Hotspot rankings analyze every retained root beyond the visible group limit and use shared dialogs.</summary>
    /// <example><code>await suite.Insights_Rankings_AnalyzeTheWholeSelection();</code></example>
    [Fact]
    public async Task Insights_Rankings_AnalyzeTheWholeSelection()
    {
        await using var app = await CreateAsync();
        await app.Services.GetRequiredService<OperationProfilingWriterService>().TickAsync();
        for (var index = 0; index < 30; index++) { Capture(app, "job:unique-" + index, OperationProfilingKind.Job); }

        for (var index = 0; index < 3; index++) { Capture(app, "job:frequent", OperationProfilingKind.Job); }

        using (var failed = app.Services.GetRequiredService<IOperationProfiler>().BeginOperation(new() { Key = "job:failed", Kind = "Job" }))
        { failed.Fail(new ProfilingFailureDescriptor { Source = "Test", Code = "Rejected" }); }

        await app.Services.GetRequiredService<OperationProfilingWriterService>().TickAsync();
        using var client = app.GetTestClient();
        var json = await client.GetFromJsonAsync<JsonElement>("/admin/profiling/insights/data?rank=count&limit=25");
        json.GetProperty("count").GetInt32().ShouldBe(34);
        json.GetProperty("completedCount").GetInt32().ShouldBe(33);
        json.GetProperty("groupCount").GetInt32().ShouldBe(32);
        json.GetProperty("groups").GetArrayLength().ShouldBe(25);
        var first = json.GetProperty("groups")[0];
        first.GetProperty("key").GetString().ShouldBe("job:frequent");
        first.GetProperty("count").GetInt32().ShouldBe(3);
        first.GetProperty("segments").GetArrayLength().ShouldBeGreaterThan(0);
        (await client.GetAsync("/admin/profiling/operations/" + first.GetProperty("representativeId").GetString() + "/content")).StatusCode.ShouldBe(HttpStatusCode.OK);
        json.GetProperty("nodes")[0].GetProperty("id").GetGuid().ShouldNotBe(Guid.Empty);
        var failures = await client.GetFromJsonAsync<JsonElement>("/admin/profiling/insights/data?rank=failures");
        failures.GetProperty("groups")[0].GetProperty("key").GetString().ShouldBe("job:failed");
        failures.GetProperty("groups")[0].GetProperty("completedLatency").GetProperty("durationUnavailable").GetBoolean().ShouldBeTrue();
        var html = await client.GetStringAsync("/admin/profiling/insights");
        html.ShouldContain("id=\"insights-rows\"");
        html.ShouldContain("id=\"operation-detail-modal\"");
        html.ShouldContain("id=\"operation-comparison-modal\"");
        html.ShouldContain("id=\"insights-segments-modal\"");
        html.ShouldContain("data-dashboard-date-range-from=\"operation-from\"");
        html.ShouldContain("data-insights=\"true\"");
    }

    /// <summary>Empty dates resolve to a bounded current window, and basic filters remain optional.</summary>
    /// <example><code>await suite.Insights_OpenDatesAndFilters_SelectRetainedStarts();</code></example>
    [Fact]
    public async Task Insights_OpenDatesAndFilters_SelectRetainedStarts()
    {
        await using var app = await CreateAsync();
        await app.Services.GetRequiredService<OperationProfilingWriterService>().TickAsync();
        Capture(app, "job:weather", OperationProfilingKind.Job);
        Capture(app, "/cities", OperationProfilingKind.HttpRequest);
        await app.Services.GetRequiredService<OperationProfilingWriterService>().TickAsync();
        using var client = app.GetTestClient();
        var json = await client.GetFromJsonAsync<JsonElement>("/admin/profiling/insights/data");
        (json.GetProperty("toUtc").GetDateTimeOffset() - json.GetProperty("fromUtc").GetDateTimeOffset()).ShouldBe(TimeSpan.FromMinutes(15));
        json.GetProperty("count").GetInt32().ShouldBe(2);
        var selected = await client.GetFromJsonAsync<JsonElement>("/admin/profiling/insights/data?key=WEATHER&kind=Job&fromUtc=" + Uri.EscapeDataString(DateTimeOffset.UtcNow.AddHours(-1).ToString("O")));
        selected.GetProperty("count").GetInt32().ShouldBe(1);
        var node = json.GetProperty("nodes")[0].GetProperty("id").GetGuid();
        (await client.GetFromJsonAsync<JsonElement>("/admin/profiling/insights/data?nodeId=" + node)).GetProperty("count").GetInt32().ShouldBe(2);
        (await client.GetFromJsonAsync<JsonElement>("/admin/profiling/insights/data?nodeId=" + Guid.NewGuid())).GetProperty("count").GetInt32().ShouldBe(0);
    }

    /// <summary>Invalid filters return safe validation responses.</summary>
    /// <example><code>await suite.Insights_InvalidSelection_ReturnsBadRequest("rank=invalid");</code></example>
    [Theory]
    [InlineData("rank=invalid")]
    [InlineData("limit=0")]
    [InlineData("limit=501")]
    [InlineData("nodeId=invalid")]
    [InlineData("fromUtc=invalid")]
    [InlineData("toUtc=0001-01-01T00:00Z")]
    [InlineData("fromUtc=2026-10-12T12:00Z&toUtc=2026-10-12T11:00Z")]
    public async Task Insights_InvalidSelection_ReturnsBadRequest(string selection)
    {
        await using var app = await CreateAsync();
        (await app.GetTestClient().GetAsync("/admin/profiling/insights/data?" + selection)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>Every resolution resolves empty dates to a rolling window ending at now, and the new tab shares the existing details modal.</summary>
    /// <example><code>await suite.Timeline_EmptyDates_UseLiveWindow("hour", 60);</code></example>
    [Theory]
    [InlineData("15m", 15)]
    [InlineData("30m", 30)]
    [InlineData("hour", 60)]
    [InlineData("12h", 720)]
    [InlineData("day", 1440)]
    [InlineData("week", 10080)]
    [InlineData("month", 43200)]
    public async Task Timeline_EmptyDates_UseLiveWindow(string resolution, int minutes)
    {
        await using var app = await CreateAsync();
        await app.Services.GetRequiredService<OperationProfilingWriterService>().TickAsync();
        Capture(app, "timeline:work", OperationProfilingKind.Job);
        await app.Services.GetRequiredService<OperationProfilingWriterService>().TickAsync();
        using var client = app.GetTestClient();
        var timeline = await client.GetFromJsonAsync<OperationProfilingTimeline>("/admin/profiling/timeline/data?resolution=" + resolution);
        timeline.TotalCount.ShouldBe(1);
        (timeline.ToUtc - timeline.FromUtc).ShouldBe(TimeSpan.FromMinutes(minutes));
        timeline.ToUtc.ShouldBeInRange(DateTimeOffset.UtcNow.AddSeconds(-5), DateTimeOffset.UtcNow.AddSeconds(5));
        var html = await client.GetStringAsync("/admin/profiling/timeline");
        html.ShouldContain("id=\"timeline-chart\"");
        html.ShouldContain("id=\"operation-detail-modal\"");
        html.ShouldContain("data-timeline=\"true\"");
        html.ShouldContain("data-dashboard-date-range-from=\"timeline-from\"");
        html.ShouldContain("['runtime', 'operations', 'requests', 'timeline', 'insights']");
        foreach (var view in new[] { "operations", "requests" })
        {
            var page = await client.GetStringAsync("/admin/profiling/" + view);
            page.ShouldContain("href=\"/admin/profiling/timeline\"");
        }
    }

    /// <summary>Timeline filters select starts by key, kind and node; cluster pagination retains its clicked window and returns compact data.</summary>
    /// <example><code>await suite.Timeline_Filters_AndClusterPaging_UseTheSharedOperationDialog();</code></example>
    [Fact]
    public async Task Timeline_Filters_AndClusterPaging_UseTheSharedOperationDialog()
    {
        await using var app = await CreateAsync();
        var writer = app.Services.GetRequiredService<OperationProfilingWriterService>();
        await writer.TickAsync();
        for (var index = 0; index < 40; index++) { Capture(app, "weather:work", OperationProfilingKind.Service); }

        Capture(app, "weather:other", OperationProfilingKind.Job);
        Capture(app, "unrelated", OperationProfilingKind.Service);
        await writer.TickAsync();
        using var client = app.GetTestClient();
        var node = app.Services.GetRequiredService<IProfilingNodeIdentityProvider>().GetNode().Identity.Id;
        var parameters = "key=WEATHER:WORK&kind=SERVICE&nodeId=" + node;
        var timeline = await client.GetFromJsonAsync<OperationProfilingTimeline>("/admin/profiling/timeline/data?" + parameters);
        timeline.TotalCount.ShouldBe(40);
        var projection = await client.GetFromJsonAsync<JsonElement>("/admin/profiling/timeline/data?" + parameters);
        projection.GetProperty("nodes")[0].GetProperty("id").GetGuid().ShouldBe(node);
        projection.GetProperty("nodes")[0].GetProperty("name").GetString().ShouldNotBeNullOrWhiteSpace();
        var frozen = parameters + "&fromUtc=" + Uri.EscapeDataString(timeline.FromUtc.ToString("O")) + "&toUtc=" + Uri.EscapeDataString(timeline.ToUtc.ToString("O"));
        var page = await client.GetFromJsonAsync<JsonElement>("/admin/profiling/timeline/executions?" + frozen);
        page.GetProperty("totalCount").GetInt64().ShouldBe(40);
        page.GetProperty("records").GetArrayLength().ShouldBe(25);
        page.GetProperty("records")[0].TryGetProperty("dimensions", out _).ShouldBeFalse();
        var id = page.GetProperty("records")[0].GetProperty("id").GetGuid();
        (await client.GetStringAsync("/admin/profiling/operations/" + id + "/content")).ShouldContain("weather:work");
        var next = await client.GetFromJsonAsync<JsonElement>("/admin/profiling/timeline/executions?" + frozen + "&cursor=" + Uri.EscapeDataString(page.GetProperty("nextCursor").GetString()));
        next.GetProperty("records").GetArrayLength().ShouldBe(15);
        next.GetProperty("hasMore").GetBoolean().ShouldBeFalse();
        (await client.GetFromJsonAsync<OperationProfilingTimeline>("/admin/profiling/timeline/data?kind=Job")).TotalCount.ShouldBe(1);
        (await client.GetFromJsonAsync<OperationProfilingTimeline>("/admin/profiling/timeline/data?nodeId=" + Guid.NewGuid())).TotalCount.ShouldBe(0);
    }

    /// <summary>Invalid windows and unbounded cluster selection fail with useful validation messages.</summary>
    /// <example><code>await suite.Timeline_InvalidInputs_ReturnBadRequest("resolution=year");</code></example>
    [Theory]
    [InlineData("resolution=year")]
    [InlineData("buckets=257")]
    [InlineData("buckets=invalid")]
    [InlineData("fromUtc=2026-10-12T13:00Z&toUtc=2026-10-12T12:00Z")]
    [InlineData("nodeId=invalid")]
    public async Task Timeline_InvalidInputs_ReturnBadRequest(string selection)
    {
        await using var app = await CreateAsync();
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/admin/profiling/timeline/data?" + selection);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var cluster = await client.GetAsync("/admin/profiling/timeline/executions?kind=Job");
        cluster.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>Retained execution IDs stay readable, but cannot split dashboard groups or survive old grouping selections.</summary>
    /// <example><code>await suite.Dimensions_LegacyExecutionIds_ClearGroupingAndKeepWorkloadChoices(false);</code></example>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dimensions_LegacyExecutionIds_ClearGroupingAndKeepWorkloadChoices(bool requestsOnly)
    {
        // Arrange
        await using var app = await CreateAsync();
        var profiler = app.Services.GetRequiredService<IOperationProfiler>();
        var kind = requestsOnly ? OperationProfilingKind.HttpRequest : OperationProfilingKind.Orchestration;
        var last = Guid.Empty;
        for (var index = 0; index < 2; index++)
        {
            using var operation = profiler.BeginOperation(new() { Key = "legacy:work", Kind = kind.ToString() });
            operation.SetDimension("cityId", "berlin");
            foreach (var key in ProfilingDimensionKeys.ExecutionIdentifiers) { operation.SetDimension(key.ToUpperInvariant(), Guid.NewGuid().ToString("D")); }

            operation.Complete(); last = operation.Id;
        }

        await app.Services.GetRequiredService<OperationProfilingWriterService>().TickAsync();
        var client = app.GetTestClient();
        var selection = "?key=legacy:work&kind=" + kind;

        // Act / Assert
        var discovery = await client.GetFromJsonAsync<OperationProfilingDimensionDiscovery>("/admin/profiling/operations/api/dimensions" + selection);
        discovery.Dimensions.ShouldHaveSingleItem().Key.ShouldBe("cityId");
        foreach (var key in ProfilingDimensionKeys.ExecutionIdentifiers)
        {
            var groups = await client.GetFromJsonAsync<OperationProfilingGroupPage>("/admin/profiling/operations/api/groups" + selection + "&groupBy=" + key.ToUpperInvariant());
            groups.TotalOperationCount.ShouldBe(2);
            groups.Groups.ShouldHaveSingleItem().Count.ShouldBe(2);
            var old = new OperationProfilingQuery
            {
                GroupingDimensions = [key.ToUpperInvariant(), "cityId"],
                GroupDimensions = [new() { Key = key, Value = new(ProfilingValueType.String, "old-instance") }],
            };
            var normalized = OperationProfilingDashboardQuery.Read(new QueryCollection(new Dictionary<string, StringValues> { ["query"] = JsonSerializer.Serialize(old) }));
            normalized.IsSuccess.ShouldBeTrue();
            normalized.Value.GroupingDimensions.ShouldBe(new[] { "cityId" });
            normalized.Value.GroupDimensions.ShouldBeEmpty();
        }

        var page = await client.GetStringAsync("/admin/profiling/" + (requestsOnly ? "requests" : "operations") + selection + "&groupBy=orchestration.instanceId");
        page.ShouldContain("data-execution-dimension-keys");
        page.ShouldContain("id=\"operation-group-by\" name=\"groupBy\" value=\"\"");
        var detail = await client.GetStringAsync("/admin/profiling/operations/" + last + "/content");
        foreach (var key in ProfilingDimensionKeys.ExecutionIdentifiers) { detail.ShouldContain(key.ToUpperInvariant()); }

        detail.Split("data-profiling-dimension-action=\"group\"").Length.ShouldBe(2); // Only the business dimension offers grouping.
    }

    /// <summary>The actual dashboard, refresh and analysis paths accept a fixed start with an empty end.</summary>
    /// <example><code>await suite.OpenEnd_ActualDashboardAndProvider_ReturnSuccessfulResponses(false);</code></example>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OpenEnd_ActualDashboardAndProvider_ReturnSuccessfulResponses(bool requestsOnly)
    {
        await using var app = await CreateAsync();
        var kind = requestsOnly ? OperationProfilingKind.HttpRequest : OperationProfilingKind.Job;
        Capture(app, "open-end:work", kind);
        await app.Services.GetRequiredService<OperationProfilingWriterService>().TickAsync();
        var from = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O");
        var parameters = "?kind=" + kind + "&fromUtc=" + Uri.EscapeDataString(from) + "&toUtc=";
        var client = app.GetTestClient();
        var list = "/admin/profiling/" + (requestsOnly ? "requests" : "operations");
        foreach (var path in new[] { list, list + "/content", "/admin/profiling/operations/api/groups", "/admin/profiling/operations/api/analysis", "/admin/profiling/operations/api/dimensions" })
        {
            var response = await client.GetAsync(path + parameters);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).ShouldNotContain("Invalid bounded profiling selector");
        }

        var groups = await client.GetFromJsonAsync<OperationProfilingGroupPage>("/admin/profiling/operations/api/groups" + parameters);
        groups.TotalOperationCount.ShouldBe(1);
    }

    /// <summary>Both views delete every instance in the selected filtered group, independently of page size, while preserving other cohorts and operation kinds.</summary>
    /// <example><code>await suite.DeleteGroup_CurrentFilters_RemoveAllMatchingInstances(false);</code></example>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeleteGroup_CurrentFilters_RemoveAllMatchingInstances(bool requestsOnly)
    {
        await using var app = await CreateAsync();
        var profiler = app.Services.GetRequiredService<IOperationProfiler>();
        var kind = requestsOnly ? OperationProfilingKind.HttpRequest : OperationProfilingKind.Service;
        var ids = new List<Guid>();
        for (var index = 0; index < 7; index++)
        {
            using var operation = profiler.BeginOperation(new() { Key = "catalog:load", Kind = kind.ToString() });
            operation.SetDimension("batch", index < 5 ? 1 : 2);
            using (var segment = profiler.BeginSegment("Load")) { segment.Complete(); }

            operation.Complete(); ids.Add(operation.Id);
        }

        var other = Capture(app, "catalog:other", kind);
        var otherKind = Capture(app, "catalog:load", requestsOnly ? OperationProfilingKind.Service : OperationProfilingKind.HttpRequest);
        var writer = app.Services.GetRequiredService<OperationProfilingWriterService>();
        await writer.TickAsync();
        var client = app.GetTestClient();
        var path = requestsOnly ? "/admin/profiling/requests" : "/admin/profiling/operations";
        var page = await client.GetStringAsync(path + "?limit=10&groupBy=batch&kind=" + kind);
        page.ShouldContain("data-operation-delete-group");
        page.ShouldContain("data-delete-url=\"" + path + "/groups/delete\"");
        page.ShouldContain("id=\"operation-delete-group-modal\"");
        var groups = await client.GetFromJsonAsync<OperationProfilingGroupPage>("/admin/profiling/operations/api/groups?groupBy=batch&kind=" + kind);
        var group = groups.Groups.Single(value => value.Count == 5);
        var selector = new OperationProfilingQuery
        {
            Key = group.Key, Kind = group.Kind, FromUtc = groups.Boundary.FromUtc, ToUtc = groups.Boundary.ToUtc, PageSize = 1,
            GroupDimensions = [new() { Key = "batch", Value = group.Dimensions.Single().Value }],
        };
        var deletion = client.PostAsJsonAsync(path + "/groups/delete", new ProfilingDashboardGroupDeleteRequest { Confirmed = true, Selection = selector });
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!deletion.IsCompleted)
        {
            await writer.TickAsync(deadline.Token);
            await Task.Delay(10, deadline.Token);
        }

        var response = await deletion;
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ProfilingClearResult>();
        result.RemovedOperationCount.ShouldBe(5);
        result.RemovedSegmentSummaryCount.ShouldBe(5);
        var store = app.Services.GetRequiredService<IOperationProfilingStore>();
        foreach (var id in ids.Take(5)) { (await store.FindAsync(id)).Value.ShouldBeNull(); }

        foreach (var id in ids.Skip(5).Append(other).Append(otherKind)) { (await store.FindAsync(id)).Value.ShouldNotBeNull(); }

        var remaining = await client.GetFromJsonAsync<OperationProfilingGroupPage>("/admin/profiling/operations/api/groups?groupBy=batch&kind=" + kind);
        remaining.TotalOperationCount.ShouldBe(2);
        remaining.TotalGroupCount.ShouldBe(1);
    }

    /// <summary>Deletion rejects unconfirmed and broad targets before changing stored history.</summary>
    /// <example><code>await suite.DeleteGroup_InvalidOrUnconfirmedSelection_ReturnsValidation("key");</code></example>
    [Theory]
    [InlineData("confirmation")]
    [InlineData("key")]
    [InlineData("kind")]
    [InlineData("window")]
    [InlineData("cursor")]
    public async Task DeleteGroup_InvalidOrUnconfirmedSelection_ReturnsValidation(string invalid)
    {
        await using var app = await CreateAsync();
        var id = Capture(app, "catalog:load", OperationProfilingKind.Service);
        await app.Services.GetRequiredService<OperationProfilingWriterService>().TickAsync();
        var query = new OperationProfilingQuery { Key = "catalog:load", Kind = "Service", FromUtc = DateTimeOffset.UtcNow.AddMinutes(-15), ToUtc = DateTimeOffset.UtcNow.AddMinutes(1) };
        query = invalid switch
        {
            "key" => query with { Key = null }, "kind" => query with { Kind = null },
            "window" => query with { FromUtc = null, ToUtc = null }, "cursor" => query with { Cursor = "page" }, _ => query,
        };
        var response = await app.GetTestClient().PostAsJsonAsync("/admin/profiling/operations/groups/delete", new ProfilingDashboardGroupDeleteRequest { Confirmed = invalid != "confirmation", Selection = query });
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await app.Services.GetRequiredService<IOperationProfilingStore>().FindAsync(id)).Value.ShouldNotBeNull();
    }

    /// <summary>Checks both lists offer one operation and independent comparison conditions in a dialog.</summary>
    /// <example><code>await test.Views_StartTimestampAndComparisonKeys_RenderForBothLists(false);</code></example>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Views_StartTimestampAndComparisonKeys_RenderForBothLists(bool requestsOnly)
    {
        await using var app = await CreateAsync();
        Capture(app, "catalog:list", OperationProfilingKind.HttpRequest);
        Capture(app, "catalog:details", OperationProfilingKind.HttpRequest);
        await app.Services.GetRequiredService<OperationProfilingWriterService>().TickAsync();
        var path = requestsOnly ? "/admin/profiling/requests" : "/admin/profiling/operations";
        var client = app.GetTestClient();
        var page = await client.GetStringAsync(path);
        var groups = await client.GetFromJsonAsync<OperationProfilingGroupPage>("/admin/profiling/operations/api/groups");

        groups.TotalOperationCount.ShouldBe(2);
        page.ShouldContain("id=\"operation-detail-modal\"");
        page.ShouldContain("data-operation-detail-url=\"" + path + "/");
        page.ShouldContain("aria-label=\"Execution details\"");
        page.ShouldContain("Execution / started UTC");
        page.ShouldNotContain("Execution / completed UTC");
        foreach (var group in groups.Groups)
        {
            page.ShouldContain("datetime=\"" + OperationProfilingViewModelBuilder.Utc(group.Representative.StartedUtc) + "\"");
        }

        page.ShouldContain("id=\"operation-comparison-modal\"");
        page.ShouldContain("<select id=\"operation-comparison-key\"");
        page.ShouldContain("data-key=\"catalog:list\"");
        page.ShouldContain("data-key=\"catalog:details\"");
        page.ShouldContain("data-operation-comparison-open");
        page.ShouldContain("id=\"operation-comparison-baseline-from\"");
        page.ShouldContain("id=\"operation-comparison-candidate-from\"");
        page.ShouldContain("id=\"operation-comparison-baseline-node\"");
        page.ShouldContain("id=\"operation-comparison-candidate-node\"");
        page.ShouldContain("id=\"operation-comparison-results\"");
        page.ShouldNotContain("operation-baseline-key");
        page.ShouldNotContain("operation-candidate-key");
        page.ShouldContain("col-md-3\"><label for=\"operation-correlation\"");
    }

    /// <summary>Initial rendering, refresh and JSON apply identical selection and HTTP-kind rules.</summary>
    [Theory]
    [InlineData("Slow")]
    [InlineData("Recent")]
    [InlineData("ByCount")]
    public async Task Views_AllModes_ShareCountsAndRestrictRequests(string mode)
    {
        await using var app = await CreateAsync();
        var first = Capture(app, "catalog", OperationProfilingKind.HttpRequest);
        Capture(app, "CATALOG", OperationProfilingKind.HttpRequest);
        var service = Capture(app, "catalog", OperationProfilingKind.Service);
        await app.Services.GetRequiredService<OperationProfilingWriterService>().TickAsync();
        var client = app.GetTestClient();
        var parameters = $"?view={mode}&kind=Service&limit=250";
        var page = await client.GetStringAsync("/admin/profiling/requests" + parameters);
        var content = await client.GetStringAsync("/admin/profiling/requests/content" + parameters);
        var groups = await client.GetFromJsonAsync<OperationProfilingGroupPage>("/admin/profiling/operations/api/groups?kind=HttpRequest&view=" + mode + "&limit=250");

        page.ShouldContain("title=\"View retained executions matching this group\">2 executions</a>");
        content.ShouldContain("title=\"View retained executions matching this group\">2 executions</a>");
        groups.TotalOperationCount.ShouldBe(2);
        groups.TotalGroupCount.ShouldBe(1);
        groups.Groups.Single().Count.ShouldBe(2);
        content.ShouldContain("Percent of operation duration");
        content.ShouldContain("UTC");
        content.ShouldContain("bi bi-info-circle");
        content.ShouldNotContain("bi bi-eye");
        content.ShouldContain("Duration breakdown");
        content.ShouldNotContain("Wall time");
        page.ShouldContain("<select id=\"operation-node\"");
        page.ShouldContain("<select id=\"operation-kind\"");
        page.ShouldContain("id=\"operation-health-modal\"");
        page.ShouldContain("aria-label=\"Capture health\"");
        page.ShouldContain("Compare executions");
        page.ShouldContain("<option value=\"250\"");
        page.ShouldContain("All nodes");
        page.ShouldNotContain("id=\"operation-outcomes\"");
        page.ShouldNotContain("id=\"operation-group\"");
        page.ShouldNotContain("id=\"operation-id\"");
        page.ShouldContain("/admin/profiling/operations");
        (await client.GetAsync("/admin/profiling/requests/" + service)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync("/admin/profiling/requests/" + first)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/admin/profiling/requests/" + service + "/content")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var detail = await client.GetStringAsync("/admin/profiling/requests/" + first + "/content");
        detail.ShouldContain("Diagnostics ID");
        detail.ShouldContain("data-operation-detail");
        detail.ShouldNotContain("<html");
        var exact = await client.GetFromJsonAsync<OperationProfilingRecord>("/admin/profiling/operations/api/records/" + first + "?outcomes=Failed&fromUtc=2000-01-01T00:00:00Z&toUtc=2000-01-02T00:00:00Z");
        exact.Id.ShouldBe(first);
    }

    /// <summary>Only explicitly persisted records are visible and initial/content reads leave pending state untouched.</summary>
    [Fact]
    public async Task Reads_WithPendingCompletions_DoNotFlushOrExposeThem()
    {
        await using var app = await CreateAsync();
        var id = Capture(app, "pending", OperationProfilingKind.Service);
        var queries = app.Services.GetRequiredService<IOperationProfilingQueryService>();
        var before = queries.GetHealth();
        before.QueueRecords.ShouldBe(1);
        var client = app.GetTestClient();

        foreach (var path in new[] { "/operations", "/operations/content", "/requests", "/operations/api/groups", "/operations/api/records", "/operations/api/dimensions" })
        {
            (await client.GetAsync("/admin/profiling" + path)).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        (await client.GetAsync("/admin/profiling/operations/" + id)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var missing = await client.GetAsync("/admin/profiling/operations/api/records/" + id);
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await missing.Content.ReadAsStringAsync()).ShouldContain("pending");
        var after = queries.GetHealth();
        after.QueueRecords.ShouldBe(before.QueueRecords);
        after.LastSuccessfulFlushUtc.ShouldBe(before.LastSuccessfulFlushUtc);
    }

    /// <summary>Provider history stays queryable when capture is disabled and the landing view reflects capability.</summary>
    [Theory]
    [InlineData(true, "/admin/profiling/operations")]
    [InlineData(false, "/admin/profiling/runtime")]
    public async Task Landing_CaptureCapabilities_SelectViewAndPreserveHistory(bool capture, string target)
    {
        await using var app = await CreateAsync(capture);
        var client = app.GetTestClient();
        var response = await client.GetAsync("/admin/profiling?key=catalog");
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ToString().ShouldBe(target + "?key=catalog");
        (await client.GetAsync("/admin/profiling/operations")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/admin/profiling/runtime")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>Invalid selections return consistent typed errors without leaking metadata or provider exceptions.</summary>
    [Fact]
    public async Task Selection_InvalidLimit_HtmlAndJsonReturnValidation()
    {
        await using var app = await CreateAsync();
        var client = app.GetTestClient();
        foreach (var path in new[] { "/operations", "/operations/content", "/operations/api/groups", "/operations/api/records", "/operations/api/analysis" })
        {
            var response = await client.GetAsync("/admin/profiling" + path + "?limit=501");
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await response.Content.ReadAsStringAsync()).ShouldContain("Invalid bounded profiling selector");
        }
    }

    /// <summary>All new routes inherit the shared dashboard authorization policy.</summary>
    [Fact]
    public async Task Routes_WithPolicy_AllInheritAuthorizationAndExcludeInternalJson()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        builder.Services.AddAuthorization();
        await using var app = builder.Build();
        var options = new DashboardEndpointsOptionsBuilder().WithGroupPath("/admin").Authorize(authorization => authorization.RequirePolicy("admins")).Build();
        new ProfilingEndpoints(options).Map(app);
        var routes = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>()
            .Where(route => route.RoutePattern.RawText.Contains("/profiling/operations") || route.RoutePattern.RawText.Contains("/profiling/requests")).ToArray();

        routes.Length.ShouldBe(17);
        foreach (var route in routes)
        {
            route.Metadata.GetOrderedMetadata<IAuthorizeData>().ShouldContain(value => value.Policy == "admins");
        }
    }

    /// <summary>Typed selector overrides can clear filters, reject duplicates, and force Requests kind.</summary>
    [Fact]
    public void Selector_BasicOverrides_ClearAdvancedValuesAndRejectDuplicates()
    {
        var input = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["query"] = JsonSerializer.Serialize(new OperationProfilingQuery { Key = "old", Kind = "Service", GroupingDimensions = ["region"], ToUtc = DateTimeOffset.UtcNow }),
            ["key"] = "", ["groupBy"] = "", ["toUtc"] = "",
        });
        var sut = OperationProfilingDashboardQuery.Read(input, requestsOnly: true);
        sut.IsSuccess.ShouldBeTrue();
        sut.Value.Key.ShouldBeNull();
        sut.Value.KeyContains.ShouldBeNull();
        sut.Value.Kind.ShouldBe("HttpRequest");
        sut.Value.GroupingDimensions.ShouldBeEmpty();
        sut.Value.ToUtc.ShouldBeNull();
        OperationProfilingDashboardQuery.Read(new QueryCollection(new Dictionary<string, StringValues> { ["key"] = new StringValues(["one", "two"]) })).IsFailure.ShouldBeTrue();
    }

    /// <summary>The initial view is Recent and basic key text selects a substring rather than an exact logical key.</summary>
    /// <example><code>await suite.Selection_DefaultRecentAndContains_AppliesToBothViews();</code></example>
    [Fact]
    public async Task Selection_DefaultRecentAndContains_AppliesToBothViews()
    {
        OperationProfilingDashboardQuery.Read(new QueryCollection()).Value.View.ShouldBe(OperationProfilingView.Recent);
        await using var app = await CreateAsync();
        Capture(app, "catalog:load", OperationProfilingKind.HttpRequest);
        Capture(app, "CATALOG:list", OperationProfilingKind.HttpRequest);
        Capture(app, "other", OperationProfilingKind.HttpRequest);
        await app.Services.GetRequiredService<OperationProfilingWriterService>().TickAsync();
        var client = app.GetTestClient();

        var page = await client.GetFromJsonAsync<OperationProfilingGroupPage>("/admin/profiling/operations/api/groups?key=ALOG:");
        page.TotalOperationCount.ShouldBe(2);
        foreach (var view in new[] { "operations", "requests" })
        {
            var html = await client.GetStringAsync("/admin/profiling/" + view + "?key=ALOG:");
            html.ShouldContain("catalog:load");
            html.ShouldContain("CATALOG:list");
            html.ShouldNotContain(">other<");
        }
    }

    /// <summary>The Runtime clear-all action fences and removes retained HTTP and non-HTTP operations while allowing fresh captures.</summary>
    /// <example><code>await suite.ClearAll_RuntimeAction_RemovesOperationsAndRequests();</code></example>
    [Fact]
    public async Task ClearAll_RuntimeAction_RemovesOperationsAndRequests()
    {
        await using var app = await CreateAsync();
        var service = Capture(app, "clear:service", OperationProfilingKind.Service);
        var request = Capture(app, "clear:http", OperationProfilingKind.HttpRequest);
        var writer = app.Services.GetRequiredService<OperationProfilingWriterService>();
        await writer.TickAsync();
        var pending = Capture(app, "clear:pending", OperationProfilingKind.Service);
        var responseTask = app.GetTestClient().PostAsJsonAsync("/admin/profiling/runtime/clear", new ProfilingDashboardClearRequest(true));
        for (var attempt = 0; attempt < 200 && !responseTask.IsCompleted; attempt++)
        {
            await writer.TickAsync();
            await Task.Delay(5);
        }

        using var response = await responseTask.WaitAsync(TimeSpan.FromSeconds(5));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ProfilingClearResult>();
        result.DataSet.ShouldBe(ProfilingDataSet.All);
        var provider = app.Services.GetRequiredService<IProfilingStorageProvider>();
        for (var attempt = 0; attempt < 5; attempt++) { await provider.ResumeMaintenanceAsync(new ProfilingMaintenanceRequest()); }

        foreach (var id in new[] { service, request, pending }) { (await provider.Operations.FindAsync(id)).Value.ShouldBeNull(); }

        var fresh = Capture(app, "clear:fresh", OperationProfilingKind.HttpRequest);
        await writer.TickAsync();
        (await provider.Operations.FindAsync(fresh)).Value.ShouldNotBeNull();
    }

    /// <summary>Host-provided dimension values render encoded and exact details expose aggregate segment paths.</summary>
    [Fact]
    public async Task Detail_WithNestedSegments_EncodesMetadataAndShowsAggregates()
    {
        await using var app = await CreateAsync();
        const string correlationId = "lookup&<script>\"quoted\"</script>";
        var id = Capture(app, "catalog", OperationProfilingKind.Service, correlationId);
        await app.Services.GetRequiredService<OperationProfilingWriterService>().TickAsync();
        var html = await app.GetTestClient().GetStringAsync("/admin/profiling/operations/" + id);
        html.ShouldContain("&lt;script&gt;");
        html.ShouldNotContain("<script>metadata");
        html.ShouldContain("Load");
        html.ShouldContain("Compute");
        html.ShouldContain("invocations");
        html.ShouldContain("UTC");
        html.ShouldContain("/admin/logentries?");
        html.ShouldContain("correlationId=" + Uri.EscapeDataString(correlationId));
        html.ShouldContain("data-dashboard-copy=\"lookup&amp;&lt;script&gt;&quot;quoted&quot;&lt;/script&gt;\"");
        html.ShouldNotContain(correlationId);
        html.ShouldNotContain("data-runtime-overlay=");
        html.ShouldContain("aria-label=\"Segment timing display\"");
        html.ShouldContain("data-segment-timing-mode=\"percentage\"");
        html.ShouldContain("data-percentage=");
    }

    /// <summary>Segment shares use the owner duration, retain values above 100%, and avoid division by zero.</summary>
    /// <example><code>suite.Percentage_OwnerDuration_FormatsObservedShare(25, 100, "25%");</code></example>
    [Theory]
    [InlineData(25, 100, "25%")]
    [InlineData(250, 100, "250%")]
    [InlineData(0, 100, "0%")]
    [InlineData(10, 0, "—")]
    public void Percentage_OwnerDuration_FormatsObservedShare(double segmentMs, double operationMs, string expected)
    {
        OperationProfilingViewModelBuilder.Percentage(TimeSpan.FromMilliseconds(segmentMs), TimeSpan.FromMilliseconds(operationMs)).ShouldBe(expected);
    }

    /// <summary>Group labels remove behavior plumbing, preserve request paths and support custom work names.</summary>
    /// <example><code>suite.GroupName_RecordedKeys_FormatsDisplayOnly("job:catalog_cleanup", "Job", "catalog cleanup");</code></example>
    [Theory]
    [InlineData("messaging:OrderCreated:handler:OrderMessageHandler", "MessageHandler", "OrderMessageHandler")]
    [InlineData("queueing:OrderCreated:handler:OrderQueueHandler", "QueueHandler", "OrderQueueHandler")]
    [InlineData("MESSAGING:OrderCreated:HANDLER:OrderMessageHandler", "messagehandler", "OrderMessageHandler")]
    [InlineData("notifier:OrderCreated:handler:OrderNotificationHandler", "NotifierHandler", "OrderNotificationHandler")]
    [InlineData("job:catalog_cleanup", "Job", "catalog cleanup")]
    [InlineData("orchestration:OrderProcessing", "Orchestration", "OrderProcessing")]
    [InlineData("activeentity:Order:FindAll", "ActiveEntity", "Order / FindAll")]
    [InlineData("repository:Order:FindAll", "Repository", "Order / FindAll")]
    [InlineData("filestorage:read", "FileStorage", "read")]
    [InlineData("documentstorage:Archive:find-page", "DocumentStorage", "Archive / find page")]
    [InlineData("blobstorage:get_properties", "BlobStorage", "get properties")]
    [InlineData("catalog:city-review", "Service", "catalog / city review")]
    [InlineData("/api/order-items/{order_id}", "HttpRequest", "/api/order-items/{order_id}")]
    [InlineData("custom-request-key", "HttpRequest", "custom-request-key")]
    public void GroupName_RecordedKeys_FormatsDisplayOnly(string key, string kind, string expected)
    {
        OperationProfilingViewModelBuilder.GroupName(key, kind).ShouldBe(expected);
    }

    /// <summary>Readable headings retain exact keys for lookup and complete counts in every list mode.</summary>
    /// <example><code>await suite.Views_FriendlyGroupHeadings_PreserveRecordedKeys("Recent");</code></example>
    [Theory]
    [InlineData("Recent")]
    [InlineData("Slow")]
    [InlineData("ByCount")]
    public async Task Views_FriendlyGroupHeadings_PreserveRecordedKeys(string mode)
    {
        await using var app = await CreateAsync();
        var profiler = app.Services.GetRequiredService<IOperationProfiler>();
        var key = "messaging:OrderCreated:handler:OrderMessageHandler";
        for (var index = 0; index < 2; index++)
        {
            using var operation = profiler.BeginOperation(new() { Key = key, Kind = "MessageHandler" });
            operation.Complete();
        }

        await app.Services.GetRequiredService<OperationProfilingWriterService>().TickAsync();
        var client = app.GetTestClient();
        var page = await client.GetStringAsync("/admin/profiling/operations?view=" + mode);
        var groups = await client.GetFromJsonAsync<OperationProfilingGroupPage>("/admin/profiling/operations/api/groups?view=" + mode);

        page.ShouldContain("title=\"Key: " + key + "\"><span>Message handler</span> &#183; <strong>OrderMessageHandler</strong>");
        page.ShouldContain("title=\"View retained executions matching this group\">2 executions</a>");
        page.ShouldContain("data-key=\"" + key + "\" data-kind=\"MessageHandler\" title=\"" + key + "\">Message handler &#183; OrderMessageHandler</option>");
        page.ShouldNotContain("<strong>" + key + "</strong>");
        groups.Groups.Single().Key.ShouldBe(key);
        groups.Groups.Single().Kind.ShouldBe("MessageHandler");
        groups.Groups.Single().Count.ShouldBe(2);
    }

    /// <summary>One occupied query budget produces the same Busy status in both render paths and JSON.</summary>
    [Fact]
    public async Task Queries_WhenCapacityOccupied_HtmlAndJsonReturnBusy()
    {
        await using var app = await CreateAsync();
        var queries = app.Services.GetRequiredService<IOperationProfilingQueryService>();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;
        var holds = Enumerable.Range(0, 4).Select(_ => queries.BuildViewAsync<int>(async (_, token) =>
        {
            if (Interlocked.Increment(ref count) == 4) { entered.TrySetResult(); }

            await release.Task.WaitAsync(token);
            return Result<int>.Success(1);
        })).ToArray();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        try
        {
            foreach (var path in new[] { "/operations", "/operations/content", "/operations/api/groups" })
            {
                (await app.GetTestClient().GetAsync("/admin/profiling" + path)).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
            }
        }
        finally
        {
            release.TrySetResult();
            await Task.WhenAll(holds);
        }
    }

    /// <summary>Comparison uses the same basic controls on both sides, overriding advanced kind values.</summary>
    [Fact]
    public async Task Compare_WithBasicKindOverride_SelectsSameKindForBothKeys()
    {
        await using var app = await CreateAsync();
        Capture(app, "baseline", OperationProfilingKind.HttpRequest);
        Capture(app, "candidate", OperationProfilingKind.HttpRequest);
        Capture(app, "candidate", OperationProfilingKind.Service);
        await app.Services.GetRequiredService<OperationProfilingWriterService>().TickAsync();
        var candidate = Uri.EscapeDataString(JsonSerializer.Serialize(new OperationProfilingQuery { Key = "candidate", Kind = "Service" }));
        var comparison = await app.GetTestClient().GetFromJsonAsync<OperationProfilingComparison>("/admin/profiling/operations/api/analysis?key=baseline&kind=HttpRequest&candidate=" + candidate);
        comparison.Baseline.Count.ShouldBe(1);
        comparison.Candidate.Count.ShouldBe(1);
    }

    /// <summary>Independent selectors compare the same work without leaking time, node, version or dimension conditions between sides.</summary>
    /// <example><code>await test.Compare_SameOperationIndependentConditions_SelectsSeparateSamples("dimensions");</code></example>
    [Theory]
    [InlineData("dimensions")]
    [InlineData("node")]
    [InlineData("time")]
    [InlineData("version")]
    public async Task Compare_SameOperationIndependentConditions_SelectsSeparateSamples(string condition)
    {
        await using var app = await CreateAsync();
        Capture(app, "catalog:list", OperationProfilingKind.HttpRequest, category: "baseline");
        Capture(app, "catalog:list", OperationProfilingKind.HttpRequest, category: "comparison");
        Capture(app, "catalog:list", OperationProfilingKind.HttpRequest, category: "comparison");
        Capture(app, "other-work", OperationProfilingKind.HttpRequest, category: "comparison");
        await app.Services.GetRequiredService<OperationProfilingWriterService>().TickAsync();
        var baseline = new OperationProfilingQuery
        {
            Key = "catalog:list", Kind = "HttpRequest", FromUtc = DateTimeOffset.UtcNow.AddMinutes(-15), ToUtc = DateTimeOffset.UtcNow.AddMinutes(1),
            Dimensions = [new() { Key = "category", Value = new(ProfilingValueType.String, "baseline") }],
        };
        var candidate = baseline with { Dimensions = [new() { Key = "category", Value = new(ProfilingValueType.String, "comparison") }] };
        candidate = condition switch
        {
            "node" => candidate with { NodeId = Guid.NewGuid() },
            "time" => candidate with { FromUtc = baseline.FromUtc.Value.AddMinutes(-15), ToUtc = baseline.FromUtc },
            "version" => candidate with { ApplicationVersion = "unrecorded-version" },
            _ => candidate,
        };
        var url = "/admin/profiling/operations/api/analysis?query=" + Uri.EscapeDataString(JsonSerializer.Serialize(baseline))
            + "&candidate=" + Uri.EscapeDataString(JsonSerializer.Serialize(candidate));

        var response = await app.GetTestClient().GetAsync(url);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var identities = json.RootElement.GetProperty("baseline").GetProperty("segments").EnumerateArray()
            .Select(segment => segment.GetProperty("comparisonKey").GetString()).ToArray();
        identities.ShouldContain("4:LOAD7:COMPUTE");
        identities.Distinct().Count().ShouldBe(identities.Length);
        var comparison = await response.Content.ReadFromJsonAsync<OperationProfilingComparison>();

        comparison.Baseline.Count.ShouldBe(1);
        comparison.Candidate.Count.ShouldBe(condition == "dimensions" ? 2 : 0);
        comparison.Baseline.Segments.ShouldContain(segment => segment.Path.Components.SequenceEqual(new[] { "Load", "Compute" }));
        if (condition == "time") { comparison.Candidate.Boundary.ToUtc.ShouldBe(baseline.FromUtc); }
    }

    /// <summary>Discovery retains missing counts while both dashboard views exclude missing grouping dimensions and preserve exact statistics.</summary>
    /// <example><code>await suite.Dimensions_AllRetainedRows_DiscoveryGroupingAndDetailActionsAgree(false);</code></example>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dimensions_AllRetainedRows_DiscoveryGroupingAndDetailActionsAgree(bool requestsOnly)
    {
        await using var app = await CreateAsync();
        var profiler = app.Services.GetRequiredService<IOperationProfiler>();
        var kind = requestsOnly ? OperationProfilingKind.HttpRequest : OperationProfilingKind.Service;
        var last = Guid.Empty;
        for (var index = 0; index < 35; index++)
        {
            using var operation = profiler.BeginOperation(new() { Key = "catalog:search", Kind = kind.ToString() });
            if (index < 15) { operation.SetDimension("cache", "hit"); }
            else if (index < 30) { operation.SetDimension("CACHE", "miss"); }
            else if (index < 34) { operation.SetDimension("cache", ""); }

            using (var load = profiler.BeginSegment("Load")) { load.SetDimension("provider", "sql"); load.Complete(); }

            operation.Complete(); last = operation.Id;
        }

        Capture(app, "different", OperationProfilingKind.Service);
        await app.Services.GetRequiredService<OperationProfilingWriterService>().TickAsync();
        var client = app.GetTestClient();
        var selection = "?key=catalog:search&kind=" + kind + "&limit=10";
        var discovery = await client.GetFromJsonAsync<OperationProfilingDimensionDiscovery>("/admin/profiling/operations/api/dimensions" + selection);
        discovery.OperationCount.ShouldBe(35);
        var choices = discovery.Dimensions.Single();
        choices.PresentCount.ShouldBe(34);
        choices.MissingCount.ShouldBe(1);
        choices.Values.Single(value => value.Value.Scalar == "hit").Count.ShouldBe(15);
        discovery.SegmentPaths.Single().Count.ShouldBe(35);

        var grouped = await client.GetFromJsonAsync<OperationProfilingGroupPage>("/admin/profiling/operations/api/groups" + selection + "&groupBy=cache&view=ByCount");
        grouped.TotalGroupCount.ShouldBe(3);
        grouped.TotalOperationCount.ShouldBe(34);
        grouped.Groups.ShouldAllBe(group => group.Dimensions.Count == 1);
        foreach (var group in grouped.Groups)
        {
            group.Distribution.ShouldNotBeNull();
            group.Distribution.Count.ShouldBe(group.Count);
            var value = group.Dimensions.SingleOrDefault()?.Value;
            var query = new OperationProfilingQuery { Key = group.Key, Kind = group.Kind, FromUtc = grouped.Boundary.FromUtc, ToUtc = grouped.Boundary.ToUtc,
                GroupDimensions = [new() { Key = "cache", Value = value, Operator = value is null ? ProfilingDimensionOperator.Missing : ProfilingDimensionOperator.Equal }] };
            var exact = await client.GetFromJsonAsync<OperationProfilingDistribution>("/admin/profiling/operations/api/analysis?query=" + Uri.EscapeDataString(JsonSerializer.Serialize(query)));
            group.Distribution.P50Milliseconds.ShouldBe(exact.P50Milliseconds);
            group.Distribution.P95Milliseconds.ShouldBe(exact.P95Milliseconds);
        }

        var html = await client.GetStringAsync("/admin/profiling/" + (requestsOnly ? "requests" : "operations") + selection + "&groupBy=cache");
        html.ShouldContain("Group by operation dimension");
        html.ShouldContain("data-dashboard-date-range-from=\"operation-from\"");
        html.ShouldContain("data-dashboard-date-range-from=\"operation-comparison-baseline-from\"");
        html.ShouldContain("data-dashboard-date-range-from=\"operation-comparison-candidate-from\"");
        html.ShouldContain("Segment filter path");
        html.ShouldContain("data-operation-comparison-selector");
        html.ShouldContain("operation-group-statistics");
        html.ShouldContain("(empty string)");
        html.ShouldNotContain("cache = Missing");
        var detail = await client.GetStringAsync("/admin/profiling/operations/" + last + "/content");
        detail.ShouldContain("data-dimension-segment-path");
        detail.ShouldContain("data-profiling-dimension-action=\"filter\"");
        detail.ShouldContain("data-profiling-dimension-action=\"baseline\"");
        detail.ShouldContain("data-profiling-dimension-action=\"candidate\"");
    }

    /// <summary>Discovery validates its scope and reuses dashboard admission and safe error responses.</summary>
    /// <example><code>await suite.Dimensions_InvalidScope_ReturnsValidation();</code></example>
    [Theory]
    [InlineData("scope=invalid")]
    [InlineData("scope=Segment")]
    [InlineData("dimensionKey=")]
    public async Task Dimensions_InvalidScope_ReturnsValidation(string parameters)
    {
        await using var app = await CreateAsync();
        var response = await app.GetTestClient().GetAsync("/admin/profiling/operations/api/dimensions?" + parameters);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("Invalid bounded profiling selector");
    }

    private static Guid Capture(WebApplication app, string key, OperationProfilingKind kind, string correlationId = null, string category = "<script>metadata</script>")
    {
        var profiler = app.Services.GetRequiredService<IOperationProfiler>();
        using var operation = profiler.BeginOperation(new OperationProfilingStartRequest { Key = key, Kind = kind.ToString(), CorrelationId = correlationId });
        operation.SetDimension("category", category);
        if (kind == OperationProfilingKind.HttpRequest) { operation.SetHttpMetadata(new() { Method = "GET", StatusCode = 200 }); }

        for (var index = 0; index < 2; index++)
        {
            using var segment = profiler.BeginSegment("Load");
            using (var child = profiler.BeginSegment("Compute")) { child.Complete(); }

            segment.Complete();
        }

        operation.Complete();
        return operation.Id;
    }

    private static async Task<WebApplication> CreateAsync(bool capture = true)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddProfiling(options => options.Enabled()).WithOperationProfiling(options => options.Enabled(capture).FlushInterval(TimeSpan.FromMinutes(1)));
        builder.Services.AddDashboard(options => options.AllowAnonymous().WithGroupPath("/admin"));
        var app = builder.Build();
        app.MapEndpoints();
        if (capture)
        {
            // Activate the writer lease before the background host startup can race fixture captures.
            await app.Services.GetRequiredService<OperationProfilingWriterService>().TickAsync();
            app.Services.GetRequiredService<OperationProfilingCompletionQueue>().Snapshot().Active.ShouldBeTrue();
        }

        await app.StartAsync();
        return app;
    }
}
