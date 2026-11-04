// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Dashboard;

using System.Diagnostics.Metrics;
using System.Net.Http.Json;
using System.Text.Json;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

[Collection(ProcessWideStateTestCollection.Name)]
public class DashboardMetricsHistoryTests
{
    [Fact]
    public void Snapshot_CompletedIntervals_UseActualStartupDurationAndMeasuredMeans()
    {
        var clock = new Clock();
        using var sut = new DashboardMetricsHistory([new("workload_test_count", "Count"), new("workload_test_duration", "Duration", true)], clock);
        using var meter = new Meter(Metrics.MeterName);
        meter.CreateCounter<long>("workload_test_count").Add(3);
        var duration = meter.CreateHistogram<double>("workload_test_duration");
        duration.Record(2); duration.Record(4);
        meter.CreateCounter<long>("workload_test_count_typed_copy").Add(100);
        using var other = new Meter("other");
        other.CreateCounter<long>("workload_test_count").Add(100);
        Json(sut).GetProperty("samples").GetArrayLength().ShouldBe(0);

        clock.Advance(TimeSpan.FromMilliseconds(500));
        var snapshot = Json(sut);
        var values = snapshot.GetProperty("samples")[0].GetProperty("values");
        values.GetProperty("workload_test_count").GetDouble().ShouldBe(6);
        values.GetProperty("workload_test_duration").GetDouble().ShouldBe(3);
        snapshot.GetProperty("observed").GetBoolean().ShouldBeTrue();
        Json(sut).GetRawText().ShouldBe(snapshot.GetRawText());
    }

    [Fact]
    public void Snapshot_IdleAndDisabledSources_DoNotInventLatencyOrSuccess()
    {
        var clock = new Clock();
        using var sut = new DashboardMetricsHistory([new("workload_idle", "Count"), new("workload_latency", "Latency", true)], clock);
        clock.Advance(TimeSpan.FromSeconds(2));

        var idle = Json(sut);
        idle.GetProperty("observed").GetBoolean().ShouldBeFalse();
        idle.GetProperty("samples")[0].GetProperty("values").GetProperty("workload_idle").GetDouble().ShouldBe(0);
        idle.GetProperty("samples")[0].GetProperty("values").GetProperty("workload_latency").ValueKind.ShouldBe(JsonValueKind.Null);
        var disabled = JsonSerializer.SerializeToElement(sut.Snapshot(false));
        disabled.GetProperty("enabled").GetBoolean().ShouldBeFalse();
        disabled.GetProperty("samples")[0].GetProperty("values").GetProperty("workload_idle").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public void Snapshot_RetentionAndDisposal_BoundHistoryAndStopNewMeasurements()
    {
        var clock = new Clock();
        using var sut = new DashboardMetricsHistory([new("workload_retention", "Count")], clock);
        using var meter = new Meter(Metrics.MeterName);
        var counter = meter.CreateCounter<long>("workload_retention");
        counter.Add(5);
        clock.Advance(TimeSpan.FromHours(1));
        var expired = Json(sut);
        expired.GetProperty("samples").GetArrayLength().ShouldBe(900);
        expired.GetProperty("samples").EnumerateArray().ShouldAllBe(sample => sample.GetProperty("values").GetProperty("workload_retention").GetDouble() == 0);
        sut.Dispose();
        counter.Add(20);
        clock.Advance(TimeSpan.FromSeconds(1));
        Json(sut).GetProperty("samples").EnumerateArray().ShouldAllBe(sample => sample.GetProperty("values").GetProperty("workload_retention").GetDouble() == 0);
    }

    [Theory]
    [InlineData("jobs")]
    [InlineData("messaging")]
    [InlineData("queueing")]
    [InlineData("orchestrations")]
    public async Task Diagnostics_CustomRoot_ExposeFourChartsAndMeasuredJson(string feature)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddDashboard(options => options.AllowAnonymous().WithGroupPath("/ops")
            .WithPluginAssemblyContaining<Presentation.Web.Orchestrations.Dashboard.DashboardEndpoints>()
            .WithPluginAssemblyContaining<Presentation.Web.Jobs.Dashboard.DashboardEndpoints>()
            .WithPluginAssemblyContaining<Presentation.Web.Messaging.Dashboard.DashboardEndpoints>()
            .WithPluginAssemblyContaining<Presentation.Web.Queueing.Dashboard.DashboardEndpoints>());
        await using var app = builder.Build();
        app.MapEndpoints();
        await app.StartAsync();
        var client = app.GetTestClient();

        var html = await client.GetStringAsync($"/ops/{feature}/diagnostics");
        for (var i = 0; i < 4; i++) html.ShouldContain($"id=\"{feature}-metric-chart-{i}\"");
        html.ShouldContain($"data-url=\"/ops/{feature}/data/realtime\"");
        html.ShouldNotContain("window.setInterval(sample");
        var data = await client.GetFromJsonAsync<JsonElement>($"/ops/{feature}/data/realtime");
        data.GetProperty("enabled").GetBoolean().ShouldBeFalse();
        data.GetProperty("bucketSeconds").GetInt32().ShouldBe(1);
        data.GetProperty("samples").ValueKind.ShouldBe(JsonValueKind.Array);
        if (feature == "jobs")
        {
            html.ShouldContain("Retained job history");
            var encodedCharts = System.Text.RegularExpressions.Regex.Match(html, "data-charts=\"([^\"]+)\"").Groups[1].Value;
            var charts = JsonDocument.Parse(System.Net.WebUtility.HtmlDecode(encodedCharts)).RootElement;
            var outcomes = charts[1].GetProperty("series").EnumerateArray()
                .Select(series => series.GetProperty("instrumentName").GetString()).ToArray();
            outcomes.ShouldBe(new[]
            {
                "jobs_executions_completed", "jobs_executions_failed", "jobs_executions_retried",
                "jobs_executions_timedout", "jobs_executions_cancelled", "jobs_executions_interrupted"
            });
        }
    }

    private static JsonElement Json(DashboardMetricsHistory history) => JsonSerializer.SerializeToElement(history.Snapshot());

    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 11, 4, 12, 0, 0, 500, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => this.now;

        public void Advance(TimeSpan duration) => this.now += duration;
    }
}
