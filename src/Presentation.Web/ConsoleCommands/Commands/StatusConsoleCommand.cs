// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using System.Linq;

public class StatusConsoleCommand : ConsoleCommandBase
{
    /// <summary>
    /// Displays high-level server/runtime status including uptime, request statistics and health endpoint result.
    /// Usage: <c>status</c>
    /// Example: <c>status</c>
    /// </summary>
    public StatusConsoleCommand() : base("status", "Show basic server status", ["stats"]) { }
    /// <summary>Executes the status command output.</summary>
    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        var stats = services.GetRequiredService<ConsoleCommandInteractiveRuntimeStats>();
        var env = services.GetRequiredService<IWebHostEnvironment>();
        var avg = stats.TotalRequests == 0 ? 0 : stats.TotalLatencyMs / (double)stats.TotalRequests;
        var table = new Table().Border(TableBorder.Minimal);
        table.AddColumn("Metric"); table.AddColumn("Value");
        table.AddRow("Environment", env.EnvironmentName);
        table.AddRow("Uptime", Format(stats.Uptime));
        table.AddRow("Requests", stats.TotalRequests.ToString());
        table.AddRow("5xx", stats.TotalFailures.ToString());
        table.AddRow("Avg Latency", avg.ToString("F1") + " ms");
        try
        {
            var server = services.GetService<IServer>();
            var feature = server?.Features.Get<IServerAddressesFeature>();
            var first = feature?.Addresses?.FirstOrDefault();
            if (!string.IsNullOrEmpty(first))
            {
                var healthUrl = first.TrimEnd('/') + "/health";
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
                var res = await http.GetAsync(healthUrl);
                table.AddRow("Health", ((int)res.StatusCode) + " " + res.ReasonPhrase);
            }
        }
        catch { table.AddRow("Health", "n/a"); }
        console.Write(table);
    }
    private static string Format(TimeSpan ts) => $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
}
// === end diagnostic additions ===