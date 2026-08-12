// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using System.Globalization;
using System.Text.Json;
using BridgingIT.DevKit.Common;
using Spectre.Console;

/// <summary>Computes an unpersisted deterministic profiling evaluation.</summary>
/// <example><code>profiling analyze --session a1b2c3d4 --node e5f6g7h8</code></example>
public sealed class ProfilingAnalyzeConsoleCommand()
    : ProfilingConsoleCommandBase("analyze", "Analyze one node timeline or two selected snapshots")
{
    private static readonly JsonSerializerOptions SerializerOptions =
        DefaultJsonSerializerOptions.Create();

    /// <summary>Gets or sets the required public session key.</summary>
    /// <example><code>command.SessionKey = "a1b2c3d4";</code></example>
    [ConsoleCommandOption("session", Alias = "s", Description = "Public session key", Required = true)]
    public string SessionKey { get; set; }

    /// <summary>Gets or sets the required public node key.</summary>
    /// <example><code>command.NodeKey = "e5f6g7h8";</code></example>
    [ConsoleCommandOption("node", Alias = "n", Description = "Public node key", Required = true)]
    public string NodeKey { get; set; }

    /// <summary>Gets or sets the optional earlier public snapshot key.</summary>
    /// <example><code>command.SnapshotAKey = "i9j0k1l2";</code></example>
    [ConsoleCommandOption("snapshot-a", Description = "Earlier public snapshot key")]
    public string SnapshotAKey { get; set; }

    /// <summary>Gets or sets the optional later public snapshot key.</summary>
    /// <example><code>command.SnapshotBKey = "m3n4o5p6";</code></example>
    [ConsoleCommandOption("snapshot-b", Description = "Later public snapshot key")]
    public string SnapshotBKey { get; set; }

    /// <summary>Gets or sets whether the result is written as JSON.</summary>
    /// <example><code>command.Json = true;</code></example>
    [ConsoleCommandOption("json", Alias = "j", Description = "Write the computed result as JSON")]
    public bool Json { get; set; }

    /// <inheritdoc />
    public override void OnAfterBind(IAnsiConsole console, string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(this.SnapshotAKey) != string.IsNullOrWhiteSpace(this.SnapshotBKey))
        {
            throw new ArgumentException("Options --snapshot-a and --snapshot-b must be supplied together.");
        }
    }

    /// <inheritdoc />
    public override async Task ExecuteAsync(
        IAnsiConsole console,
        IServiceProvider services,
        CancellationToken cancellationToken = default
    )
    {
        var queries = GetRequired<IProfilingQueryService>(console, services);
        if (queries is null)
        {
            return;
        }

        var result = await queries
            .EvaluateAsync(
                new ProfilingEvaluationRequest(
                    this.SessionKey,
                    this.NodeKey,
                    this.SnapshotAKey,
                    this.SnapshotBKey
                ),
                cancellationToken
            )
            .ConfigureAwait(false);
        if (result.IsFailure)
        {
            WriteErrors(console, result);
            return;
        }

        if (this.Json)
        {
            console.Write(new Text(JsonSerializer.Serialize(result.Value, SerializerOptions) + Environment.NewLine));
            return;
        }

        WriteHumanResult(console, result.Value);
    }

    private static void WriteHumanResult(IAnsiConsole console, ProfilingEvaluationResult result)
    {
        var summary = new Table()
            .Border(TableBorder.Minimal)
            .AddColumn("Scope")
            .AddColumn("Value");
        summary.AddRow("Mode", Markup.Escape(result.Scope.Mode.ToString()));
        summary.AddRow("Session", Markup.Escape(result.Scope.SessionKey));
        summary.AddRow("Node", Markup.Escape(result.Scope.NodeKey));
        summary.AddRow("Snapshots", result.Scope.SnapshotCount.ToString(CultureInfo.InvariantCulture));
        summary.AddRow("Sufficiency", Markup.Escape(result.DataQuality.Sufficiency.ToString()));
        summary.AddRow("Provisional", result.Scope.Provisional ? "Yes" : "No");
        console.Write(summary);

        if (result.KPIs.Count > 0)
        {
            var kpis = new Table().Border(TableBorder.Minimal).AddColumn("KPI").AddColumn("Value");
            foreach (var kpi in result.KPIs)
            {
                kpis.AddRow(
                    Markup.Escape(kpi.Identifier),
                    kpi.Value is null
                        ? "[grey]Unavailable[/]"
                        : Markup.Escape($"{kpi.Value.Value.ToString("0.###", CultureInfo.InvariantCulture)} {kpi.Unit}")
                );
            }

            console.Write(kpis);
        }

        if (result.Signals.Count == 0)
        {
            console.MarkupLine("[green]No evidence-backed signals were emitted.[/]");
        }
        else
        {
            var signals = new Table()
                .Border(TableBorder.Minimal)
                .AddColumn("Signal")
                .AddColumn("Label")
                .AddColumn("Confidence")
                .AddColumn("Explanation")
                .AddColumn("Action");
            foreach (var signal in result.Signals)
            {
                signals.AddRow(
                    Markup.Escape(signal.Identifier),
                    Markup.Escape(signal.Label.ToString()),
                    Markup.Escape(signal.Confidence.ToString()),
                    Markup.Escape(signal.Explanation),
                    Markup.Escape(signal.SuggestedAction)
                );
            }

            console.Write(signals);
        }

        foreach (var limitation in result.Limitations)
        {
            console.MarkupLine($"[yellow]Limitation:[/] {Markup.Escape(limitation)}");
        }
    }
}
