// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Spectre.Console;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;

public class InfoConsoleCommand : ConsoleCommandBase
{
    /// <summary>
    /// Shows .NET framework, OS, architecture, GC mode and process details.
    /// Usage: <c>info</c>
    /// </summary>
    public InfoConsoleCommand() : base("info", ".NET runtime & process info", ["i"]) { }
    /// <summary>Displays process and runtime information.</summary>
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        var proc = Process.GetCurrentProcess();
        var gcMode = GCSettings.IsServerGC ? "Server" : "Workstation";
        var latency = GCSettings.LatencyMode;
        var framework = RuntimeInformation.FrameworkDescription;
        var os = RuntimeInformation.OSDescription;
        var arch = RuntimeInformation.ProcessArchitecture.ToString();
        var pid = proc.Id.ToString();
        var config = GetBuildConfiguration();
        var table = new Table().Border(TableBorder.Minimal);
        table.AddColumn("Key"); table.AddColumn("Value");
        table.AddRow("Framework", framework);
        table.AddRow("OS", os);
        table.AddRow("Architecture", arch);
        table.AddRow("ProcessId", pid);
        table.AddRow("GC.Mode", gcMode);
        table.AddRow("GC.Latency", latency.ToString());
        table.AddRow("Build.Config", config);
        table.AddRow("WorkingSetMB", (proc.WorkingSet64 / (1024 * 1024.0)).ToString("F2"));
        table.AddRow("StartTime", proc.StartTime.ToString("u"));
        console.Write(table);

        return Task.CompletedTask;

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
// === end diagnostic additions ===