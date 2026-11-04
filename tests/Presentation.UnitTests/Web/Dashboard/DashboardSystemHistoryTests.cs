namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Dashboard;

using System.Net.Http.Json;
using System.Text.Json;
using BridgingIT.DevKit.Presentation;
using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

public class DashboardSystemHistoryTests
{
    [Fact]
    public void Snapshot_ActualIntervals_UseCompletedRequestsAndPreserveIdleLatencyGaps()
    {
        var clock = new Clock();
        var runtime = Substitute.For<IDotNetMetricsSnapshotService>();
        runtime.GetSnapshot().Returns(new DotNetMetricsSnapshotModel { CpuUsagePercent = 20, WorkingSetMb = 100, PrivateMemoryMb = 80, ManagedMemoryMb = 40, HeapSizeMb = 30 });
        var requests = Substitute.For<IAspNetMetricsSnapshotService>();
        var http = new AspNetMetricsSnapshotModel();
        requests.GetSnapshot().Returns(_ => http);
        using var sut = new DashboardSystemHistory(runtime, requests, clock);
        Last(sut).GetProperty("latency").ValueKind.ShouldBe(JsonValueKind.Null);
        Last(sut).GetProperty("started").ValueKind.ShouldBe(JsonValueKind.Null);

        http = new() { TotalRequests = 6, Status2xx = 3, Status5xx = 1, FailedRequests = 1, TotalLatencyMs = 100 };
        clock.Advance(TimeSpan.FromSeconds(2));

        var values = Last(sut);
        values.GetProperty("started").GetDouble().ShouldBe(3);
        values.GetProperty("completed").GetDouble().ShouldBe(2);
        values.GetProperty("failed").GetDouble().ShouldBe(0.5);
        values.GetProperty("latency").GetDouble().ShouldBe(25);
        values.GetProperty("lifetimeLatency").GetDouble().ShouldBe(25);
        values.GetProperty("heap").GetDouble().ShouldBe(30);
        var snapshot = Json(sut).GetRawText();
        Json(sut).GetRawText().ShouldBe(snapshot);
        clock.Advance(TimeSpan.FromSeconds(1));
        Last(sut).GetProperty("completed").GetDouble().ShouldBe(0);
        Last(sut).GetProperty("latency").ValueKind.ShouldBe(JsonValueKind.Null);
        runtime.GetSnapshot().Returns(new DotNetMetricsSnapshotModel { CpuUsagePercent = 40 });
        clock.Advance(TimeSpan.FromSeconds(1));
        Last(sut).GetProperty("cpuMean").GetDouble().ShouldBe(25);
    }

    [Fact]
    public void Snapshot_SourceRecoveryAndReset_StartNewBaselineWithoutNegativeRates()
    {
        var clock = new Clock();
        var requests = Substitute.For<IAspNetMetricsSnapshotService>();
        var http = new AspNetMetricsSnapshotModel { TotalRequests = 10, Status2xx = 10, TotalLatencyMs = 100 };
        requests.GetSnapshot().Returns(_ => http);
        using var sut = new DashboardSystemHistory(requests: requests, timeProvider: clock);
        Json(sut).GetProperty("sourceStatus").GetString().ShouldBe("Runtime metrics unavailable");
        http = new() { TotalRequests = 1, Status2xx = 1, TotalLatencyMs = 5 };
        clock.Advance(TimeSpan.FromSeconds(1));
        Last(sut).GetProperty("completed").ValueKind.ShouldBe(JsonValueKind.Null);
        http = null;
        clock.Advance(TimeSpan.FromSeconds(1));
        Last(sut).GetProperty("latency").ValueKind.ShouldBe(JsonValueKind.Null);
        http = new() { TotalRequests = 20, Status2xx = 20, TotalLatencyMs = 400 };
        clock.Advance(TimeSpan.FromSeconds(1));
        Last(sut).GetProperty("completed").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public void Snapshot_MissingOrFailedSources_KeepValuesUnavailable()
    {
        var clock = new Clock();
        var runtime = Substitute.For<IDotNetMetricsSnapshotService>();
        runtime.GetSnapshot().Returns(_ => throw new InvalidOperationException("unavailable"));
        using var sut = new DashboardSystemHistory(runtime, timeProvider: clock);
        Json(sut).GetProperty("observed").GetBoolean().ShouldBeFalse();
        Last(sut).EnumerateObject().ShouldAllBe(item => item.Value.ValueKind == JsonValueKind.Null);
        using var disabled = new DashboardSystemHistory(timeProvider: new Clock());
        Json(disabled).GetProperty("enabled").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public void Snapshot_RetentionAndDisposal_BoundHistoryAndStopSampling()
    {
        var clock = new Clock();
        using var sut = new DashboardSystemHistory(timeProvider: clock);
        for (var i = 0; i < 1000; i++) clock.Advance(TimeSpan.FromSeconds(1));
        Json(sut).GetProperty("samples").GetArrayLength().ShouldBe(900);
        sut.Dispose();
        clock.Advance(TimeSpan.FromHours(1));
        Json(sut).GetProperty("samples").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Routes_CustomPaths_SeparateOverviewAndDiagnosticsAndRememberView()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddDashboard(options =>
        {
            options.AllowAnonymous().WithGroupPath("/ops");
            options.Build().EndpointPaths.SystemDiagnostics = "/runtime/charts";
            options.Build().EndpointPaths.SystemDiagnosticsData = "/runtime/samples";
        });
        await using var app = builder.Build();
        app.MapEndpoints();
        await app.StartAsync();
        var client = app.GetTestClient();
        var overview = await client.GetStringAsync("/ops/system");
        overview.ShouldContain("Busiest Routes");
        overview.ShouldNotContain("system-cpu-history");
        overview.ShouldNotContain("bdk.dashboard.system.samples");
        overview.ShouldContain("/ops/runtime/charts");
        var html = await client.GetStringAsync("/ops/runtime/charts");
        for (var i = 0; i < 4; i++) html.ShouldContain($"id=\"system-metric-chart-{i}\"");
        html.ShouldContain("/ops/runtime/samples");
        html.ShouldContain("Managed");
        html.ShouldContain("Interval mean");
        var snapshot = await client.GetFromJsonAsync<JsonElement>("/ops/runtime/samples");
        snapshot.GetProperty("enabled").GetBoolean().ShouldBeFalse();
        var provider = app.Services.GetServices<IDashboardPageProvider>().OfType<DashboardPageProvider>().Single();
        provider.GetPages(new Microsoft.AspNetCore.Http.DefaultHttpContext()).Single(page => page.Key == "dashboard.system")
            .RememberedViews["diagnostics"].ShouldBe("/ops/runtime/charts");
    }

    private static JsonElement Json(DashboardSystemHistory history) => JsonSerializer.SerializeToElement(history.Snapshot(), new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static JsonElement Last(DashboardSystemHistory history) => Json(history).GetProperty("samples").EnumerateArray().Last().GetProperty("values");

    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 11, 4, 15, 0, 0, TimeSpan.Zero);
        private TimerCallback callback;

        public override DateTimeOffset GetUtcNow() => this.now;

        public override ITimer CreateTimer(TimerCallback callback, object state, TimeSpan dueTime, TimeSpan period)
        {
            this.callback = callback;
            callback(state);
            return new Timer(_ => { }, null, Timeout.Infinite, Timeout.Infinite);
        }

        public void Advance(TimeSpan duration)
        {
            this.now += duration;
            this.callback(null);
        }
    }
}
