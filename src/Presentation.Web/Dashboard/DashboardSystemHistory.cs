// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.Web.Dashboard;

using Microsoft.Extensions.Logging;

/// <summary>Samples existing runtime and HTTP snapshots into bounded process-local chart history.</summary>
/// <example><code>using var history = new DashboardSystemHistory(runtimeSnapshots, requestSnapshots);</code></example>
public sealed class DashboardSystemHistory : IDisposable
{
    private readonly object gate = new();
    private readonly Queue<Sample> samples = new();
    private readonly Queue<double> cpuSamples = new();
    private readonly IDotNetMetricsSnapshotService runtime;
    private readonly IAspNetMetricsSnapshotService requests;
    private readonly TimeProvider clock;
    private readonly ILogger<DashboardSystemHistory> logger;
    private readonly ITimer timer;
    private readonly DateTimeOffset started;
    private AspNetMetricsSnapshotModel previous;
    private DateTimeOffset previousAt;
    private bool runtimeAvailable;
    private bool requestsAvailable;
    private bool disposed;

    internal static readonly IReadOnlyList<DashboardWorkloadChart> Charts =
    [
        new("CPU usage", "%", [new("cpu", "Process"), new("cpuMean", "5-sample mean")]),
        new("Memory", "MB", [new("workingSet", "Working set"), new("privateMemory", "Private"), new("managedMemory", "Managed"), new("heap", "GC heap")]),
        new("Request rate", "requests/s", [new("started", "Started"), new("completed", "Completed"), new("failed", "Failed (5xx)")]),
        new("Request latency", "ms", [new("latency", "Interval mean", true), new("lifetimeLatency", "Since start mean", true)]),
    ];

    /// <summary>Starts sampling once per second, independently of browser requests.</summary>
    /// <param name="runtime">Optional existing runtime snapshot service.</param>
    /// <param name="requests">Optional existing request snapshot service.</param>
    /// <param name="timeProvider">Optional clock and timer provider.</param>
    /// <param name="logger">Optional logger for source failures.</param>
    public DashboardSystemHistory(IDotNetMetricsSnapshotService runtime = null, IAspNetMetricsSnapshotService requests = null,
        TimeProvider timeProvider = null, ILogger<DashboardSystemHistory> logger = null)
    {
        this.runtime = runtime;
        this.requests = requests;
        this.clock = timeProvider ?? TimeProvider.System;
        this.logger = logger;
        this.started = this.clock.GetUtcNow();
        this.timer = this.clock.CreateTimer(_ => this.Capture(), null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    /// <summary>Returns at most fifteen minutes of samples; polling never adds observations.</summary>
    /// <returns>A JSON-ready chart snapshot with missing measurements represented by null.</returns>
    public object Snapshot()
    {
        lock (this.gate)
        {
            this.Trim(this.clock.GetUtcNow());
            return new
            {
                enabled = this.runtime is not null || this.requests is not null,
                observed = this.samples.Count > 0 && (this.runtimeAvailable || this.requestsAvailable),
                sourceStatus = !this.runtimeAvailable && !this.requestsAvailable ? "Runtime and HTTP metrics unavailable"
                    : !this.runtimeAvailable ? "Runtime metrics unavailable" : !this.requestsAvailable ? "HTTP metrics unavailable" : null,
                startedAt = this.started,
                bucketSeconds = 1,
                samples = this.samples.ToArray()
            };
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (this.gate)
        {
            this.disposed = true;
        }

        this.timer.Dispose();
    }

    private void Capture()
    {
        lock (this.gate)
        {
            if (this.disposed) return;
            var at = this.clock.GetUtcNow();
            var dotNet = this.Read(() => this.runtime?.GetSnapshot());
            var http = this.Read(() => this.requests?.GetSnapshot());
            this.runtimeAvailable = dotNet is not null;
            this.requestsAvailable = http is not null;
            var values = Charts.SelectMany(chart => chart.Series).ToDictionary(series => series.InstrumentName, _ => (double?)null);
            if (dotNet is not null)
            {
                this.cpuSamples.Enqueue(dotNet.CpuUsagePercent);
                while (this.cpuSamples.Count > 5) this.cpuSamples.Dequeue();
                values["cpu"] = dotNet.CpuUsagePercent;
                values["cpuMean"] = this.cpuSamples.Average();
                values["workingSet"] = dotNet.WorkingSetMb;
                values["privateMemory"] = dotNet.PrivateMemoryMb;
                values["managedMemory"] = dotNet.ManagedMemoryMb;
                values["heap"] = dotNet.HeapSizeMb;
            }
            else this.cpuSamples.Clear();

            if (http is not null)
            {
                var completed = Completed(http);
                values["lifetimeLatency"] = completed > 0 ? http.TotalLatencyMs / (double)completed : null;
                var elapsed = (at - this.previousAt).TotalSeconds;
                if (this.previous is not null && elapsed > 0 && http.TotalRequests >= this.previous.TotalRequests &&
                    completed >= Completed(this.previous) && http.FailedRequests >= this.previous.FailedRequests &&
                    http.TotalLatencyMs >= this.previous.TotalLatencyMs && http.ProcessStartedAtUtc == this.previous.ProcessStartedAtUtc)
                {
                    var count = completed - Completed(this.previous);
                    values["started"] = (http.TotalRequests - this.previous.TotalRequests) / elapsed;
                    values["completed"] = count / elapsed;
                    values["failed"] = (http.FailedRequests - this.previous.FailedRequests) / elapsed;
                    values["latency"] = count > 0 ? (http.TotalLatencyMs - this.previous.TotalLatencyMs) / (double)count : null;
                }
            }

            this.previous = http;
            this.previousAt = at;
            this.samples.Enqueue(new Sample(at, values));
            this.Trim(at);
        }
    }

    private T Read<T>(Func<T> read) where T : class
    {
        try { return read(); }
        catch (Exception exception)
        {
            this.logger?.LogDebug(exception, "System diagnostic metric source failed ({Source})", typeof(T).Name);
            return null;
        }
    }

    private static long Completed(AspNetMetricsSnapshotModel snapshot) =>
        snapshot.Status1xx + snapshot.Status2xx + snapshot.Status3xx + snapshot.Status4xx + snapshot.Status5xx;

    private void Trim(DateTimeOffset at)
    {
        while (this.samples.Count > 900 || this.samples.TryPeek(out var first) && first.At <= at.AddMinutes(-15)) this.samples.Dequeue();
    }

    private sealed record Sample(DateTimeOffset At, Dictionary<string, double?> Values);
}
