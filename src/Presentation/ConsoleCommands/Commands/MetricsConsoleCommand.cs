// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using System.Diagnostics;

public class MetricsConsoleCommand : ConsoleCommandBase
{
    /// <summary>
    /// Dumps aggregated runtime metrics (requests, memory, GC, latency).
    /// Usage: <c>metrics</c>
    /// Alias: <c>dump</c>
    /// </summary>
    public MetricsConsoleCommand() : base("metrics", "Dump key runtime metrics", "dump") { }
    /// <summary>Outputs current runtime metrics.</summary>
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        var stats = services.GetRequiredService<ConsoleCommandInteractiveRuntimeStats>();
        var proc = Process.GetCurrentProcess();
        var managed = GC.GetTotalMemory(false);
        var avgLatency = stats.TotalRequests == 0 ? 0 : stats.TotalLatencyMs / (double)stats.TotalRequests;
        var failureRate = stats.TotalRequests == 0 ? 0 : (stats.TotalFailures / (double)stats.TotalRequests) * 100.0;
        var table = new Table().Border(TableBorder.Minimal);
        table.AddColumn("Metric"); table.AddColumn("Value");
        table.AddRow("Uptime", Format(stats.Uptime));
        table.AddRow("Requests.Total", stats.TotalRequests.ToString());
        table.AddRow("Requests.Failures", stats.TotalFailures.ToString());
        table.AddRow("Requests.FailureRate", failureRate.ToString("F2") + "%");
        table.AddRow("Latency.AvgMs", avgLatency.ToString("F2"));
        table.AddRow("Memory.ManagedMB", (managed / (1024 * 1024.0)).ToString("F2"));
        table.AddRow("Memory.WorkingSetMB", (proc.WorkingSet64 / (1024 * 1024.0)).ToString("F2"));
        table.AddRow("GC.Gen0", GC.CollectionCount(0).ToString());
        table.AddRow("GC.Gen1", GC.CollectionCount(1).ToString());
        table.AddRow("GC.Gen2", GC.CollectionCount(2).ToString());
        table.AddRow("Process.Threads", proc.Threads.Count.ToString());
        table.AddRow("Process.Handles", proc.HandleCount.ToString());
        console.Write(table);

        return Task.CompletedTask;
    }
    private static string Format(TimeSpan ts) => $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
}
// === end diagnostic additions ===