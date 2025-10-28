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
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Threading; // added for ThreadPool

static class ConsoleCommandHistory
{
    private static readonly object sync = new();
    private static bool loaded;
    private static readonly List<string> items = [];
    private static string filePath;
    public static string LastError { get; private set; } = string.Empty;
    public static void Initialize(string assemblyName = null)
    {
        lock (sync)
        {
            if (loaded)
            {
                return;
            }

            var asmName = string.IsNullOrWhiteSpace(assemblyName) ? "unknown" : assemblyName;
            filePath = Path.Combine(Path.GetTempPath(), $"bitdevkit_cli_history_{asmName}.txt");
            if (File.Exists(filePath))
            {
                try { items.AddRange(File.ReadAllLines(filePath).Where(l => !string.IsNullOrWhiteSpace(l))); } catch (Exception ex) { LastError = ex.Message; }
            }
            loaded = true;
        }
    }
    public static void Append(string line)
    {
        lock (sync)
        {
            items.Add(line);
            try { File.AppendAllText(filePath, line + Environment.NewLine); }
            catch (Exception ex)
            {
                if (string.IsNullOrWhiteSpace(LastError))
                {
                    LastError = ex.Message;
                }
            }
        }
    }
    public static IReadOnlyList<string> GetAll() { lock (sync) { return items.ToList(); } }
    public static void ClearKeepLast(int keepLast)
    {
        lock (sync)
        {
            if (keepLast <= 0)
            {
                items.Clear();
                try
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }
                }
                catch (Exception ex)
                {
                    if (string.IsNullOrWhiteSpace(LastError))
                    {
                        LastError = ex.Message;
                    }
                }
                return;
            }
            if (keepLast >= items.Count)
            {
                return;
            }

            var retained = items.Skip(Math.Max(0, items.Count - keepLast)).ToList();
            items.Clear(); items.AddRange(retained);
            try { File.WriteAllLines(filePath, retained); }
            catch (Exception ex)
            {
                if (string.IsNullOrWhiteSpace(LastError))
                {
                    LastError = ex.Message;
                }
            }
        }
    }
}

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers console commands for non-interactive usage (single-run invocation in console apps).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional builder for adding additional commands.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddConsoleCommands(this IServiceCollection services, Action<ConsoleCommandsBuilder> configure)
    {
        services.AddSingleton(_ => AnsiConsole.Create(new AnsiConsoleSettings { Ansi = AnsiSupport.Detect, ColorSystem = ColorSystemSupport.Detect }));

        services.AddTransient<IConsoleCommand, HelpConsoleCommand>();
        services.AddTransient<IConsoleCommand, InfoConsoleCommand>();

        if (configure is not null)
        {
            configure(new ConsoleCommandsBuilder(services));
        }

        return services;
    }

    /// <summary>
    /// Registers interactive console commands and supporting runtime services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional builder for additional commands.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddConsoleCommandsInteractive(this IServiceCollection services, Action<ConsoleCommandsBuilder> configure = null)
    {
        services.AddSingleton(_ => AnsiConsole.Create(new AnsiConsoleSettings { Ansi = AnsiSupport.Detect, ColorSystem = ColorSystemSupport.Detect }));
        services.AddSingleton<ConsoleCommandInteractiveRuntimeStats>();

        // built-in commands
        services.AddTransient<IConsoleCommand, StatusConsoleCommand>();
        services.AddTransient<IConsoleCommand, MemoryConsoleCommand>();
        services.AddTransient<IConsoleCommand, EnvConsoleCommand>();
        services.AddTransient<IConsoleCommand, KestrelPortsConsoleCommand>();
        services.AddTransient<IConsoleCommand, ClearConsoleCommand>();
        services.AddTransient<IConsoleCommand, HelpConsoleCommand>();
        services.AddTransient<IConsoleCommand, QuitConsoleCommand>();
        services.AddTransient<IConsoleCommand, MetricsConsoleCommand>();
        services.AddTransient<IConsoleCommand, InfoConsoleCommand>();
        services.AddTransient<IConsoleCommand, GcCollectConsoleCommand>();
        services.AddTransient<IConsoleCommand, ThreadsConsoleCommand>();
        services.AddTransient<IConsoleCommand, RestartConsoleCommand>();
        services.AddTransient<IConsoleCommand, SampleConsoleCommand>();
        services.AddTransient<IConsoleCommand, HistoryListConsoleCommand>();
        services.AddTransient<IConsoleCommand, HistoryClearConsoleCommand>();
        services.AddTransient<IConsoleCommand, HistorySearchConsoleCommand>();
        services.AddTransient<IConsoleCommand, EchoConsoleCommand>();
        services.AddTransient<IConsoleCommand, BrowseConsoleCommand>();

        // extra command registrations
        if (configure is not null)
        {
            configure(new ConsoleCommandsBuilder(services));
        }

        // diag group (interactive)
        services.AddTransient<IConsoleCommand, DiagGcConsoleCommand>();
        services.AddTransient<IConsoleCommand, DiagThreadsConsoleCommand>();
        services.AddTransient<IConsoleCommand, DiagMemConsoleCommand>();
        services.AddTransient<IConsoleCommand, DiagPerfConsoleCommand>();
        services.AddTransient<IConsoleCommand, DiagEnvConsoleCommand>();

        return services;
    }
}

/// <summary>
/// Provides an interactive command-based console that runs inside a locally hosted Kestrel <see cref="WebApplication"/>.
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Enables the interactive console loop in a web application when running locally.
    /// </summary>
    /// <param name="app">The web application instance.</param>
    /// <param name="startupDelay">Optional delay before starting the loop.</param>
    public static void UseConsoleCommandsInteractive(this WebApplication app, TimeSpan? startupDelay = null)
    {
        if (!IsLocalAndKestrel(app))
        {
            return;
        }

        app.Lifetime.ApplicationStarted.Register(() =>
        {
            _ = Task.Run(async () =>
            {
                const string restartMarkerVar = "BITDEVKIT_RESTARTING";
                // If this instance was spawned by a restart, clear the marker so future restarts are allowed.
                if (Environment.GetEnvironmentVariable(restartMarkerVar) == "1")
                {
                    Environment.SetEnvironmentVariable(restartMarkerVar, null);
                }

                EnsureHistoryLoaded();
                if (startupDelay.HasValue && startupDelay.Value.TotalMilliseconds > 0)
                {
                    await Task.Delay(startupDelay.Value);
                }
                var console = app.Services.GetRequiredService<IAnsiConsole>();

                await RunLoopAsync(app, console);
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
    private static async Task RunLoopAsync(WebApplication app, IAnsiConsole console)
    {
        PrintBanner(console);

        while (!app.Lifetime.ApplicationStopping.IsCancellationRequested)
        {
            console.Markup("[grey]> [/]");

            var line = Console.ReadLine();
            if (line is null) { break; }
            if (string.IsNullOrWhiteSpace(line)) { continue; }

            ConsoleCommandHistory.Append(line);
            var tokens = SplitArgs(line);
            if (tokens.Length == 0) { continue; }

            var primary = tokens[0];
            using var scope = app.Services.CreateScope();
            var all = scope.ServiceProvider.GetServices<IConsoleCommand>().ToList();
            IConsoleCommand cmd = null;
            var groupedCandidates = all.OfType<IGroupedConsoleCommand>()
                .Where(g => string.Equals(g.GroupName, primary, StringComparison.OrdinalIgnoreCase)
                    || g.GroupAliases?.Any(a => string.Equals(a, primary, StringComparison.OrdinalIgnoreCase)) == true)
                .ToList();
            var consumed = 1;
            if (groupedCandidates.Count != 0)
            {
                if (tokens.Length == 1)
                {
                    // list subcommands for this group
                    var table = new Table().Border(TableBorder.Minimal).Title($"[bold cyan]{primary} (group) subcommands[/]");
                    table.AddColumn("Sub"); table.AddColumn("Description");
                    foreach (var sc in groupedCandidates.OrderBy(c => c.Name)) { table.AddRow(sc.Name, sc.Description); }
                    console.Write(table); continue;
                }
                var sub = tokens[1]; consumed = 2; cmd = groupedCandidates.FirstOrDefault(c => c.Matches(sub));
                if (cmd is null) { console.MarkupLine($"[yellow]Unknown subcommand '{sub}' for group '{primary}'.[/]"); continue; }
            }
            else { cmd = all.FirstOrDefault(c => c.Matches(primary)); }
            if (cmd is null) { console.MarkupLine("[yellow]Unknown command[/]. Type [bold]help[/] for list."); continue; }
            console.MarkupLine("[cyan]=== " + (cmd is IGroupedConsoleCommand gc ? gc.GroupName + " " + cmd.Name : cmd.Name) + " ===[/][grey] " + cmd.Description + "[/]");

            var bindTokens = tokens.Skip(consumed).ToArray();
            var (ok, errors) = ConsoleCommandBinder.TryBind(cmd, bindTokens);
            if (!ok)
            {
                foreach (var e in errors)
                {
                    console.MarkupLine("[red]" + e + "[/]");
                }

                ConsoleCommandBinder.WriteHelp(console, cmd, detailed: true); continue;
            }
            try { cmd.OnAfterBind(console, tokens); }
            catch (Exception hookEx) { console.MarkupLine("[red]Post-bind validation failed:[/] " + hookEx.Message); continue; }
            try { await cmd.ExecuteAsync(console, scope.ServiceProvider); }
            catch (Exception ex) { console.MarkupLine("[red]Command failed:[/] " + ex.Message); }
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
        console.MarkupLine("[grey]Type [bold]help[/]/[bold]?[/] for commands. Examples: [bold]status[/], [bold]mem[/], [bold]env[/], [bold]ports[/], [bold]metrics[/], [bold]restart[/].[/]");
        console.Write(new Rule().Centered().RuleStyle("grey"));
    }

    /// <summary>
    /// Ensures history file has been loaded for the current assembly context.
    /// </summary>
    private static void EnsureHistoryLoaded()
    {
        ConsoleCommandHistory.Initialize(
            Assembly.GetEntryAssembly()?.GetName().Name);
    }
}

/// <summary>
/// Fluent builder for interactive console command registration.
/// </summary>
/// <remarks>Initializes builder with the provided service collection.</remarks>
public class ConsoleCommandsBuilder(IServiceCollection services)
{
    /// <summary>Gets the DI service collection used for registrations.</summary>
    public IServiceCollection Services { get; } = services;

    /// <summary>
    /// Adds a transient console command implementation.
    /// </summary>
    /// <typeparam name="TCommand">The command type implementing <see cref="IConsoleCommand"/>.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public ConsoleCommandsBuilder AddCommand<TCommand>() where TCommand : class, IConsoleCommand
    {
        this.Services.AddTransient<IConsoleCommand, TCommand>();
        return this;
    }
}

/// <summary>Command binding utility (reflection + caching).</summary>
static class ConsoleCommandBinder
{
    private static readonly Dictionary<Type, ConsoleCommandMeta> cache = [];
    private static readonly object sync = new();

    /// <summary>
    /// Attempts to bind tokens to the properties of a console command.
    /// </summary>
    /// <param name="cmd">The command instance to bind values to.</param>
    /// <param name="tokens">The string tokens representing option/argument values.</param>
    /// <returns>Tuple of success flag and list of error messages.</returns>
    public static (bool ok, List<string> errors) TryBind(IConsoleCommand cmd, string[] tokens)
    {
        var meta = GetMeta(cmd.GetType());
        var errors = new List<string>();
        var options = ParseOptions(tokens);
        var consumedIndices = new HashSet<int>(options.SelectMany(kvp => kvp.Value.Indices));
        var positionals = tokens.Where((t, i) => !consumedIndices.Contains(i)).ToList();

        // Bind options
        foreach (var o in meta.Options)
        {
            if (options.TryGetValue(o.Name, out var present) || (o.Alias != null && options.TryGetValue(o.Alias, out present)))
            {
                var valToken = present.Value;
                object value = null;
                if (o.Property.PropertyType == typeof(bool))
                {
                    // flag: presence sets true unless explicit false given
                    var raw = valToken;
                    value = raw is null || raw.Equals("true", StringComparison.OrdinalIgnoreCase) || raw == "1";
                    if (raw is not null && (raw.Equals("false", StringComparison.OrdinalIgnoreCase) || raw == "0"))
                    {
                        value = false;
                    }
                }
                else
                {
                    if (valToken is null)
                    {
                        errors.Add($"Option --{o.Name} requires a value.");
                        continue;
                    }
                    if (!TryConvert(valToken, o.Property.PropertyType, out value))
                    {
                        errors.Add($"Invalid value '{valToken}' for --{o.Name} (expected {FriendlyType(o.Property.PropertyType)}). ");
                        continue;
                    }
                }
                o.Property.SetValue(cmd, value);
            }
            else
            {
                if (o.Required)
                {
                    errors.Add($"Missing required option --{o.Name}.");
                }
                else if (o.Default is not null)
                {
                    o.Property.SetValue(cmd, o.Default);
                }
            }
        }

        // Bind positional
        foreach (var a in meta.Arguments.OrderBy(a => a.Order))
        {
            if (a.Order < positionals.Count)
            {
                var raw = positionals[a.Order];
                if (!TryConvert(raw, a.Property.PropertyType, out var value))
                {
                    errors.Add($"Invalid value '{raw}' for argument #{a.Order} ({a.Property.Name}) expected {FriendlyType(a.Property.PropertyType)}.");
                    continue;
                }
                a.Property.SetValue(cmd, value);
            }
            else if (a.Required)
            {
                errors.Add($"Missing required argument at position {a.Order} ({a.Property.Name}).");
            }
        }

        return (errors.Count == 0, errors);
    }

    /// <summary>
    /// Writes command help including options and arguments.
    /// </summary>
    /// <param name="console">The console to write help to.</param>
    /// <param name="cmd">The command instance to generate help for.</param>
    /// <param name="detailed">Whether to include detailed option examples.</param>
    public static void WriteHelp(IAnsiConsole console, IConsoleCommand cmd, bool detailed = false)
    {
        var meta = GetMeta(cmd.GetType());
        var table = new Table().Border(TableBorder.Minimal).Title($"[bold cyan]{cmd.Name}[/]");
        table.AddColumn("Key"); table.AddColumn("Value");
        table.AddRow("Description", cmd.Description);
        if (meta.Options.Any())
        {
            var optText = string.Join("\n", meta.Options.Select(o => $"--{o.Name}{(o.Alias != null ? "/-" + o.Alias : "")}: {o.Description} {(o.Required ? "(required)" : "")} {(o.Default != null ? "(default=" + o.Default + ")" : "")} ({FriendlyType(o.Property.PropertyType)})"));
            table.AddRow("Options", optText);
        }
        if (meta.Arguments.Any())
        {
            var argText = string.Join("\n", meta.Arguments.OrderBy(a => a.Order).Select(a => $"{a.Order}: {a.Property.Name} {a.Description ?? ""} {(a.Required ? "(required)" : "")} ({FriendlyType(a.Property.PropertyType)})"));
            table.AddRow("Arguments", argText);
        }
        if (detailed && meta.Options.Any())
        {
            table.AddRow("Example", $"{cmd.Name} " + string.Join(' ', meta.Options.Take(2).Select(o => $"--{o.Name}{(o.Property.PropertyType != typeof(bool) ? "=value" : "")}")));
        }
        console.Write(table);
    }

    /// <summary>
    /// Gets cached or newly built metadata for a command type.
    /// </summary>
    /// <param name="t">The command type.</param>
    /// <returns>The metadata for the command.</returns>
    private static ConsoleCommandMeta GetMeta(Type t)
    {
        lock (sync)
        {
            if (!cache.TryGetValue(t, out var meta))
            {
                var props = t.GetProperties(BindingFlags.Instance | BindingFlags.Public);
                var options = new List<OptionMeta>();
                var args = new List<ArgumentMeta>();
                foreach (var p in props)
                {
                    var opt = p.GetCustomAttribute<ConsoleCommandOptionAttribute>();
                    if (opt is not null)
                    {
                        options.Add(new OptionMeta
                        {
                            Name = opt.Name,
                            Alias = opt.Alias,
                            Description = opt.Description ?? p.Name,
                            Required = opt.Required,
                            Default = opt.Default,
                            Property = p
                        });
                        continue;
                    }
                    var arg = p.GetCustomAttribute<ConsoleCommandArgumentAttribute>();
                    if (arg is not null)
                    {
                        args.Add(new ArgumentMeta
                        {
                            Order = arg.Order,
                            Description = arg.Description,
                            Required = arg.Required,
                            Property = p
                        });
                    }
                }
                meta = new ConsoleCommandMeta(options, args);
                cache[t] = meta;
            }
            return meta;
        }
    }
    private static string FriendlyType(Type t) => t.Name switch
    {
        nameof(String) => "string",
        nameof(Int32) => "int",
        nameof(Int64) => "long",
        nameof(Boolean) => "bool",
        nameof(DateTime) => "datetime",
        nameof(Guid) => "guid",
        nameof(TimeSpan) => "timespan",
        _ => t.IsEnum ? "enum" : t.Name.ToLowerInvariant()
    };

    private static bool TryConvert(string raw, Type type, out object value)
    {
        value = null;
        if (type == typeof(string)) { value = raw; return true; }
        if (type == typeof(int) || type == typeof(int?)) { if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) { value = i; return true; } return false; }
        if (type == typeof(long) || type == typeof(long?)) { if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)) { value = l; return true; } return false; }
        if (type == typeof(bool) || type == typeof(bool?)) { if (bool.TryParse(raw, out var b)) { value = b; return true; } if (raw == "1") { value = true; return true; } if (raw == "0") { value = false; return true; } return false; }
        if (type == typeof(Guid) || type == typeof(Guid?)) { if (Guid.TryParse(raw, out var g)) { value = g; return true; } return false; }
        if (type == typeof(DateTime) || type == typeof(DateTime?)) { if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt)) { value = dt; return true; } return false; }
        if (type == typeof(TimeSpan) || type == typeof(TimeSpan?)) { if (TimeSpan.TryParse(raw, CultureInfo.InvariantCulture, out var ts)) { value = ts; return true; } return false; }
        if (type.IsEnum) { if (Enum.TryParse(type, raw, true, out var ev)) { value = ev; return true; } return false; }
        // fallback
        try
        {
            value = Convert.ChangeType(raw, type, CultureInfo.InvariantCulture);
            return true;
        }
        catch { return false; }
    }

    private static Dictionary<string, ParsedOption> ParseOptions(string[] tokens)
    {
        var dict = new Dictionary<string, ParsedOption>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < tokens.Length; i++)
        {
            var t = tokens[i];
            if (t.StartsWith("--"))
            {
                var trimmed = t.Substring(2);
                string name; string val = null;
                var eqIdx = trimmed.IndexOf('=');
                if (eqIdx >= 0)
                {
                    name = trimmed[..eqIdx];
                    val = trimmed[(eqIdx + 1)..];
                }
                else
                {
                    name = trimmed;
                    // value maybe next token if not another option
                    if (i + 1 < tokens.Length && !tokens[i + 1].StartsWith("-"))
                    {
                        val = tokens[i + 1];
                        dict[name] = new ParsedOption(val, [i, i + 1]);
                        i++;
                        continue;
                    }
                }
                dict[name] = new ParsedOption(val, [i]);
            }
            else if (t.StartsWith('-') && t.Length > 1 && !t.StartsWith("--"))
            {
                var alias = t[1..];
                string val = null;
                if (i + 1 < tokens.Length && !tokens[i + 1].StartsWith("-"))
                {
                    val = tokens[i + 1];
                    dict[alias] = new ParsedOption(val, [i, i + 1]);
                    i++;
                }
                else
                {
                    dict[alias] = new ParsedOption(null, [i]);
                }
            }
        }
        return dict;
    }

    private sealed record ParsedOption(string Value, int[] Indices);

    private sealed record ConsoleCommandMeta(IReadOnlyList<OptionMeta> Options, IReadOnlyList<ArgumentMeta> Arguments);

    private sealed record OptionMeta
    {
        public string Name { get; init; } = default!;

        public string Alias { get; init; }

        public string Description { get; init; } = string.Empty;

        public bool Required { get; init; }

        public object Default { get; init; }

        public PropertyInfo Property { get; init; } = default!;
    }

    private sealed record ArgumentMeta
    {
        public int Order { get; init; }

        public string Description { get; init; }

        public bool Required { get; init; }

        public PropertyInfo Property { get; init; } = default!;
    }
}

// INTERFACES & BASE
public interface IConsoleCommand
{
    /// <summary>Gets the primary command name.</summary>
    string Name { get; }

    /// <summary>Gets the aliases that also trigger this command.</summary>
    IReadOnlyCollection<string> Aliases { get; }

    /// <summary>Gets a short description of the command purpose.</summary>
    string Description { get; }

    /// <summary>Determines whether user input matches the command name or an alias.</summary>
    bool Matches(string input);

    /// <summary>Hook invoked after successful binding for additional validation or adjustment.</summary>
    void OnAfterBind(IAnsiConsole console, string[] tokens);

    /// <summary>Executes the command logic asynchronously.</summary>
    Task ExecuteAsync(IAnsiConsole console, IServiceProvider services);
}

public interface IGroupedConsoleCommand : IConsoleCommand
{
    string GroupName { get; }

    IReadOnlyCollection<string> GroupAliases { get; }
}

public abstract class ConsoleCommandBase(string name, string description, params string[] aliases) : IConsoleCommand
{
    /// <summary>Gets the command name.</summary>
    public string Name { get; } = name;

    /// <summary>Gets the collection of aliases.</summary>
    public IReadOnlyCollection<string> Aliases { get; } = aliases.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Gets the description text.</summary>
    public string Description { get; } = description;

    /// <inheritdoc />
    public bool Matches(string input) => string.Equals(this.Name, input, StringComparison.OrdinalIgnoreCase) || this.Aliases.Any(a => string.Equals(a, input, StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc />
    public virtual void OnAfterBind(IAnsiConsole console, string[] tokens) { }

    /// <inheritdoc />
    public abstract Task ExecuteAsync(IAnsiConsole console, IServiceProvider services);
}

// COMMANDS
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

public class MemoryConsoleCommand : ConsoleCommandBase
{
    /// <summary>
    /// Shows managed memory, process working set and selected diagnostic counters.
    /// Usage: <c>mem</c>
    /// Examples:
    /// <list type="bullet">
    /// <item><description><c>mem</c></description></item>
    /// </list>
    /// </summary>
    public MemoryConsoleCommand() : base("memory", "Show memory usage", ["mem"]) { }

    /// <summary>Executes the memory command.</summary>
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        var proc = Process.GetCurrentProcess();
        var managed = GC.GetTotalMemory(false);
        var table = new Table().Border(TableBorder.Minimal);
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
public class KestrelPortsConsoleCommand : ConsoleCommandBase
{
    /// <summary>
    /// Lists currently bound Kestrel addresses/ports.
    /// Usage: <c>ports</c>
    /// Aliases: <c>urls</c>, <c>kestrel</c>
    /// </summary>
    public KestrelPortsConsoleCommand() : base("ports", "Show Kestrel bound addresses", "urls", "kestrel") { }

    /// <summary>Executes ports listing.</summary>
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        var server = services.GetService<IServer>();
        var feature = server?.Features.Get<IServerAddressesFeature>();
        var addresses = feature?.Addresses ?? Array.Empty<string>();
        if (addresses.Count == 0)
        {
            console.MarkupLine("[yellow]No addresses available yet.[/]");

            return Task.CompletedTask;
        }
        var table = new Table().Border(TableBorder.Minimal);
        table.AddColumn("Address");
        foreach (var a in addresses)
        {
            table.AddRow(a);
        }

        console.Write(table);

        return Task.CompletedTask;
    }
}
public class ClearConsoleCommand : ConsoleCommandBase
{
    /// <summary>
    /// Clears the console using Spectre facilities.
    /// Usage: <c>clear</c>
    /// Aliases: <c>cls</c>, <c>c</c>
    /// </summary>
    public ClearConsoleCommand() : base("clear", "Clear the screen", "cls", "c") { }

    /// <summary>Executes screen clear.</summary>
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        AnsiConsole.Clear();

        return Task.CompletedTask;
    }
}
public class HelpConsoleCommand : ConsoleCommandBase
{
    [ConsoleCommandArgument(0, Description = "Command or group name for detailed help", Required = false)] public string Target { get; set; }
    [ConsoleCommandArgument(1, Description = "Subcommand name (when first arg is a group)", Required = false)] public string Sub { get; set; }

    /// <summary>
    /// Lists all available commands or detailed help for a specific command.
    /// Usage: <c>help</c> | <c>help &lt;command&gt;</c>
    /// Alias: <c>?</c>
    /// Example: <c>help mem</c>
    /// </summary>
    public HelpConsoleCommand() : base("help", "List available commands", "?") { }

    /// <summary>Executes help listing.</summary>
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        var commands = services.GetServices<IConsoleCommand>().ToList();
        if (!string.IsNullOrWhiteSpace(this.Target))
        {
            // When both group and sub are provided attempt specific subcommand help first.
            if (!string.IsNullOrWhiteSpace(this.Sub))
            {
                var specific = commands.OfType<IGroupedConsoleCommand>()
                    .Where(g => string.Equals(g.GroupName, this.Target, StringComparison.OrdinalIgnoreCase)
                        || g.GroupAliases?.Any(a => string.Equals(a, this.Target, StringComparison.OrdinalIgnoreCase)) == true)
                    .FirstOrDefault(c => string.Equals(c.Name, this.Sub, StringComparison.OrdinalIgnoreCase) || c.Aliases.Any(a => string.Equals(a, this.Sub, StringComparison.OrdinalIgnoreCase)));
                if (specific is not null) { ConsoleCommandBinder.WriteHelp(console, specific, detailed: true); return Task.CompletedTask; }
            }
            // Try regular (non-group) command match
            var direct = commands.FirstOrDefault(c => c.Matches(this.Target));
            if (direct is not null && string.IsNullOrWhiteSpace(this.Sub)) { ConsoleCommandBinder.WriteHelp(console, direct, detailed: true); return Task.CompletedTask; }
            // Group listing (if Target matches a group)
            var grouped = commands.OfType<IGroupedConsoleCommand>()
                .Where(g => string.Equals(g.GroupName, this.Target, StringComparison.OrdinalIgnoreCase)
                    || g.GroupAliases?.Any(a => string.Equals(a, this.Target, StringComparison.OrdinalIgnoreCase)) == true)
                .Cast<IConsoleCommand>().ToList();
            if (grouped.Count != 0)
            {
                if (!string.IsNullOrWhiteSpace(this.Sub))
                {
                    console.MarkupLine($"[yellow]No such subcommand:[/] {this.Target} {this.Sub}");
                }

                var tbl = new Table().Border(TableBorder.Minimal).Title($"[bold cyan]{this.Target} (group) subcommands[/]");
                tbl.AddColumn("Sub"); tbl.AddColumn("Description");
                foreach (var sc in grouped.OrderBy(c => c.Name))
                {
                    tbl.AddRow(sc.Name, sc.Description);
                }

                console.Write(tbl); return Task.CompletedTask;
            }
            // Fallback unknown
            console.MarkupLine("[yellow]No such command or group:[/] " + this.Target);
        }
        // List all commands sorted by group then name
        var table = new Table().Border(TableBorder.Minimal);
        table.AddColumn("Command/Group"); table.AddColumn("Aliases"); table.AddColumn("Description");
        var ordered = commands
            .OrderBy(c => c is IGroupedConsoleCommand gc ? gc.GroupName : "")
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var c in ordered)
        {
            if (c is IGroupedConsoleCommand gc)
            {
                var aliasList = gc.GroupAliases?.Where(a => !string.Equals(a, gc.GroupName, StringComparison.OrdinalIgnoreCase)).ToList() ?? new List<string>();
                var cmdAliases = c.Aliases.Where(a => !string.Equals(a, c.Name, StringComparison.OrdinalIgnoreCase));
                var allAliases = aliasList.Concat(cmdAliases).Distinct(StringComparer.OrdinalIgnoreCase);
                table.AddRow(gc.GroupName + " " + c.Name, string.Join(", ", allAliases), c.Description);
            }
            else
            {
                var aliases = c.Aliases.Count != 0 ? string.Join(", ", c.Aliases.Where(a => !string.Equals(a, c.Name, StringComparison.OrdinalIgnoreCase))) : string.Empty;
                table.AddRow(c.Name, aliases, c.Description);
            }
        }
        console.Write(table); return Task.CompletedTask;
    }
}
public class QuitConsoleCommand : ConsoleCommandBase
{
    /// <summary>
    /// Signals the application lifetime to stop (graceful shutdown).
    /// Usage: <c>quit</c>
    /// Aliases: <c>exit</c>, <c>q</c>
    /// </summary>
    public QuitConsoleCommand() : base("quit", "Stop the server", "exit", "q") { }
    /// <summary>Stops application execution.</summary>
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        console.MarkupLine("[red]Stopping...[/]");
        services.GetRequiredService<IHostApplicationLifetime>().StopApplication();

        return Task.CompletedTask;
    }
}
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

public class GcCollectConsoleCommand : ConsoleCommandBase
{
    [ConsoleCommandOption("no-collect", Description = "Skip forcing GC; only show current memory stats")] public bool NoCollect { get; set; }
    /// <summary>
    /// Forces a full garbage collection and reports memory freed and collection counts (unless --no-collect specified).
    /// Usage: <c>gc</c>
    /// Alias: <c>collect</c>
    /// </summary>
    public GcCollectConsoleCommand() : base("gc", "Force GC collect and show freed memory", ["col"]) { }

    /// <summary>Executes (optional) GC collection and displays memory stats.</summary>
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        var before = GC.GetTotalMemory(false);
        if (!this.NoCollect)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        var after = GC.GetTotalMemory(false); // do not force finalizers if skipped
        var freedMb = this.NoCollect ? 0 : (before - after) / (1024 * 1024.0);
        var table = new Table().Border(TableBorder.Minimal);
        table.AddColumn("Metric"); table.AddColumn("Value");
        table.AddRow("Before.Managed MB", (before / (1024 * 1024.0)).ToString("F2"));
        table.AddRow("After.Managed MB", (after / (1024 * 1024.0)).ToString("F2"));
        table.AddRow("Freed MB", freedMb.ToString("F2"));
        table.AddRow("Mode", this.NoCollect ? "skipped" : "forced");
        table.AddRow("Gen0", GC.CollectionCount(0).ToString());
        table.AddRow("Gen1", GC.CollectionCount(1).ToString());
        table.AddRow("Gen2", GC.CollectionCount(2).ToString());
        console.Write(table);

        return Task.CompletedTask;
    }
}

public class ThreadsConsoleCommand : ConsoleCommandBase
{
    /// <summary>
    /// Displays thread pool configuration, usage and pending work item count.
    /// Usage: <c>threads</c>
    /// </summary>
    public ThreadsConsoleCommand() : base("threads", "Thread pool statistics", ["tr"]) { }

    /// <summary>Outputs thread pool statistics.</summary>
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        ThreadPool.GetAvailableThreads(out var workerAvail, out var ioAvail);
        ThreadPool.GetMaxThreads(out var workerMax, out var ioMax);
        ThreadPool.GetMinThreads(out var workerMin, out var ioMin);
        var workerUsed = workerMax - workerAvail;
        var ioUsed = ioMax - ioAvail;
        var pending = GetPendingWorkItemCountSafe();
        var table = new Table().Border(TableBorder.Minimal);
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
            try { return ThreadPool.PendingWorkItemCount; } catch { return -1; }
        }
    }
}

public class RestartConsoleCommand : ConsoleCommandBase
{
    /// <summary>
    /// Restarts the current application (development only) by spawning a new process and stopping the existing one.
    /// Usage: <c>restart</c>
    /// Aliases: <c>re</c>, <c>reload</c>
    /// </summary>
    public RestartConsoleCommand() : base("restart", "Restart the application (development only)", "re") { }
    /// <summary>Performs application restart procedure.</summary>
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        var env = services.GetRequiredService<IWebHostEnvironment>();
        if (!env.IsDevelopment()) { console.MarkupLine("[red]Restart denied: not a development environment.[/]"); return Task.CompletedTask; }
        const string markerVar = "BITDEVKIT_RESTARTING";
        var alreadyRestarting = Environment.GetEnvironmentVariable(markerVar) == "1";
        if (alreadyRestarting) { console.MarkupLine("[yellow]Restart already in progress.[/]"); return Task.CompletedTask; }
        Environment.SetEnvironmentVariable(markerVar, "1");
        var exe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exe)) { console.MarkupLine("[red]Cannot determine executable path.[/]"); return Task.CompletedTask; }
        var argList = Environment.GetCommandLineArgs().Skip(1).Select(a => a.Contains(' ') ? $"\"{a}\"" : a);
        var argsLine = string.Join(' ', argList);
        try
        {
            console.MarkupLine("[yellow]Spawning new instance...[/]");
            var startInfo = new ProcessStartInfo(exe, argsLine) { UseShellExecute = false };
            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            console.MarkupLine("[red]Failed to spawn new instance:[/] " + ex.Message);
            Environment.SetEnvironmentVariable(markerVar, null);
            return Task.CompletedTask;
        }
        console.MarkupLine("[grey]Stopping current instance...[/]");
        services.GetRequiredService<IHostApplicationLifetime>().StopApplication();

        return Task.CompletedTask;
    }
}

public class SampleConsoleCommand : ConsoleCommandBase
{
    [ConsoleCommandOption("name", Description = "Name to use", Required = true)] public string UserName { get; set; }
    [ConsoleCommandOption("count", Alias = "c", Description = "Base count", Default = 1)] public int Count { get; set; }
    [ConsoleCommandOption("enable", Alias = "e", Description = "Enable extra output")] public bool Enable { get; set; }
    [ConsoleCommandOption("guid", Description = "Optional guid")] public Guid GuidValue { get; set; }
    [ConsoleCommandArgument(0, Description = "Greeting target", Required = true)] public string Target { get; set; }
    [ConsoleCommandArgument(1, Description = "Repeat override", Required = false)] public int Repeat { get; set; }

    /// <summary>
    /// Demonstrates option & argument binding plus post-bind adjustments.
    /// Usage: <c>sample --name Bob Alice --count3 --enable</c>
    /// </summary>
    public SampleConsoleCommand() : base("sample", "Demonstration command for binding", "demo") { }
    /// <summary>Adjusts values after binding (applies Count to Repeat when not supplied).</summary>
    public override void OnAfterBind(IAnsiConsole console, string[] tokens)
    {
        // Cross-field adjustment: if Repeat not supplied (0) use Count
        if (this.Repeat <= 0)
        {
            this.Repeat = this.Count;
        }
    }
    /// <summary>Executes sample output logic.</summary>
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        var table = new Table().Border(TableBorder.Minimal);
        table.AddColumn("Field"); table.AddColumn("Value");
        table.AddRow("Name", this.UserName);
        table.AddRow("Target", this.Target);
        table.AddRow("Count", this.Count.ToString());
        table.AddRow("Repeat", this.Repeat.ToString());
        table.AddRow("Enable", this.Enable.ToString());
        table.AddRow("GuidValue", this.GuidValue == Guid.Empty ? "(empty)" : this.GuidValue.ToString());
        console.Write(table);
        if (this.Enable)
        {
            for (var i = 0; i < this.Repeat; i++)
            {
                console.MarkupLine($"[green]{i + 1}. Hello {this.Target} from {this.UserName}![/]");
            }
        }

        return Task.CompletedTask;
    }
}
public class EchoConsoleCommand : ConsoleCommandBase
{
    [ConsoleCommandArgument(0, Description = "Text to echo", Required = true)] public string Text { get; set; }
    [ConsoleCommandOption("upper", Alias = "u", Description = "Uppercase output")] public bool Upper { get; set; }
    [ConsoleCommandOption("repeat", Alias = "r", Description = "Repeat count", Default = 1)] public int Repeat { get; set; }
    [ConsoleCommandOption("color", Alias = "c", Description = "Spectre color (e.g. green, yellow)")] public string Color { get; set; }

    /// <summary>
    /// Echoes provided text with optional repetition, color and case transformation.
    /// Usage: <c>echo "Hello World" --repeat3 --color yellow --upper</c>
    /// </summary>
    public EchoConsoleCommand() : base("echo", "Echo text with optional transformations") { }
    /// <summary>Post-bind adjustments (normalizes repeat & sets default color).</summary>
    public override void OnAfterBind(IAnsiConsole console, string[] tokens)
    {
        if (this.Repeat <= 0)
        {
            this.Repeat = 1;
        }

        if (string.IsNullOrWhiteSpace(this.Color))
        {
            this.Color = "white";
        }
    }
    /// <summary>Outputs the echoed text lines.</summary>
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        var output = this.Upper ? this.Text.ToUpperInvariant() : this.Text;
        for (var i = 1; i <= this.Repeat; i++)
        {
            console.MarkupLine($"[{this.Color}]{i}. {Markup.Escape(output)}[/]");
        }
        return Task.CompletedTask;
    }
}

public class HistoryListConsoleCommand : ConsoleCommandBase, IGroupedConsoleCommand
{
    public string GroupName => "history";
    public IReadOnlyCollection<string> GroupAliases => ["hist"];
    [ConsoleCommandOption("max", Alias = "m", Description = "Max items to show", Default = 50)] public int Max { get; set; }
    public HistoryListConsoleCommand() : base("list", "List command history") { }
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        var all = ConsoleCommandHistory.GetAll(); var take = this.Max <= 0 ? 50 : this.Max;
        var seq = all.Select((l, i) => (i + 1, l)).Reverse().Take(take).Reverse();
        var table = new Table().Border(TableBorder.Minimal); table.AddColumn("#"); table.AddColumn("Command");
        foreach (var item in seq)
        {
            table.AddRow(item.Item1.ToString(), item.l);
        }

        if (!table.Rows.Any())
        {
            table.AddRow("-", "(empty)");
        }

        console.Write(table);
        var lastError = ConsoleCommandHistory.LastError; if (!string.IsNullOrWhiteSpace(lastError))
        {
            console.MarkupLine("[yellow]History IO issues detected:[/] " + lastError);
        }

        return Task.CompletedTask;
    }
}
public class HistoryClearConsoleCommand : ConsoleCommandBase, IGroupedConsoleCommand
{
    public string GroupName => "history";
    public IReadOnlyCollection<string> GroupAliases => ["hist"];
    [ConsoleCommandOption("keep-last", Alias = "k", Description = "Keep last N entries", Default = 10)] public int KeepLast { get; set; }
    public HistoryClearConsoleCommand() : base("clear", "Clear command history (optionally keep last N)") { }
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        ConsoleCommandHistory.ClearKeepLast(Math.Max(0, this.KeepLast));
        console.MarkupLine(this.KeepLast > 0 ? $"[green]History cleared; kept last {this.KeepLast} entries.[/]" : "[green]History fully cleared.[/]");

        return Task.CompletedTask;
    }
}
public class HistorySearchConsoleCommand : ConsoleCommandBase, IGroupedConsoleCommand
{
    public string GroupName => "history";
    public IReadOnlyCollection<string> GroupAliases => ["hist"];
    [ConsoleCommandArgument(0, Description = "Search text", Required = true)] public string Text { get; set; }
    public HistorySearchConsoleCommand() : base("search", "Search command history by substring") { }
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        var query = this.Text ?? string.Empty;
        var matches = ConsoleCommandHistory.GetAll().Select((l, i) => (i + 1, l))
            .Where(x => x.l.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            .Reverse().Take(100).Reverse();
        var table = new Table().Border(TableBorder.Minimal); table.AddColumn("#"); table.AddColumn("Match");
        foreach (var item in matches)
        {
            table.AddRow(item.Item1.ToString(), item.l);
        }

        if (!table.Rows.Any())
        {
            table.AddRow("-", "(none)");
        }

        console.Write(table);

        return Task.CompletedTask;
    }
}
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

// === Diagnostic helper & grouped commands (diag) ===
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
// === end diagnostic additions ===