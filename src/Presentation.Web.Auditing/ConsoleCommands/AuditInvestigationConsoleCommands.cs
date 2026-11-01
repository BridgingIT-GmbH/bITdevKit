// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Auditing;

using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

/// <summary>Lists scoped audits with bounded provider-side filters and paging.</summary>
/// <example><code>auditing list --outcome Failed --take 25</code></example>
public sealed class AuditListConsoleCommand() : AuditQueryConsoleCommandBase("list", "Search retained audit executions")
{
    /// <inheritdoc />
    protected override async Task ExecuteReadAsync(IAnsiConsole console, IAuditQueryService queries, IServiceProvider services, CancellationToken cancellationToken)
        => this.WritePage(console, await queries.SearchAsync(this.BuildFilter(), cancellationToken).ConfigureAwait(false));
}

/// <summary>Reads one permitted audit after original-source integrity verification.</summary>
/// <example><code>auditing show 235283d5-43ac-4ed1-a7c8-e7251aa27d0b --json</code></example>
public sealed class AuditShowConsoleCommand() : AuditConsoleCommandBase("show", "Inspect one audit and its permitted evidence")
{
    /// <summary>Gets or sets the exact audit identity, including retained archives.</summary>
    /// <example><code>command.AuditId = auditId;</code></example>
    [ConsoleCommandArgument(0, Description = "Audit ID", Required = true)]
    public Guid AuditId { get; set; }

    /// <inheritdoc />
    protected override async Task ExecuteReadAsync(IAnsiConsole console, IAuditQueryService queries, IServiceProvider services, CancellationToken cancellationToken)
    {
        if (this.AuditId == Guid.Empty) throw new ArgumentException("An audit identity is required.");
        var result = await queries.FindAsync(this.AuditId, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure) { WriteErrors(console, result.Errors); return; }

        if (this.Json) { WriteJson(console, result.Value); return; }

        var details = result.Value;
        var record = details.Record;
        var table = new Table().Border(TableBorder.Minimal).AddColumn("Field").AddColumn("Value");
        void Row(string label, string value) => table.AddRow(Cell(label), Cell(value));
        Row("Audit ID", record.AuditId.ToString("D")); Row("Key", record.Key); Row("Label", record.DisplayName); Row("Reason", record.Reason);
        Row("Started UTC", record.StartedUtc.ToString("O")); Row("Completed UTC", record.CompletedUtc.ToString("O"));
        Row("Actor", record.Actor?.UserId); Row("Initiating actor", record.InitiatingActor?.UserId); Row("Module", record.ModuleName);
        Row("Correlation", record.CorrelationId); Row("Origin", record.OriginAuditId.ToString("D")); Row("Cause", record.CausationAuditId?.ToString("D"));
        Row("Outcome", record.Outcome.ToString()); Row("Completeness", record.Completeness.ToString()); Row("Storage", details.StorageState);
        Row("Integrity", details.IntegrityStatus); Row("Exposure transformed", details.IsTransformed.ToString());
        Row("Actions retained/observed", $"{record.Actions.Length}/{record.TotalActionCount}"); Row("Change groups", record.EntityChanges.Length.ToString());
        console.Write(table);
        var actions = new Table().Border(TableBorder.Minimal).AddColumn("Action ID").AddColumn("Parent").AddColumn("Key").AddColumn("Outcome");
        foreach (var action in record.Actions.Take(20)) actions.AddRow(Cell(action.Id.ToString("D")), Cell(action.ParentActionId?.ToString("D")), Cell(action.Key), Cell(action.Outcome.ToString()));
        console.Write(actions);
        console.WriteLine("Showing at most 20 actions. Use --json for all permitted actions, details, changes, errors, and capture notes.");
    }
}

/// <summary>Follows explicit origin and causation links under the anchor's trusted read scope.</summary>
/// <example><code>auditing related 235283d5-43ac-4ed1-a7c8-e7251aa27d0b --lifecycle All</code></example>
public sealed class AuditRelatedConsoleCommand() : AuditQueryConsoleCommandBase("related", "Find causally related audit executions")
{
    /// <summary>Gets or sets the authorized anchor audit identity.</summary>
    /// <example><code>command.AuditId = auditId;</code></example>
    [ConsoleCommandArgument(0, Description = "Anchor audit ID", Required = true)]
    public Guid AuditId { get; set; }

    /// <inheritdoc />
    protected override async Task ExecuteReadAsync(IAnsiConsole console, IAuditQueryService queries, IServiceProvider services, CancellationToken cancellationToken)
    {
        if (this.AuditId == Guid.Empty) throw new ArgumentException("An audit identity is required.");
        this.WritePage(console, await queries.FindRelatedAsync(this.AuditId, this.BuildFilter(), cancellationToken).ConfigureAwait(false));
    }
}

/// <summary>Investigates an exact typed resource while retaining repeated action occurrences.</summary>
/// <example><code>auditing history Customer C-42 --lifecycle All</code></example>
public sealed class AuditHistoryConsoleCommand() : AuditQueryConsoleCommandBase("history", "Find audits for one typed resource")
{
    /// <summary>Gets or sets the exact registered resource type or alias.</summary>
    /// <example><code>command.ResourceType = "Customer";</code></example>
    [ConsoleCommandArgument(0, Description = "Resource type or configured alias", Required = true)]
    public string ResourceType { get; set; }

    /// <summary>Gets or sets the opaque resource identifier.</summary>
    /// <example><code>command.ResourceId = "C-42";</code></example>
    [ConsoleCommandArgument(1, Description = "Exact resource ID", Required = true)]
    public string ResourceId { get; set; }

    /// <inheritdoc />
    protected override async Task ExecuteReadAsync(IAnsiConsole console, IAuditQueryService queries, IServiceProvider services, CancellationToken cancellationToken)
        => this.WritePage(console, await queries.GetResourceHistoryAsync(new(this.ResourceType, this.ResourceId), this.BuildFilter(), cancellationToken).ConfigureAwait(false));
}

/// <summary>Counts scoped audits by an allowlisted field without reading raw evidence.</summary>
/// <example><code>auditing stats --field Outcome --from 2026-10-31 --to 2026-11-01</code></example>
public sealed class AuditStatsConsoleCommand() : AuditQueryConsoleCommandBase("stats", "Count audits by an approved field")
{
    /// <summary>Gets or sets the catalog field used for grouping.</summary>
    /// <example><code>command.Field = "Outcome";</code></example>
    [ConsoleCommandOption("field", Description = "Approved grouping field", Default = "Outcome")]
    public string Field { get; set; } = "Outcome";

    /// <inheritdoc />
    protected override async Task ExecuteReadAsync(IAnsiConsole console, IAuditQueryService queries, IServiceProvider services, CancellationToken cancellationToken)
    {
        if (this.Take > 100 || this.Field?.Length is not (> 0 and <= 512)) throw new ArgumentException("Invalid audit grouping bounds.");
        var result = await queries.AggregateAsync(this.BuildFilter(), this.Field, this.Take, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure) { WriteErrors(console, result.Errors); return; }

        if (this.Json) { WriteJson(console, result.Value); return; }

        console.WriteLine($"Up to {this.Take} groups; counts include the complete scoped selection.");
        var table = new Table().Border(TableBorder.Minimal).AddColumn("Group").AddColumn("Audits");
        foreach (var group in result.Value) table.AddRow(Cell(group.Key), Cell(group.Count.ToString()));
        console.Write(table);
    }
}

/// <summary>Reads bounded capture and delivery observations without flushing or changing the store.</summary>
/// <example><code>auditing diagnostics --json</code></example>
public sealed class AuditDiagnosticsConsoleCommand() : AuditConsoleCommandBase("diagnostics", "Inspect permitted capture and delivery observations")
{
    /// <inheritdoc />
    protected override async Task ExecuteReadAsync(IAnsiConsole console, IAuditQueryService queries, IServiceProvider services, CancellationToken cancellationToken)
    {
        var snapshot = await services.GetRequiredService<AuditDiagnosticsService>().GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        // Structured diagnostics preserve unavailable/restricted distinctions and expose no business payloads.
        WriteJson(console, snapshot);
    }
}
