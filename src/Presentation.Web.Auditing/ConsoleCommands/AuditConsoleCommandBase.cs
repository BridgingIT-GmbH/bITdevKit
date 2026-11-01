// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Auditing;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

/// <summary>Runs explicitly registered audit investigation commands under capture suppression.</summary>
/// <example><code>await new AuditListConsoleCommand().ExecuteAsync(console, services, cancellationToken);</code></example>
public abstract class AuditConsoleCommandBase(string name, string description) : ConsoleCommandBase(name, description), IGroupedConsoleCommand
{
    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true, Converters = { new JsonStringEnumConverter() }
    };

    /// <inheritdoc />
    /// <example><code>var group = command.GroupName;</code></example>
    public string GroupName => "auditing";

    /// <inheritdoc />
    /// <example><code>var aliases = command.GroupAliases;</code></example>
    public IReadOnlyCollection<string> GroupAliases => ["audit"];

    /// <summary>Gets or sets whether to print the approved response as JSON.</summary>
    /// <example><code>command.Json = true;</code></example>
    [ConsoleCommandOption("json", Alias = "j", Description = "Print the permitted response as JSON")]
    public bool Json { get; set; }

    /// <summary>Executes a read without exposing unexpected exception messages or creating audit evidence.</summary>
    /// <example><code>await command.ExecuteAsync(console, services, cancellationToken);</code></example>
    public sealed override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken = default)
    {
        try
        {
            using var suppression = services.GetService<IAuditContext>()?.Suppress();
            var queries = services.GetService<IAuditQueryService>();
            if (queries is null) { console.WriteLine("Auditing is not registered."); return; }

            await this.ExecuteReadAsync(console, queries, services, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (ArgumentException) { console.WriteLine("AuditConsoleArgumentsInvalid: check arguments and command help."); }
        catch (Exception) { console.WriteLine("AuditReadUnavailable"); }
    }

    /// <summary>Executes the command through application query or diagnostics services.</summary>
    protected abstract Task ExecuteReadAsync(IAnsiConsole console, IAuditQueryService queries, IServiceProvider services, CancellationToken cancellationToken);

    /// <summary>Prints only bounded audit error identifiers, never provider messages or raw records.</summary>
    protected static void WriteErrors(IAnsiConsole console, IEnumerable<IResultError> errors)
    {
        var code = errors.OfType<AuditError>().FirstOrDefault()?.Code;
        console.WriteLine(code is { Length: > 0 and <= 128 } && code.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-' or ':') ? code : "AuditReadUnavailable");
    }

    /// <summary>Prints structured permitted data without interpreting terminal markup.</summary>
    protected static void WriteJson<T>(IAnsiConsole console, T value) => console.Write(new Text(JsonSerializer.Serialize(value, jsonOptions) + Environment.NewLine));

    /// <summary>Prints a bounded page of summaries or resource-history occurrences.</summary>
    protected void WritePage<T>(IAnsiConsole console, ResultPaged<T> page)
    {
        if (page.IsFailure) { WriteErrors(console, page.Errors); return; }

        if (this.Json) { WriteJson(console, new { Items = page.Value, page.TotalCount, page.CurrentPage, page.PageSize }); return; }

        console.WriteLine($"Page {page.CurrentPage}; {page.TotalCount} matching audits. Summary integrity: NotChecked.");
        var table = new Table().Border(TableBorder.Minimal).AddColumn("Audit ID").AddColumn("Started UTC").AddColumn("Key").AddColumn("Outcome").AddColumn("Completeness").AddColumn("Storage");
        foreach (var item in page.Value)
        {
            var record = item is AuditResourceHistoryItem history ? history.Record : item as AuditRecordSummary;
            if (record is null) continue;
            table.AddRow(Cell(record.AuditId.ToString("D")), Cell(record.StartedUtc.ToString("O", CultureInfo.InvariantCulture)), Cell(record.Key), Cell(record.Outcome.ToString()), Cell(record.Completeness.ToString()), Cell(record.StorageState));
        }

        console.Write(table);
        foreach (var history in page.Value.OfType<AuditResourceHistoryItem>())
        {
            console.WriteLine($"{history.Record.AuditId:D}: root matched={history.RootResourceMatched}; matching actions={history.MatchingActionCount}; shown={history.MatchingActions.Length}.");
            foreach (var action in history.MatchingActions)
            {
                console.Write(Cell($"  {action.ActionId:D} {action.Key} {action.Outcome}"));
                console.WriteLine();
            }
        }
    }

    /// <summary>Escapes control characters and prevents data from being interpreted as markup.</summary>
    protected static Text Cell(string value) => new(value is null ? "-" : string.Concat(value.Select(character => char.IsControl(character) ? "\\u" + ((int)character).ToString("x4", CultureInfo.InvariantCulture) : character.ToString())));
}
