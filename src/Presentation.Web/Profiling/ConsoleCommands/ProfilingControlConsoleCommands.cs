// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using System.Globalization;
using BridgingIT.DevKit.Common;
using Spectre.Console;

/// <summary>Shows profiling availability and the active session.</summary>
/// <example><code>profiling status</code></example>
public sealed class ProfilingStatusConsoleCommand()
    : ProfilingConsoleCommandBase("status", "Show profiling availability and active collection")
{
    /// <inheritdoc />
    public override async Task ExecuteAsync(
        IAnsiConsole console,
        IServiceProvider services,
        CancellationToken cancellationToken = default
    )
    {
        var control = GetRequired<IProfilingControlService>(console, services);
        if (control is null)
        {
            return;
        }

        var result = await control.GetStatusAsync(cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            WriteErrors(console, result);
            return;
        }

        var status = result.Value;
        var table = new Table()
            .Border(TableBorder.Minimal)
            .AddColumn("Property")
            .AddColumn("Value");
        table.AddRow("Enabled", status.Enabled ? "[green]Yes[/]" : "[yellow]No[/]");
        table.AddRow("Available", status.Available ? "[green]Yes[/]" : "[yellow]No[/]");
        table.AddRow("Session", Markup.Escape(status.Session?.Identity.Key ?? "None"));
        table.AddRow("State", Markup.Escape(status.Session?.State.ToString() ?? "Idle"));
        table.AddRow("Nodes", status.Participations.Count.ToString(CultureInfo.InvariantCulture));
        console.Write(table);
    }
}

/// <summary>Starts profiling collection with optional core setting overrides.</summary>
/// <example><code>profiling start --name warmup --interval 500ms --duration 30s</code></example>
public sealed class ProfilingStartConsoleCommand()
    : ProfilingConsoleCommandBase("start", "Start a profiling collection session")
{
    private TimeSpan? samplingInterval;
    private TimeSpan? parsedDuration;

    /// <summary>Gets or sets the optional session name.</summary>
    /// <example><code>command.Name = "warm-up";</code></example>
    [ConsoleCommandOption("name", Alias = "n", Description = "Optional session name")]
    public string SessionName { get; set; }

    /// <summary>Gets or sets the optional sampling interval text.</summary>
    /// <example><code>command.Interval = "500ms";</code></example>
    [ConsoleCommandOption("interval", Alias = "i", Description = "Sampling interval (ms/s/m/h or TimeSpan)")]
    public string Interval { get; set; }

    /// <summary>Gets or sets the optional collection duration text.</summary>
    /// <example><code>command.Duration = "30s";</code></example>
    [ConsoleCommandOption("duration", Alias = "d", Description = "Collection duration (ms/s/m/h or TimeSpan)")]
    public string Duration { get; set; }

    /// <inheritdoc />
    public override void OnAfterBind(IAnsiConsole console, string[] tokens)
    {
        this.samplingInterval = ParseOptional(this.Interval, "interval");
        this.parsedDuration = ParseOptional(this.Duration, "duration");
    }

    /// <inheritdoc />
    public override async Task ExecuteAsync(
        IAnsiConsole console,
        IServiceProvider services,
        CancellationToken cancellationToken = default
    )
    {
        var control = GetRequired<IProfilingControlService>(console, services);
        if (control is null)
        {
            return;
        }

        var result = await control
            .StartAsync(
                new ProfilingStartRequest(
                    this.SessionName,
                    this.samplingInterval,
                    this.parsedDuration
                ),
                cancellationToken
            )
            .ConfigureAwait(false);
        if (result.IsFailure)
        {
            WriteErrors(console, result);
            return;
        }

        WriteControlResult(console, result.Value);
    }

    private static TimeSpan? ParseOptional(string value, string optionName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (ProfilingDurationParser.TryParse(value, out var duration))
        {
            return duration;
        }

        throw new ArgumentException(
            $"Invalid --{optionName} value '{value}'. Use ms, s, m, h, or a standard TimeSpan."
        );
    }
}

/// <summary>Stops the active profiling session.</summary>
/// <example><code>profiling stop</code></example>
public sealed class ProfilingStopConsoleCommand()
    : ProfilingConsoleCommandBase("stop", "Stop the active profiling collection session")
{
    /// <inheritdoc />
    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken = default) =>
        await ExecuteControlAsync(console, services, cancellationToken).ConfigureAwait(false);

    private static async Task ExecuteControlAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken)
    {
        var control = GetRequired<IProfilingControlService>(console, services);
        if (control is null) return;
        var result = await control.StopAsync(cancellationToken).ConfigureAwait(false);
        if (result.IsFailure) WriteErrors(console, result); else WriteControlResult(console, result.Value);
    }
}

/// <summary>Captures a manual snapshot, optionally in a named standalone session.</summary>
/// <example><code>profiling snapshot --name checkpoint</code></example>
public sealed class ProfilingSnapshotConsoleCommand()
    : ProfilingConsoleCommandBase("snapshot", "Capture one manual profiling snapshot")
{
    /// <summary>Gets or sets the optional standalone session name.</summary>
    /// <example><code>command.SessionName = "checkpoint";</code></example>
    [ConsoleCommandOption("name", Alias = "n", Description = "Optional standalone session name")]
    public string SessionName { get; set; }

    /// <inheritdoc />
    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var control = GetRequired<IProfilingControlService>(console, services);
        if (control is null) return;
        var result = await control.SnapshotAsync(this.SessionName, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure) WriteErrors(console, result); else WriteControlResult(console, result.Value);
    }
}

/// <summary>Requests one normal deployment-wide garbage collection action.</summary>
/// <example><code>profiling gc</code></example>
public sealed class ProfilingGarbageCollectionConsoleCommand()
    : ProfilingConsoleCommandBase("gc", "Request a profiling garbage collection action")
{
    /// <inheritdoc />
    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var control = GetRequired<IProfilingControlService>(console, services);
        if (control is null) return;
        var result = await control.CollectGarbageAsync(cancellationToken).ConfigureAwait(false);
        if (result.IsFailure) WriteErrors(console, result); else WriteControlResult(console, result.Value);
    }
}

/// <summary>Adds a named phase marker to the active session.</summary>
/// <example><code>profiling mark --name "load started"</code></example>
public sealed class ProfilingMarkConsoleCommand()
    : ProfilingConsoleCommandBase("mark", "Add a phase marker to the active profiling session")
{
    /// <summary>Gets or sets the required marker name.</summary>
    /// <example><code>command.MarkerName = "load started";</code></example>
    [ConsoleCommandOption("name", Alias = "n", Description = "Phase marker name", Required = true)]
    public string MarkerName { get; set; }

    /// <inheritdoc />
    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var control = GetRequired<IProfilingControlService>(console, services);
        if (control is null) return;
        var result = await control.AddPhaseMarkerAsync(this.MarkerName, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            WriteErrors(console, result);
            return;
        }

        console.MarkupLine(
            $"Marker [bold]{Markup.Escape(result.Value.Name)}[/] added to session [bold]{Markup.Escape(result.Value.SessionKey)}[/]"
        );
    }
}

/// <summary>Clears all stored profiling data after explicit confirmation.</summary>
/// <example><code>profiling clear --yes</code></example>
public sealed class ProfilingClearConsoleCommand()
    : ProfilingConsoleCommandBase("clear", "Clear all stored profiling data")
{
    /// <summary>Gets or sets whether the destructive reset was explicitly confirmed.</summary>
    /// <example><code>command.Confirmed = true;</code></example>
    [ConsoleCommandOption("yes", Alias = "y", Description = "Confirm removal including pinned sessions")]
    public bool Confirmed { get; set; }

    /// <inheritdoc />
    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken = default)
    {
        if (!this.Confirmed)
        {
            console.MarkupLine(
                "[yellow]No data was changed. Use --yes to remove all profiling data, including pinned sessions.[/]"
            );
            return;
        }

        var control = GetRequired<IProfilingControlService>(console, services);
        if (control is null) return;
        var result = await control.ClearAsync(true, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            WriteErrors(console, result);
            return;
        }

        console.MarkupLine(
            $"[green]Profiling data cleared:[/] {result.Value.RemovedSessionCount.ToString(CultureInfo.InvariantCulture)} sessions and "
                + $"{result.Value.RemovedSnapshotCount.ToString(CultureInfo.InvariantCulture)} snapshots removed"
        );
    }
}
