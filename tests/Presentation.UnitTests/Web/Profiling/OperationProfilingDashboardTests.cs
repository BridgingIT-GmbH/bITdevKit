// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Profiling;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using BridgingIT.DevKit.Presentation.Web.Profiling.Dashboard;
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
        var parameters = $"?view={mode}&kind=Service&limit=10";
        var page = await client.GetStringAsync("/admin/profiling/requests" + parameters);
        var content = await client.GetStringAsync("/admin/profiling/requests/content" + parameters);
        var groups = await client.GetFromJsonAsync<OperationProfilingGroupPage>("/admin/profiling/operations/api/groups?kind=HttpRequest&view=" + mode);

        page.ShouldContain("2 retained matching executions");
        content.ShouldContain("2 retained matching executions");
        groups.TotalOperationCount.ShouldBe(2);
        groups.TotalGroupCount.ShouldBe(1);
        groups.Groups.Single().Count.ShouldBe(2);
        content.ShouldContain("Exclusive wall-time share");
        content.ShouldContain("UTC");
        page.ShouldContain("/admin/profiling/operations");
        (await client.GetAsync("/admin/profiling/requests/" + service)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync("/admin/profiling/requests/" + first)).StatusCode.ShouldBe(HttpStatusCode.OK);
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

        foreach (var path in new[] { "/operations", "/operations/content", "/requests", "/operations/api/groups", "/operations/api/records" })
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
            var response = await client.GetAsync("/admin/profiling" + path + "?limit=201");
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

        routes.Length.ShouldBe(14);
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
            ["query"] = JsonSerializer.Serialize(new OperationProfilingQuery { Key = "old", Kind = "Service", GroupingDimensions = ["region"] }),
            ["key"] = "", ["groupBy"] = "",
        });
        var sut = OperationProfilingDashboardQuery.Read(input, requestsOnly: true);
        sut.IsSuccess.ShouldBeTrue();
        sut.Value.Key.ShouldBeNull();
        sut.Value.Kind.ShouldBe("HttpRequest");
        sut.Value.GroupingDimensions.ShouldBeEmpty();
        OperationProfilingDashboardQuery.Read(new QueryCollection(new Dictionary<string, StringValues> { ["key"] = new StringValues(["one", "two"]) })).IsFailure.ShouldBeTrue();
    }

    /// <summary>Host-provided dimension values render encoded and exact details expose aggregate segment paths.</summary>
    [Fact]
    public async Task Detail_WithNestedSegments_EncodesMetadataAndShowsAggregates()
    {
        await using var app = await CreateAsync();
        var id = Capture(app, "catalog", OperationProfilingKind.Service);
        await app.Services.GetRequiredService<OperationProfilingWriterService>().TickAsync();
        var html = await app.GetTestClient().GetStringAsync("/admin/profiling/operations/" + id);
        html.ShouldContain("&lt;script&gt;");
        html.ShouldNotContain("<script>metadata");
        html.ShouldContain("Load");
        html.ShouldContain("Compute");
        html.ShouldContain("invocations");
        html.ShouldContain("UTC");
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

    private static Guid Capture(WebApplication app, string key, OperationProfilingKind kind)
    {
        var profiler = app.Services.GetRequiredService<IOperationProfiler>();
        using var operation = profiler.BeginOperation(key, kind);
        operation.SetDimension("category", "<script>metadata</script>");
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
        await app.StartAsync();
        return app;
    }
}
