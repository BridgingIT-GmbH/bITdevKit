// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Presentation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Hosting;
using Spectre.Console;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;

/// <summary>
/// Provides an interactive command-based console that runs inside a locally hosted Kestrel <see cref="WebApplication"/>.
/// Commands are discovered via dependency injection and executed from user input while the web server runs.
/// </summary>
public static class CommandInteractiveConsole
{
    /// <summary>
    /// Registers the interactive console infrastructure and built-in commands with the service collection.
    /// Allows fluent registration of additional external commands via the <paramref name="configure"/> callback.
    /// </summary>
    /// <param name="services">The DI service collection.</param>
    /// <param name="configure">Optional builder callback to register additional commands.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddInteractiveConsole(this IServiceCollection services, Action<InteractiveConsoleBuilder> configure = null)
    {
        services.AddSingleton<IAnsiConsole>(_ => AnsiConsole.Console);
        services.AddSingleton<InteractiveCliRuntimeStats>();

        // built-in commands
        services.AddTransient<IInteractiveCommand, StatusInteractiveCommand>();
        services.AddTransient<IInteractiveCommand, MemInteractiveCommand>();
        services.AddTransient<IInteractiveCommand, EnvInteractiveCommand>();
        services.AddTransient<IInteractiveCommand, PortsInteractiveCommand>();
        services.AddTransient<IInteractiveCommand, ClearInteractiveCommand>();
        services.AddTransient<IInteractiveCommand, HelpInteractiveCommand>();
        services.AddTransient<IInteractiveCommand, QuitInteractiveCommand>();
        services.AddTransient<IInteractiveCommand, MetricsInteractiveCommand>();
        services.AddTransient<IInteractiveCommand, InfoInteractiveCommand>();
        services.AddTransient<IInteractiveCommand, GcCollectInteractiveCommand>();
        services.AddTransient<IInteractiveCommand, ThreadsInteractiveCommand>();

        // external fluent registrations
        if (configure is not null)
        {
            configure(new InteractiveConsoleBuilder(services));
        }

        return services;
    }

    /// <summary>
    /// Starts the interactive command loop (non-blocking) when running locally on Kestrel.
    /// The optional <paramref name="startupDelay"/> is applied after the application has signaled <see cref="IHostApplicationLifetime.ApplicationStarted"/>.
    /// This ensures the host is fully ready before accepting interactive commands.
    /// </summary>
    /// <param name="app">The current <see cref="WebApplication"/> instance.</param>
    /// <param name="startupDelay">Optional delay applied after application start before the loop begins.</param>
    public static void UseInteractiveConsole(this WebApplication app, TimeSpan? startupDelay = null)
    {
        if (!IsLocalAndKestrel(app))
        {
            return;
        }

        app.Lifetime.ApplicationStarted.Register(() =>
        {
            _ = Task.Run(async () =>
            {
                if (startupDelay.HasValue && startupDelay.Value.TotalMilliseconds > 0)
                {
                    await Task.Delay(startupDelay.Value);
                }
                var console = app.Services.GetRequiredService<IAnsiConsole>();
                var commands = app.Services.GetServices<IInteractiveCommand>().ToList();

                await RunLoopAsync(app, console, commands);
            });
        });
    }

    /// <summary>
    /// Determines whether the application is running locally on Kestrel (no IIS proxy) so the interactive console can be enabled.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns><c>true</c> if local &amp; Kestrel; otherwise <c>false</c>.</returns>
    private static bool IsLocalAndKestrel(WebApplication app)
    {
        if (!app.Environment.IsLocalDevelopment() && !app.Environment.IsDevelopment())
        {
            return false;
        }

        try
        {
            var server = app.Services.GetService<IServer>();
            var hasFeature = server?.Features.Get<IServerAddressesFeature>() is not null;
            var hasUrls = app.Urls?.Count > 0;
            return hasFeature || hasUrls;
        }
        catch { return false; }
    }

    /// <summary>
    /// Main input loop that reads user commands and executes them until shutdown or stdin closes.
    /// </summary>
    /// <param name="app">The running web application.</param>
    /// <param name="console">The Spectre console abstraction.</param>
    /// <param name="commands">Resolved command instances.</param>
    private static async Task RunLoopAsync(WebApplication app, IAnsiConsole console, List<IInteractiveCommand> commands)
    {
        PrintBanner(console);
        while (!app.Lifetime.ApplicationStopping.IsCancellationRequested)
        {
            console.Markup("[grey]> [/]");
            var line = Console.ReadLine();
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = SplitArgs(line);
            var name = parts[0];
            var cmd = commands.FirstOrDefault(c => c.Matches(name));
            if (cmd == null)
            {
                console.MarkupLine("[yellow]Unknown command[/]. Type [bold]help[/] for list.");
                continue;
            }
            try
            {
                await cmd.ExecuteAsync(parts.Skip(1).ToArray(), console, app.Services);
            }
            catch (Exception ex)
            {
                console.MarkupLine("[red]Command failed:[/] " + ex.Message);
            }
        }
    }

    /// <summary>
    /// Splits a raw command line into tokens honoring quoted segments.
    /// </summary>
    /// <param name="commandLine">The raw command line string.</param>
    /// <returns>Token array.</returns>
    private static string[] SplitArgs(string commandLine)
    {
        var list = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        foreach (var ch in commandLine)
        {
            switch (ch)
            {
                case '"': inQuotes = !inQuotes; break;
                case ' ' when !inQuotes:
                    if (current.Length > 0) { list.Add(current.ToString()); current.Clear(); }
                    break;
                default: current.Append(ch); break;
            }
        }
        if (current.Length > 0)
        {
            list.Add(current.ToString());
        }

        return list.ToArray();
    }

    /// <summary>
    /// Writes the startup banner and quick usage hint.
    /// </summary>
    /// <param name="console">The console abstraction.</param>
    private static void PrintBanner(IAnsiConsole console)
    {
        //console.Write(new FigletText("DevKit CLI").Centered().Color(Color.Cyan1));
        console.MarkupLine("[grey]Type [bold]help[/]/[bold]?[/] for commands. Examples: [bold]status[/], [bold]mem[/], [bold]env[/], [bold]ports[/], [bold]clear[/], [bold]quit[/].[/]");
        console.Write(new Rule().Centered().RuleStyle("grey"));
    }
}

/// <summary>
/// Fluent builder for interactive console command registration.
/// </summary>
public sealed class InteractiveConsoleBuilder
{
    /// <summary>Initializes a new instance of the <see cref="InteractiveConsoleBuilder"/> class.</summary>
    internal InteractiveConsoleBuilder(IServiceCollection services) => this.Services = services;
    /// <summary>Gets the underlying service collection.</summary>
    public IServiceCollection Services { get; }
    /// <summary>
    /// Registers a command type implementing <see cref="IInteractiveCommand"/> as transient.
    /// </summary>
    /// <typeparam name="TCommand">The command implementation type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public InteractiveConsoleBuilder AddCommand<TCommand>() where TCommand : class, IInteractiveCommand
    {
        this.Services.AddTransient<IInteractiveCommand, TCommand>();
        return this;
    }
}

/// <summary>
/// Holds runtime statistics captured for the interactive status command.
/// </summary>
public sealed class InteractiveCliRuntimeStats
{
    /// <summary>Gets the timestamp when the application started collecting stats.</summary>
    public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;

    /// <summary>Total number of HTTP requests observed.</summary>
    public long TotalRequests;

    /// <summary>Total number of failed (HTTP5xx) responses observed.</summary>
    public long TotalFailures;

    /// <summary>Cumulative latency (milliseconds) across all observed requests.</summary>
    public long TotalLatencyMs;

    /// <summary>Gets the calculated application uptime.</summary>
    public TimeSpan Uptime => DateTimeOffset.UtcNow - this.StartedAt;
}

/// <summary>
/// Represents an interactive command that can be executed from the console loop.
/// </summary>
public interface IInteractiveCommand
{
    /// <summary>Gets the primary command name.</summary>
    string Name { get; }

    /// <summary>Gets the list of supported aliases.</summary>
    IReadOnlyCollection<string> Aliases { get; }

    /// <summary>Gets the human readable description of the command.</summary>
    string Description { get; }

    /// <summary>
    /// Determines whether the provided input matches this command (name or alias).
    /// </summary>
    /// <param name="input">Input token.</param>
    /// <returns><c>true</c> if matched; otherwise <c>false</c>.</returns>
    bool Matches(string input);

    /// <summary>
    /// Executes the command logic.
    /// </summary>
    /// <param name="args">Additional arguments after the command name.</param>
    /// <param name="console">Console for output.</param>
    /// <param name="services">Service provider for resolving dependencies.</param>
    Task ExecuteAsync(string[] args, IAnsiConsole console, IServiceProvider services);
}

/// <summary>
/// Base class providing default matching and metadata behavior for interactive commands.
/// </summary>
/// <param name="name">Primary command name.</param>
/// <param name="description">Command description.</param>
/// <param name="aliases">Optional aliases.</param>
abstract class InteractiveCommandBase(string name, string description, params string[] aliases) : IInteractiveCommand
{
    /// <inheritdoc />
    public string Name { get; } = name;
    /// <inheritdoc />
    public IReadOnlyCollection<string> Aliases { get; } = aliases.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    /// <inheritdoc />
    public string Description { get; } = description;

    /// <inheritdoc />
    public bool Matches(string input) => string.Equals(this.Name, input, StringComparison.OrdinalIgnoreCase) || this.Aliases.Any(a => string.Equals(a, input, StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc />
    public abstract Task ExecuteAsync(string[] args, IAnsiConsole console, IServiceProvider services);
}

/// <summary>
/// Displays runtime status information (uptime, request counts, health probe).
/// </summary>
sealed class StatusInteractiveCommand : InteractiveCommandBase
{
    public StatusInteractiveCommand() : base("status", "Show basic server status") { }
    /// <inheritdoc />
    public override async Task ExecuteAsync(string[] args, IAnsiConsole console, IServiceProvider services)
    {
        var stats = services.GetRequiredService<InteractiveCliRuntimeStats>();
        var env = services.GetRequiredService<IWebHostEnvironment>();
        var avg = stats.TotalRequests == 0 ? 0 : stats.TotalLatencyMs / (double)stats.TotalRequests;
        var table = new Table().Border(TableBorder.Minimal).Title("[bold cyan]Status[/]");
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

/// <summary>
/// Displays current process memory and resource usage metrics.
/// </summary>
sealed class MemInteractiveCommand : InteractiveCommandBase
{
    public MemInteractiveCommand() : base("mem", "Show memory usage") { }
    /// <inheritdoc />
    public override Task ExecuteAsync(string[] args, IAnsiConsole console, IServiceProvider services)
    {
        var proc = Process.GetCurrentProcess();
        var managed = GC.GetTotalMemory(false);
        var table = new Table().Border(TableBorder.Minimal).Title("[bold cyan]Memory[/]");
        table.AddColumn("Metric"); table.AddColumn("Value");
        table.AddRow("Managed", $"{managed / (1024 * 1024.0):F2} MB");
        table.AddRow("WorkingSet", $"{proc.WorkingSet64 / (1024 * 1024.0):F2} MB");
        table.AddRow("PrivateMem", $"{proc.PrivateMemorySize64 / (1024 * 1024.0):F2} MB");
        table.AddRow("Threads", proc.Threads.Count.ToString());
        table.AddRow("Handles", proc.HandleCount.ToString());
        console.Write(table);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Displays environment related information (paths, environment name, application name).
/// </summary>
sealed class EnvInteractiveCommand : InteractiveCommandBase
{
    public EnvInteractiveCommand() : base("env", "Show environment info") { }
    /// <inheritdoc />
    public override Task ExecuteAsync(string[] args, IAnsiConsole console, IServiceProvider services)
    {
        var env = services.GetRequiredService<IWebHostEnvironment>();
        var table = new Table().Border(TableBorder.Minimal).Title("[bold cyan]Environment[/]");
        table.AddColumn("Key"); table.AddColumn("Value");
        table.AddRow("Name", env.EnvironmentName);
        table.AddRow("App", env.ApplicationName);
        table.AddRow("ContentRoot", env.ContentRootPath);
        table.AddRow("WebRoot", env.WebRootPath ?? "(none)");
        console.Write(table);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Lists active Kestrel bound addresses (listens / URLs).
/// </summary>
sealed class PortsInteractiveCommand : InteractiveCommandBase
{
    public PortsInteractiveCommand() : base("ports", "Show Kestrel bound addresses", "urls", "kestrel") { }
    /// <inheritdoc />
    public override Task ExecuteAsync(string[] args, IAnsiConsole console, IServiceProvider services)
    {
        var server = services.GetService<IServer>();
        var feature = server?.Features.Get<IServerAddressesFeature>();
        var addresses = feature?.Addresses ?? Array.Empty<string>();
        if (addresses.Count == 0)
        {
            console.MarkupLine("[yellow]No addresses available yet.[/]");
            return Task.CompletedTask;
        }
        var table = new Table().Border(TableBorder.Minimal).Title("[bold cyan]Addresses[/]");
        table.AddColumn("Address");
        foreach (var a in addresses)
        {
            table.AddRow(a);
        }

        console.Write(table);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Clears the console output area.
/// </summary>
sealed class ClearInteractiveCommand : InteractiveCommandBase
{
    public ClearInteractiveCommand() : base("clear", "Clear the screen", "cls", "c") { }
    /// <inheritdoc />
    public override Task ExecuteAsync(string[] args, IAnsiConsole console, IServiceProvider services)
    {
        AnsiConsole.Clear();
        return Task.CompletedTask;
    }
}

/// <summary>
/// Displays help information (list of commands, aliases, descriptions).
/// </summary>
sealed class HelpInteractiveCommand : InteractiveCommandBase
{
    public HelpInteractiveCommand() : base("help", "List available commands", "?") { }
    /// <inheritdoc />
    public override Task ExecuteAsync(string[] args, IAnsiConsole console, IServiceProvider services)
    {
        var commands = services.GetServices<IInteractiveCommand>()
        .OrderBy(c => c.Name)
        .ToList();
        var table = new Table().Border(TableBorder.Minimal).Title("[bold cyan]Commands[/]");
        table.AddColumn("Command"); table.AddColumn("Aliases"); table.AddColumn("Description");
        foreach (var c in commands)
        {
            var aliases = c.Aliases.Count != 0 ? string.Join(", ", c.Aliases.Where(a => !string.Equals(a, c.Name, StringComparison.OrdinalIgnoreCase))) : string.Empty;
            table.AddRow(c.Name, aliases, c.Description);
        }
        console.Write(table);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Initiates graceful shutdown of the running application.
/// </summary>
sealed class QuitInteractiveCommand : InteractiveCommandBase
{
    public QuitInteractiveCommand() : base("quit", "Stop the server", "exit", "q") { }

    /// <inheritdoc />
    public override Task ExecuteAsync(string[] args, IAnsiConsole console, IServiceProvider services)
    {
        console.MarkupLine("[red]Stopping...[/]");
        services.GetRequiredService<IHostApplicationLifetime>().StopApplication();
        return Task.CompletedTask;
    }
}

/// <summary>
/// Dumps key runtime metrics (requests, latency, memory, GC, threads, handles).
/// </summary>
sealed class MetricsInteractiveCommand : InteractiveCommandBase
{
    public MetricsInteractiveCommand() : base("metrics", "Dump key runtime metrics", "dump") { }
    /// <inheritdoc />
    public override Task ExecuteAsync(string[] args, IAnsiConsole console, IServiceProvider services)
    {
        var stats = services.GetRequiredService<InteractiveCliRuntimeStats>();
        var env = services.GetRequiredService<IWebHostEnvironment>();
        var proc = Process.GetCurrentProcess();
        var managed = GC.GetTotalMemory(false);
        var avgLatency = stats.TotalRequests == 0 ? 0 : stats.TotalLatencyMs / (double)stats.TotalRequests;
        var failureRate = stats.TotalRequests == 0 ? 0 : (stats.TotalFailures / (double)stats.TotalRequests) * 100.0;

        var table = new Table().Border(TableBorder.Minimal).Title("[bold cyan]Metrics[/]");
        table.AddColumn("Metric"); table.AddColumn("Value");
        table.AddRow("Environment", env.EnvironmentName);
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

/// <summary>
/// Extensions for wiring a middleware that collects request latency for status metrics.
/// </summary>
public static class CommandInteractiveConsoleMiddlewareExtensions
{
    /// <summary>
    /// Adds a middleware to collect latency and error statistics used by the status interactive command.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The configured <see cref="WebApplication"/>.</returns>
    public static WebApplication UseCommandInteractiveStats(this WebApplication app)
    {
        var stats = app.Services.GetRequiredService<InteractiveCliRuntimeStats>();
        app.Use(async (ctx, next) =>
        {
            var sw = Stopwatch.StartNew();
            try
            {
                System.Threading.Interlocked.Increment(ref stats.TotalRequests);
                await next();
                if (ctx.Response.StatusCode >= 500)
                {
                    System.Threading.Interlocked.Increment(ref stats.TotalFailures);
                }
            }
            finally
            {
                sw.Stop();
                System.Threading.Interlocked.Add(ref stats.TotalLatencyMs, sw.ElapsedMilliseconds);
            }
        });

        return app;
    }
}

/// <summary>
/// Provides detailed runtime information (.NET, process, GC, configuration).
/// </summary>
sealed class InfoInteractiveCommand : InteractiveCommandBase
{
    public InfoInteractiveCommand() : base("info", ".NET runtime & process info") { }
    public override Task ExecuteAsync(string[] args, IAnsiConsole console, IServiceProvider services)
    {
        var proc = Process.GetCurrentProcess();
        var gcMode = GCSettings.IsServerGC ? "Server" : "Workstation";
        var latency = GCSettings.LatencyMode;
        var framework = RuntimeInformation.FrameworkDescription;
        var os = RuntimeInformation.OSDescription;
        var arch = RuntimeInformation.ProcessArchitecture.ToString();
        var pid = proc.Id.ToString();
        var config = GetBuildConfiguration();
        var table = new Table().Border(TableBorder.Minimal).Title("[bold cyan]Info[/]");
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

/// <summary>
/// Forces a full GC collection and reports reclaimed memory.
/// </summary>
sealed class GcCollectInteractiveCommand : InteractiveCommandBase
{
    public GcCollectInteractiveCommand() : base("gc", "Force GC collect and show freed memory", "collect") { }
    public override Task ExecuteAsync(string[] args, IAnsiConsole console, IServiceProvider services)
    {
        var before = GC.GetTotalMemory(false);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var after = GC.GetTotalMemory(true);
        var freedMb = (before - after) / (1024 * 1024.0);
        var table = new Table().Border(TableBorder.Minimal).Title("[bold cyan]GC Collect[/]");
        table.AddColumn("Metric"); table.AddColumn("Value");
        table.AddRow("Before.ManagedMB", (before / (1024 * 1024.0)).ToString("F2"));
        table.AddRow("After.ManagedMB", (after / (1024 * 1024.0)).ToString("F2"));
        table.AddRow("FreedMB", freedMb.ToString("F2"));
        table.AddRow("Gen0", GC.CollectionCount(0).ToString());
        table.AddRow("Gen1", GC.CollectionCount(1).ToString());
        table.AddRow("Gen2", GC.CollectionCount(2).ToString());
        console.Write(table);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Displays thread pool statistics (available/used/min/max/pending work items).
/// </summary>
sealed class ThreadsInteractiveCommand : InteractiveCommandBase
{
    public ThreadsInteractiveCommand() : base("threads", "Thread pool statistics") { }
    public override Task ExecuteAsync(string[] args, IAnsiConsole console, IServiceProvider services)
    {
        ThreadPool.GetAvailableThreads(out var workerAvail, out var ioAvail);
        ThreadPool.GetMaxThreads(out var workerMax, out var ioMax);
        ThreadPool.GetMinThreads(out var workerMin, out var ioMin);
        var workerUsed = workerMax - workerAvail;
        var ioUsed = ioMax - ioAvail;
        var pending = GetPendingWorkItemCountSafe();
        var table = new Table().Border(TableBorder.Minimal).Title("[bold cyan]ThreadPool[/]");
        table.AddColumn("Metric"); table.AddColumn("Value");
        table.AddRow("Worker.Max", workerMax.ToString());
        table.AddRow("Worker.Min", workerMin.ToString());
        table.AddRow("Worker.Available", workerAvail.ToString());
        table.AddRow("Worker.Used", workerUsed.ToString());
        table.AddRow("IO.Max", ioMax.ToString());
        table.AddRow("IO.Min", ioMin.ToString());
        table.AddRow("IO.Available", ioAvail.ToString());
        table.AddRow("IO.Used", ioUsed.ToString());
        table.AddRow("PendingWorkItems", pending.ToString());
        console.Write(table);
        return Task.CompletedTask;

        static long GetPendingWorkItemCountSafe()
        {
            try
            {
                return ThreadPool.PendingWorkItemCount; // .NET6+
            }
            catch
            {
                return -1;
            }
        }
    }
}

// ...remaining existing command and middleware classes unchanged...
