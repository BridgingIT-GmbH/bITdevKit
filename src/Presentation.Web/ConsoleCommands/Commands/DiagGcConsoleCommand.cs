// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;

public class DiagGcConsoleCommand : ConsoleCommandBase, IGroupedConsoleCommand
{
    public string GroupName => "diag";
    public IReadOnlyCollection<string> GroupAliases => ["d"];
    [ConsoleCommandOption("force", Alias = "f", Description = "Force full GC before reporting")] public bool Force { get; set; }
    public DiagGcConsoleCommand() : base("gc", "GC statistics (optionally force collection)") { }
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        var env = services.GetRequiredService<IWebHostEnvironment>();
        var allow = env.IsDevelopment();
        if (this.Force && !allow)
        {
            console.MarkupLine("[yellow]Force collection only allowed in development.[/]");
        }

        console.Write(DiagnosticTablesBuilder.BuildGc(this.Force && allow));
        return Task.CompletedTask;
    }
}

public class DiagThreadsConsoleCommand : ConsoleCommandBase, IGroupedConsoleCommand
{
    public string GroupName => "diag";
    public IReadOnlyCollection<string> GroupAliases => ["d"];
    public DiagThreadsConsoleCommand() : base("threads", "Thread pool statistics (diag)") { }
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    { console.Write(DiagnosticTablesBuilder.BuildThreads()); return Task.CompletedTask; }
}

public class DiagMemConsoleCommand : ConsoleCommandBase, IGroupedConsoleCommand
{
    public string GroupName => "diag";
    public IReadOnlyCollection<string> GroupAliases => ["d"];
    public DiagMemConsoleCommand() : base("mem", "Detailed memory usage (diag)") { }
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    { console.Write(DiagnosticTablesBuilder.BuildMemory()); return Task.CompletedTask; }
}

public class DiagPerfConsoleCommand : ConsoleCommandBase, IGroupedConsoleCommand
{
    public string GroupName => "diag";
    public IReadOnlyCollection<string> GroupAliases => ["d"];
    public DiagPerfConsoleCommand() : base("perf", "Point-in-time performance snapshot") { }
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    { var stats = services.GetRequiredService<ConsoleCommandInteractiveRuntimeStats>(); console.Write(DiagnosticTablesBuilder.BuildPerf(stats)); return Task.CompletedTask; }
}

public class DiagEnvConsoleCommand : ConsoleCommandBase, IGroupedConsoleCommand
{
    public string GroupName => "diag";
    public IReadOnlyCollection<string> GroupAliases => ["d"];
    public DiagEnvConsoleCommand() : base("env", "Runtime & environment info (diag)") { }
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    { console.Write(DiagnosticTablesBuilder.BuildEnv()); return Task.CompletedTask; }
}

static class DiagnosticTablesBuilder
{
    public static Table BuildGc(bool forced)
    {
        if (forced)
        {
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        }
        var info = GC.GetGCMemoryInfo();
        var table = new Table().Border(TableBorder.Minimal);
        table.AddColumn("Metric"); table.AddColumn("Value");
        table.AddRow("TotalMemoryMB", (GC.GetTotalMemory(false) / (1024 * 1024.0)).ToString("F2"));
        table.AddRow("HeapSizeMB", (info.HeapSizeBytes / (1024 * 1024.0)).ToString("F2"));
        table.AddRow("FragmentedMB", (info.FragmentedBytes / (1024 * 1024.0)).ToString("F2"));
        table.AddRow("Gen0", GC.CollectionCount(0).ToString());
        table.AddRow("Gen1", GC.CollectionCount(1).ToString());
        table.AddRow("Gen2", GC.CollectionCount(2).ToString());
        table.AddRow("Compacted", info.Compacted ? "true" : "false");
        table.AddRow("Forced", forced ? "true" : "false");
        return table;
    }
    public static Table BuildThreads()
    {
        ThreadPool.GetAvailableThreads(out var workerAvail, out var ioAvail);
        ThreadPool.GetMaxThreads(out var workerMax, out var ioMax);
        ThreadPool.GetMinThreads(out var workerMin, out var ioMin);
        var table = new Table().Border(TableBorder.Minimal);
        table.AddColumn("Metric"); table.AddColumn("Value");
        table.AddRow("Worker.Max", workerMax.ToString());
        table.AddRow("Worker.Min", workerMin.ToString());
        table.AddRow("Worker.Available", workerAvail.ToString());
        table.AddRow("Worker.Used", (workerMax - workerAvail).ToString());
        table.AddRow("IO.Max", ioMax.ToString());
        table.AddRow("IO.Min", ioMin.ToString());
        table.AddRow("IO.Available", ioAvail.ToString());
        table.AddRow("IO.Used", (ioMax - ioAvail).ToString());
        table.AddRow("PendingWorkItems", SafePending().ToString());
        return table;
        static long SafePending() { try { return ThreadPool.PendingWorkItemCount; } catch { return -1; } }
    }
    public static Table BuildMemory()
    {
        var proc = Process.GetCurrentProcess();
        var managed = GC.GetTotalMemory(false);
        var info = GC.GetGCMemoryInfo();
        var table = new Table().Border(TableBorder.Minimal);
        table.AddColumn("Metric"); table.AddColumn("Value");
        table.AddRow("ManagedMB", (managed / (1024 * 1024.0)).ToString("F2"));
        table.AddRow("WorkingSetMB", (proc.WorkingSet64 / (1024 * 1024.0)).ToString("F2"));
        table.AddRow("PrivateMemMB", (proc.PrivateMemorySize64 / (1024 * 1024.0)).ToString("F2"));
        table.AddRow("HeapSizeMB", (info.HeapSizeBytes / (1024 * 1024.0)).ToString("F2"));
        table.AddRow("FragmentedMB", (info.FragmentedBytes / (1024 * 1024.0)).ToString("F2"));
        table.AddRow("Gen0", GC.CollectionCount(0).ToString());
        table.AddRow("Gen1", GC.CollectionCount(1).ToString());
        table.AddRow("Gen2", GC.CollectionCount(2).ToString());
        return table;
    }
    public static Table BuildPerf(ConsoleCommandInteractiveRuntimeStats stats)
    {
        var proc = Process.GetCurrentProcess();
        var uptime = stats.Uptime;
        var cpuTotal = proc.TotalProcessorTime.TotalSeconds;
        var elapsed = (DateTime.UtcNow - proc.StartTime.ToUniversalTime()).TotalSeconds;
        var cpuPercent = elapsed <= 0 ? 0 : (cpuTotal / elapsed) / Environment.ProcessorCount * 100.0;
        var avgLatency = stats.TotalRequests == 0 ? 0 : stats.TotalLatencyMs / (double)stats.TotalRequests;
        var failureRate = stats.TotalRequests == 0 ? 0 : (stats.TotalFailures / (double)stats.TotalRequests) * 100.0;
        var managed = GC.GetTotalMemory(false);
        var table = new Table().Border(TableBorder.Minimal);
        table.AddColumn("Metric"); table.AddColumn("Value");
        table.AddRow("Uptime", Format(uptime));
        table.AddRow("CPU%", cpuPercent.ToString("F2"));
        table.AddRow("Requests.Total", stats.TotalRequests.ToString());
        table.AddRow("Requests.Failures", stats.TotalFailures.ToString());
        table.AddRow("Requests.FailureRate%", failureRate.ToString("F2"));
        table.AddRow("Latency.AvgMs", avgLatency.ToString("F2"));
        table.AddRow("ManagedMB", (managed / (1024 * 1024.0)).ToString("F2"));
        table.AddRow("WorkingSetMB", (proc.WorkingSet64 / (1024 * 1024.0)).ToString("F2"));
        table.AddRow("Threads", proc.Threads.Count.ToString());
        return table;
        static string Format(TimeSpan ts) => $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
    }
    public static Table BuildEnv()
    {
        var proc = Process.GetCurrentProcess();
        var table = new Table().Border(TableBorder.Minimal);
        table.AddColumn("Key"); table.AddColumn("Value");
        table.AddRow("Framework", RuntimeInformation.FrameworkDescription);
        table.AddRow("OS", RuntimeInformation.OSDescription);
        table.AddRow("Architecture", RuntimeInformation.ProcessArchitecture.ToString());
        table.AddRow("GC.Mode", GCSettings.IsServerGC ? "Server" : "Workstation");
        table.AddRow("GC.Latency", GCSettings.LatencyMode.ToString());
        table.AddRow("ProcessId", proc.Id.ToString());
        table.AddRow("StartTime", proc.StartTime.ToString("u"));
        table.AddRow("WorkingSetMB", (proc.WorkingSet64 / (1024 * 1024.0)).ToString("F2"));
        table.AddRow("AssembliesLoaded", AppDomain.CurrentDomain.GetAssemblies().Length.ToString());
        table.AddRow("Build.Config", GetBuildConfiguration());
        return table;
        static string GetBuildConfiguration()
        {
#if DEBUG
            return "Debug";
#elif RELEASE
 return "Release";
#else
 return "Unknown";
#endif
        }
    }
}