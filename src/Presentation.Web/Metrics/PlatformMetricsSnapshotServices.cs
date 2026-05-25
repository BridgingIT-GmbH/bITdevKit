// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;

/// <summary>
/// Provides live snapshots of curated .NET runtime metrics.
/// </summary>
public interface IDotNetMetricsSnapshotService
{
    DotNetMetricsSnapshotModel GetSnapshot();
}

/// <summary>
/// Provides live snapshots of curated ASP.NET request metrics.
/// </summary>
public interface IAspNetMetricsSnapshotService
{
    AspNetMetricsSnapshotModel GetSnapshot();

    AspNetRouteMetricsSnapshotModel GetRouteSnapshot();
}

/// <summary>
/// Captures curated .NET runtime metrics from the current process and CLR.
/// </summary>
public class DotNetMetricsSnapshotService : IDotNetMetricsSnapshotService
{
    private readonly Lock syncLock = new();
    private readonly DateTimeOffset processStartedAtUtc = new(Process.GetCurrentProcess().StartTime.ToUniversalTime());
    private DateTimeOffset? lastCpuSampleAtUtc;
    private TimeSpan? lastTotalProcessorTime;
    private double lastCpuUsagePercent;

    public DotNetMetricsSnapshotModel GetSnapshot()
    {
        var capturedAtUtc = DateTimeOffset.UtcNow;
        using var process = Process.GetCurrentProcess();
        process.Refresh();

        var gcInfo = GC.GetGCMemoryInfo();
        ThreadPool.GetAvailableThreads(out var workerAvailable, out var ioAvailable);
        ThreadPool.GetMaxThreads(out var workerMax, out var ioMax);

        return new DotNetMetricsSnapshotModel
        {
            CapturedAtUtc = capturedAtUtc,
            ProcessStartedAtUtc = this.processStartedAtUtc,
            UptimeSeconds = Math.Max(0, (capturedAtUtc - this.processStartedAtUtc).TotalSeconds),
            CpuUsagePercent = this.SampleCpuUsage(capturedAtUtc, process.TotalProcessorTime),
            WorkingSetMb = ToMegabytes(process.WorkingSet64),
            PrivateMemoryMb = ToMegabytes(process.PrivateMemorySize64),
            ManagedMemoryMb = ToMegabytes(GC.GetTotalMemory(false)),
            HeapSizeMb = ToMegabytes(gcInfo.HeapSizeBytes),
            FragmentedMemoryMb = ToMegabytes(gcInfo.FragmentedBytes),
            TotalAllocatedMb = ToMegabytes(GC.GetTotalAllocatedBytes(false)),
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2),
            ThreadCount = process.Threads.Count,
            WorkerThreadsUsed = workerMax - workerAvailable,
            WorkerThreadsAvailable = workerAvailable,
            IoThreadsUsed = ioMax - ioAvailable,
            IoThreadsAvailable = ioAvailable,
            PendingWorkItems = SafePendingWorkItems()
        };
    }

    private static double ToMegabytes(long bytes)
    {
        return bytes / (1024d * 1024d);
    }

    private static long SafePendingWorkItems()
    {
        try
        {
            return ThreadPool.PendingWorkItemCount;
        }
        catch
        {
            return -1;
        }
    }

    private double SampleCpuUsage(DateTimeOffset capturedAtUtc, TimeSpan totalProcessorTime)
    {
        lock (this.syncLock)
        {
            if (!this.lastCpuSampleAtUtc.HasValue || !this.lastTotalProcessorTime.HasValue)
            {
                this.lastCpuSampleAtUtc = capturedAtUtc;
                this.lastTotalProcessorTime = totalProcessorTime;
                this.lastCpuUsagePercent = 0;

                return this.lastCpuUsagePercent;
            }

            var elapsedSeconds = Math.Max(0, (capturedAtUtc - this.lastCpuSampleAtUtc.Value).TotalSeconds);
            var cpuSeconds = Math.Max(0, (totalProcessorTime - this.lastTotalProcessorTime.Value).TotalSeconds);

            if (elapsedSeconds > 0)
            {
                var sample = (cpuSeconds / (elapsedSeconds * Environment.ProcessorCount)) * 100d;
                this.lastCpuUsagePercent = Math.Clamp(sample, 0, 100);
            }

            this.lastCpuSampleAtUtc = capturedAtUtc;
            this.lastTotalProcessorTime = totalProcessorTime;

            return this.lastCpuUsagePercent;
        }
    }
}

/// <summary>
/// Collects lightweight ASP.NET request statistics for the metrics endpoint.
/// </summary>
public sealed class AspNetMetricsTracker
{
    private long totalRequests;
    private long activeRequests;
    private long maxObservedConcurrentRequests;
    private long failedRequests;
    private long totalLatencyMs;
    private long status1xx;
    private long status2xx;
    private long status3xx;
    private long status4xx;
    private long status5xx;
    private long lastRequestUtcTicks;
    private readonly ConcurrentDictionary<string, AspNetRouteMetricsAccumulator> routes = new(StringComparer.OrdinalIgnoreCase);

    public void BeginRequest()
    {
        Interlocked.Increment(ref this.totalRequests);

        var active = Interlocked.Increment(ref this.activeRequests);
        var currentMax = Interlocked.Read(ref this.maxObservedConcurrentRequests);
        while (active > currentMax)
        {
            var original = Interlocked.CompareExchange(ref this.maxObservedConcurrentRequests, active, currentMax);
            if (original == currentMax)
            {
                break;
            }

            currentMax = original;
        }
    }

    public void CompleteRequest(string method, string route, int statusCode, long elapsedMilliseconds, DateTimeOffset finishedAtUtc)
    {
        Interlocked.Decrement(ref this.activeRequests);
        Interlocked.Add(ref this.totalLatencyMs, elapsedMilliseconds);
        Interlocked.Exchange(ref this.lastRequestUtcTicks, finishedAtUtc.UtcTicks);

        if (statusCode >= 500)
        {
            Interlocked.Increment(ref this.failedRequests);
        }

        switch (statusCode / 100)
        {
            case 1:
                Interlocked.Increment(ref this.status1xx);
                break;
            case 2:
                Interlocked.Increment(ref this.status2xx);
                break;
            case 3:
                Interlocked.Increment(ref this.status3xx);
                break;
            case 4:
                Interlocked.Increment(ref this.status4xx);
                break;
            default:
                Interlocked.Increment(ref this.status5xx);
                break;
        }

        if (!string.IsNullOrWhiteSpace(route))
        {
            var key = $"{method} {route}";
            var accumulator = this.routes.GetOrAdd(key, _ => new AspNetRouteMetricsAccumulator(method, route));
            accumulator.Record(statusCode, elapsedMilliseconds, finishedAtUtc);
        }
    }

    public AspNetMetricsSnapshotModel CreateSnapshot(DateTimeOffset processStartedAtUtc)
    {
        var capturedAtUtc = DateTimeOffset.UtcNow;
        var total = Interlocked.Read(ref this.totalRequests);
        var failed = Interlocked.Read(ref this.failedRequests);
        var active = Interlocked.Read(ref this.activeRequests);
        var totalLatency = Interlocked.Read(ref this.totalLatencyMs);
        var uptimeSeconds = Math.Max(0, (capturedAtUtc - processStartedAtUtc).TotalSeconds);
        var lastRequestTicks = Interlocked.Read(ref this.lastRequestUtcTicks);

        return new AspNetMetricsSnapshotModel
        {
            CapturedAtUtc = capturedAtUtc,
            ProcessStartedAtUtc = processStartedAtUtc,
            UptimeSeconds = uptimeSeconds,
            TotalRequests = total,
            TrackedRouteCount = this.routes.Count,
            ActiveRequests = active,
            MaxObservedConcurrentRequests = Interlocked.Read(ref this.maxObservedConcurrentRequests),
            FailedRequests = failed,
            FailureRatePercent = total == 0 ? 0 : (failed / (double)total) * 100d,
            AverageLatencyMs = total == 0 ? 0 : totalLatency / (double)total,
            TotalLatencyMs = totalLatency,
            RequestsPerMinute = uptimeSeconds <= 0 ? 0 : (total / uptimeSeconds) * 60d,
            Status1xx = Interlocked.Read(ref this.status1xx),
            Status2xx = Interlocked.Read(ref this.status2xx),
            Status3xx = Interlocked.Read(ref this.status3xx),
            Status4xx = Interlocked.Read(ref this.status4xx),
            Status5xx = Interlocked.Read(ref this.status5xx),
            LastRequestAtUtc = lastRequestTicks <= 0 ? null : new DateTimeOffset(lastRequestTicks, TimeSpan.Zero)
        };
    }

    public AspNetRouteMetricsSnapshotModel CreateRouteSnapshot(DateTimeOffset processStartedAtUtc)
    {
        var capturedAtUtc = DateTimeOffset.UtcNow;

        return new AspNetRouteMetricsSnapshotModel
        {
            CapturedAtUtc = capturedAtUtc,
            ProcessStartedAtUtc = processStartedAtUtc,
            UptimeSeconds = Math.Max(0, (capturedAtUtc - processStartedAtUtc).TotalSeconds),
            TrackedRouteCount = this.routes.Count,
            Routes = this.routes.Values
                .Select(route => route.ToModel())
                .OrderByDescending(route => route.RequestCount)
                .ThenBy(route => route.Route, StringComparer.OrdinalIgnoreCase)
                .ThenBy(route => route.Method, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }
}

/// <summary>
/// Provides a snapshot projection over the ASP.NET request tracker.
/// </summary>
public sealed class AspNetMetricsSnapshotService(AspNetMetricsTracker tracker) : IAspNetMetricsSnapshotService
{
    private readonly DateTimeOffset processStartedAtUtc = new(Process.GetCurrentProcess().StartTime.ToUniversalTime());

    public AspNetMetricsSnapshotModel GetSnapshot()
    {
        return tracker.CreateSnapshot(this.processStartedAtUtc);
    }

    public AspNetRouteMetricsSnapshotModel GetRouteSnapshot()
    {
        return tracker.CreateRouteSnapshot(this.processStartedAtUtc);
    }
}

internal sealed class AspNetRouteMetricsAccumulator(string method, string route)
{
    private long requestCount;
    private long status1xx;
    private long status2xx;
    private long status3xx;
    private long status4xx;
    private long status5xx;
    private long failureCount;
    private long totalLatencyMs;
    private long lastRequestUtcTicks;

    public void Record(int statusCode, long elapsedMilliseconds, DateTimeOffset finishedAtUtc)
    {
        Interlocked.Increment(ref this.requestCount);
        Interlocked.Add(ref this.totalLatencyMs, elapsedMilliseconds);
        Interlocked.Exchange(ref this.lastRequestUtcTicks, finishedAtUtc.UtcTicks);

        if (statusCode >= 500)
        {
            Interlocked.Increment(ref this.failureCount);
        }

        switch (statusCode / 100)
        {
            case 1:
                Interlocked.Increment(ref this.status1xx);
                break;
            case 2:
                Interlocked.Increment(ref this.status2xx);
                break;
            case 3:
                Interlocked.Increment(ref this.status3xx);
                break;
            case 4:
                Interlocked.Increment(ref this.status4xx);
                break;
            default:
                Interlocked.Increment(ref this.status5xx);
                break;
        }
    }

    public AspNetRouteMetricsModel ToModel()
    {
        var count = Interlocked.Read(ref this.requestCount);
        var failures = Interlocked.Read(ref this.failureCount);
        var totalLatency = Interlocked.Read(ref this.totalLatencyMs);
        var lastRequestTicks = Interlocked.Read(ref this.lastRequestUtcTicks);

        return new AspNetRouteMetricsModel
        {
            Method = method,
            Route = route,
            RequestCount = count,
            Status1xx = Interlocked.Read(ref this.status1xx),
            Status2xx = Interlocked.Read(ref this.status2xx),
            Status3xx = Interlocked.Read(ref this.status3xx),
            Status4xx = Interlocked.Read(ref this.status4xx),
            Status5xx = Interlocked.Read(ref this.status5xx),
            FailureCount = failures,
            FailureRatePercent = count == 0 ? 0 : (failures / (double)count) * 100d,
            TotalLatencyMs = totalLatency,
            AverageLatencyMs = count == 0 ? 0 : totalLatency / (double)count,
            LastRequestAtUtc = lastRequestTicks <= 0 ? null : new DateTimeOffset(lastRequestTicks, TimeSpan.Zero)
        };
    }
}
