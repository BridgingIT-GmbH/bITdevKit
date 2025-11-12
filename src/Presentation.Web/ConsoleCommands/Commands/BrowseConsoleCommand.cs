// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public class BrowseConsoleCommand : ConsoleCommandBase, IGroupedConsoleCommand
{
    public string GroupName => "browse";
    public IReadOnlyCollection<string> GroupAliases => ["web"];
    [ConsoleCommandArgument(0, Description = "Relative path to append", Required = false)] public string Path { get; set; }
    [ConsoleCommandOption("no-https", Description = "Prefer HTTP instead of HTTPS")] public bool NoHttps { get; set; }
    [ConsoleCommandOption("all", Description = "Open all bound addresses")] public bool All { get; set; }
    public BrowseConsoleCommand() : base("open", "Open default browser on Kestrel address", "web") { }
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        var server = services.GetService<IServer>(); var feature = server?.Features.Get<IServerAddressesFeature>(); var addresses = feature?.Addresses?.ToList() ?? [];
        if (addresses.Count == 0) { console.MarkupLine("[yellow]No server addresses available (server not fully started?).[/]"); return Task.CompletedTask; }
        var httpAddresses = addresses.Where(a => a.StartsWith("http://", StringComparison.OrdinalIgnoreCase)).ToList();
        var httpsAddresses = addresses.Where(a => a.StartsWith("https://", StringComparison.OrdinalIgnoreCase)).ToList();
        List<string> targets;
        if (this.All)
        {
            targets = this.NoHttps ? (httpAddresses.Count == 0 ? addresses : httpAddresses) : (httpsAddresses.Count != 0 ? httpsAddresses : addresses);
            if (this.NoHttps && httpAddresses.Count == 0)
            {
                console.MarkupLine("[yellow]--no-https requested but no HTTP binding found; falling back to all addresses.[/]");
            }
        }
        else
        {
            if (this.NoHttps)
            {
                targets = httpAddresses.Count != 0 ? [httpAddresses.First()] : [addresses.First()];
                if (httpAddresses.Count == 0)
                {
                    console.MarkupLine("[yellow]--no-https requested but no HTTP binding found; using HTTPS instead.[/]");
                }
            }
            else { targets = [httpsAddresses.FirstOrDefault() ?? addresses.First()]; }
        }
        var pathSegment = NormalizePath(this.Path);
        foreach (var baseAddr in targets)
        {
            var url = string.IsNullOrEmpty(pathSegment) ? baseAddr.TrimEnd('/') + "/" : baseAddr.TrimEnd('/') + "/" + pathSegment;
            console.MarkupLine($"[grey]Opening:[/] [blue underline]{Markup.Escape(url)}[/]");
            TryOpen(url, console);
        }
        return Task.CompletedTask;
        static string NormalizePath(string p) { if (string.IsNullOrWhiteSpace(p)) { return string.Empty; } var trimmed = p.Trim(); if (trimmed.StartsWith('/')) { trimmed = trimmed[1..]; } return trimmed; }
        static void TryOpen(string url, IAnsiConsole console)
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                else if (OperatingSystem.IsMacOS())
                {
                    Process.Start("open", url);
                }
                else if (OperatingSystem.IsLinux())
                {
                    Process.Start("xdg-open", url);
                }
                else
                {
                    console.MarkupLine("[yellow]Unsupported OS for auto-launch. Please open manually.[/]");
                }
            }
            catch (Exception ex) { console.MarkupLine("[red]Failed to launch browser:[/] " + ex.Message); }
        }
    }
}
// === end diagnostic additions ===