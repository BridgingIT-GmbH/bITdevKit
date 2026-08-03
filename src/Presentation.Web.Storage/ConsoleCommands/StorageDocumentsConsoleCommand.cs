// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using System.Globalization;
using BridgingIT.DevKit.Application.Storage;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

/// <summary>
/// Provides basic Document Storage console operations.
/// </summary>
/// <example>
/// <code>
/// storage documents clients
/// storage documents list --client customers --partition people --row-prefix DE-
/// storage documents read --client customers --partition people --row 42
/// storage documents delete --client customers --partition people --row 42
/// storage documents delete-all --client customers --partition people --row-prefix tmp- --yes
/// </code>
/// </example>
public sealed class StorageDocumentsConsoleCommand() : StorageConsoleCommandBase("documents", "Document Storage operations", "docs")
{
    /// <summary>
    /// Gets or sets the document operation: clients, list, read, delete, or delete-all.
    /// </summary>
    /// <example>
    /// <code>
    /// storage documents clients
    /// </code>
    /// </example>
    [ConsoleCommandArgument(0, Description = "Operation: clients, list, read, delete, delete-all", Required = false)]
    public string Operation { get; set; }

    /// <summary>
    /// Gets or sets the document client id.
    /// </summary>
    /// <example>
    /// <code>
    /// storage documents list --client myapp.person
    /// </code>
    /// </example>
    [ConsoleCommandOption("client", Alias = "c", Description = "Document client id")]
    public string ClientId { get; set; }

    /// <summary>
    /// Gets or sets the partition key.
    /// </summary>
    /// <example>
    /// <code>
    /// storage documents read --partition people
    /// </code>
    /// </example>
    [ConsoleCommandOption("partition", Alias = "p", Description = "Partition key")]
    public string PartitionKey { get; set; }

    /// <summary>
    /// Gets or sets the row key.
    /// </summary>
    /// <example>
    /// <code>
    /// storage documents read --row 42
    /// </code>
    /// </example>
    [ConsoleCommandOption("row", Alias = "r", Description = "Row key")]
    public string RowKey { get; set; }

    /// <summary>
    /// Gets or sets whether row key matching should use the supplied row key as a prefix.
    /// </summary>
    /// <example>
    /// <code>
    /// storage documents list --row-prefix DE-
    /// </code>
    /// </example>
    [ConsoleCommandOption("row-prefix", Description = "Row key prefix")]
    public string RowKeyPrefix { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of list items to return.
    /// </summary>
    /// <example>
    /// <code>
    /// storage documents list --take 25
    /// </code>
    /// </example>
    [ConsoleCommandOption("take", Alias = "t", Description = "Max items to return", Default = 50)]
    public int Take { get; set; } = 50;

    /// <summary>
    /// Gets or sets the opaque continuation token for list paging.
    /// </summary>
    /// <example>
    /// <code>
    /// storage documents list --continuation eyJ2IjoxfQ
    /// </code>
    /// </example>
    [ConsoleCommandOption("continuation", Description = "Opaque continuation token")]
    public string ContinuationToken { get; set; }

    /// <summary>
    /// Gets or sets whether a full document-type scan is explicitly allowed.
    /// </summary>
    /// <example>
    /// <code>
    /// storage documents list --full-scan
    /// </code>
    /// </example>
    [ConsoleCommandOption("full-scan", Description = "Allow a full document-type scan")]
    public bool AllowFullScan { get; set; }

    /// <summary>
    /// Gets or sets the local output path used by read operations.
    /// </summary>
    /// <example>
    /// <code>
    /// storage documents read --partition people --row 42 --output .\person.json
    /// </code>
    /// </example>
    [ConsoleCommandOption("output", Alias = "o", Description = "Local output path for read")]
    public string Output { get; set; }

    /// <summary>
    /// Gets or sets whether destructive delete-all operations are confirmed.
    /// </summary>
    /// <example>
    /// <code>
    /// storage documents delete-all --client myapp.person --full-scan --yes
    /// </code>
    /// </example>
    [ConsoleCommandOption("yes", Alias = "y", Description = "Confirm destructive delete-all")]
    public bool Yes { get; set; }

    /// <summary>
    /// Gets or sets whether delete-all only reports candidates.
    /// </summary>
    /// <example>
    /// <code>
    /// storage documents delete-all --client myapp.person --partition people --dry-run
    /// </code>
    /// </example>
    [ConsoleCommandOption("dry-run", Description = "Preview delete-all candidates without deleting")]
    public bool DryRun { get; set; }

    /// <summary>
    /// Gets or sets whether delete-all continues after individual delete failures.
    /// </summary>
    /// <example>
    /// <code>
    /// storage documents delete-all --client myapp.person --full-scan --yes --continue-on-error
    /// </code>
    /// </example>
    [ConsoleCommandOption("continue-on-error", Description = "Continue delete-all after item failures")]
    public bool ContinueOnError { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of items processed by delete-all.
    /// </summary>
    /// <example>
    /// <code>
    /// storage documents delete-all --client myapp.person --partition people --yes --max 100
    /// </code>
    /// </example>
    [ConsoleCommandOption("max", Alias = "m", Description = "Maximum delete-all candidates")]
    public int MaxItems { get; set; }

    /// <inheritdoc />
    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var factory = services.GetService<IDocumentStoreClientFactory>();
        if (factory is null)
        {
            console.MarkupLine("[red]Document Storage is not registered.[/]");
            return;
        }

        switch ((this.Operation ?? "clients").ToLowerInvariant())
        {
            case "clients":
                this.WriteClients(console, factory);
                break;
            case "list":
                await this.ListAsync(console, factory, cancellationToken).ConfigureAwait(false);
                break;
            case "read":
                await this.ReadAsync(console, factory, cancellationToken).ConfigureAwait(false);
                break;
            case "delete":
                await this.DeleteAsync(console, factory, cancellationToken).ConfigureAwait(false);
                break;
            case "delete-all":
            case "deleteall":
                await this.DeleteAllAsync(console, factory, cancellationToken).ConfigureAwait(false);
                break;
            default:
                console.MarkupLine($"[yellow]Unknown Document Storage operation '{Markup.Escape(this.Operation)}'.[/]");
                WriteUsage(console, "storage documents [clients|list|read|delete|delete-all] --client <id>");
                break;
        }
    }

    private void WriteClients(IAnsiConsole console, IDocumentStoreClientFactory factory)
    {
        var descriptors = factory.GetDescriptors();
        if (descriptors.Count == 0)
        {
            console.MarkupLine("[yellow]No document clients are registered.[/]");
            return;
        }

        var table = new Table().Border(TableBorder.Minimal);
        table.AddColumn("Client");
        table.AddColumn("Name");
        table.AddColumn("Type");
        table.AddColumn("Provider");

        foreach (var descriptor in descriptors.OrderBy(descriptor => descriptor.ClientId, StringComparer.OrdinalIgnoreCase))
        {
            table.AddRow(
                Markup.Escape(descriptor.ClientId),
                Markup.Escape(descriptor.Name ?? string.Empty),
                Markup.Escape(descriptor.DocumentTypeName ?? descriptor.DocumentType?.Name ?? string.Empty),
                Markup.Escape(descriptor.ProviderName ?? string.Empty));
        }

        console.Write(table);
    }

    private async Task ListAsync(IAnsiConsole console, IDocumentStoreClientFactory factory, CancellationToken cancellationToken)
    {
        var accessor = this.ResolveAccessor(console, factory);
        if (accessor is null)
        {
            return;
        }

        var query = this.CreateQuery();
        if (query is null)
        {
            WriteUsage(console, "storage documents list --client <id> [--partition <key> --row-prefix <prefix>] [--full-scan]");
            return;
        }

        var result = await accessor.ListPageAsync(query, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            WriteErrors(console, result);
            return;
        }

        var table = new Table().Border(TableBorder.Minimal);
        table.AddColumn("Partition");
        table.AddColumn("Row");

        foreach (var key in result.Value.Items.OrderBy(key => key.PartitionKey, StringComparer.OrdinalIgnoreCase).ThenBy(key => key.RowKey, StringComparer.OrdinalIgnoreCase))
        {
            table.AddRow(Markup.Escape(key.PartitionKey ?? string.Empty), Markup.Escape(key.RowKey ?? string.Empty));
        }

        console.Write(table);
        console.MarkupLine($"[grey]{result.Value.Items.Count} document key(s)[/]");
        WriteContinuation(console, result.Value.ContinuationToken);
    }

    private async Task ReadAsync(IAnsiConsole console, IDocumentStoreClientFactory factory, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(this.PartitionKey) || string.IsNullOrWhiteSpace(this.RowKey))
        {
            WriteUsage(console, "storage documents read --client <id> --partition <key> --row <key> [--output <path>]");
            return;
        }

        var accessor = this.ResolveAccessor(console, factory);
        if (accessor is null)
        {
            return;
        }

        var result = await accessor.GetEntryJsonAsync(new DocumentKey(this.PartitionKey, this.RowKey), cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            WriteErrors(console, result);
            return;
        }

        if (!string.IsNullOrWhiteSpace(this.Output))
        {
            var outputPath = Path.GetFullPath(this.Output);
            await File.WriteAllTextAsync(outputPath, result.Value.Content ?? string.Empty, cancellationToken).ConfigureAwait(false);
            console.MarkupLine($"[green]Wrote document to[/] {Markup.Escape(outputPath)}");
            return;
        }

        console.WriteLine(result.Value.Content ?? string.Empty);
        console.MarkupLine($"[grey]ETag:[/] {Markup.Escape(result.Value.Info.ETag ?? string.Empty)}");
        console.MarkupLine($"[grey]Size:[/] {result.Value.Size.ToString(CultureInfo.InvariantCulture)} byte(s)");
    }

    private async Task DeleteAsync(IAnsiConsole console, IDocumentStoreClientFactory factory, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(this.PartitionKey) || string.IsNullOrWhiteSpace(this.RowKey))
        {
            WriteUsage(console, "storage documents delete --client <id> --partition <key> --row <key>");
            return;
        }

        var accessor = this.ResolveAccessor(console, factory);
        if (accessor is null)
        {
            return;
        }

        var result = await accessor.DeleteAsync(new DocumentKey(this.PartitionKey, this.RowKey), cancellationToken: cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            WriteErrors(console, result);
            return;
        }

        console.MarkupLine("[green]Document deleted.[/]");
    }

    private async Task DeleteAllAsync(IAnsiConsole console, IDocumentStoreClientFactory factory, CancellationToken cancellationToken)
    {
        if (!this.DryRun && !this.Yes)
        {
            console.MarkupLine("[red]Refusing to delete documents without --yes. Use --dry-run to preview candidates.[/]");
            return;
        }

        if (string.IsNullOrWhiteSpace(this.PartitionKey) && !this.AllowFullScan)
        {
            console.MarkupLine("[red]Refusing to delete a full document type without --full-scan.[/]");
            return;
        }

        var accessor = this.ResolveAccessor(console, factory);
        if (accessor is null)
        {
            return;
        }

        var query = this.CreateDeleteAllQuery();
        var deleted = 0;
        var candidates = 0;
        var failures = 0;

        while (true)
        {
            var pageResult = await accessor.ListPageAsync(query, cancellationToken).ConfigureAwait(false);
            if (pageResult.IsFailure)
            {
                WriteErrors(console, pageResult);
                return;
            }

            foreach (var key in pageResult.Value.Items ?? [])
            {
                candidates++;
                if (this.MaxItems > 0 && candidates > this.MaxItems)
                {
                    console.MarkupLine(this.DryRun
                        ? $"[yellow]Document delete-all dry run found {candidates - 1} candidate(s).[/]"
                        : $"[green]Deleted {deleted} document(s).[/]");
                    return;
                }

                if (this.DryRun)
                {
                    continue;
                }

                var deleteResult = await accessor.DeleteAsync(key, cancellationToken: cancellationToken).ConfigureAwait(false);
                if (deleteResult.IsFailure)
                {
                    failures++;
                    WriteErrors(console, deleteResult);
                    if (!this.ContinueOnError)
                    {
                        console.MarkupLine($"[red]Document delete-all stopped after {deleted} delete(s).[/]");
                        return;
                    }
                }
                else
                {
                    deleted++;
                }
            }

            if (!pageResult.Value.HasMore)
            {
                break;
            }

            query = ContinueQuery(query, pageResult.Value.ContinuationToken);
        }

        if (this.DryRun)
        {
            console.MarkupLine($"[yellow]Document delete-all dry run found {candidates} candidate(s).[/]");
            return;
        }

        console.MarkupLine(failures == 0
            ? $"[green]Deleted {deleted} document(s).[/]"
            : $"[yellow]Deleted {deleted} document(s) with {failures} failure(s).[/]");
    }

    private DocumentQuery CreateQuery()
    {
        if (!string.IsNullOrWhiteSpace(this.PartitionKey))
        {
            var rowKey = this.RowKeyPrefix ?? this.RowKey ?? string.Empty;
            return new DocumentQuery
            {
                DocumentKey = new DocumentKey(this.PartitionKey, rowKey),
                Filter = this.RowKeyPrefix is not null ? DocumentKeyFilter.RowKeyPrefixMatch : DocumentKeyFilter.FullMatch,
                Take = this.Take > 0 ? this.Take : null,
                ContinuationToken = this.ContinuationToken,
                AllowFullScan = this.AllowFullScan
            };
        }

        if (this.AllowFullScan)
        {
            return new DocumentQuery
            {
                Take = this.Take > 0 ? this.Take : null,
                ContinuationToken = this.ContinuationToken,
                AllowFullScan = true
            };
        }

        return null;
    }

    private DocumentQuery CreateDeleteAllQuery()
    {
        if (!string.IsNullOrWhiteSpace(this.PartitionKey))
        {
            var rowPrefix = this.RowKeyPrefix ?? this.RowKey ?? string.Empty;
            return new DocumentQuery
            {
                DocumentKey = new DocumentKey(this.PartitionKey, rowPrefix),
                Filter = DocumentKeyFilter.RowKeyPrefixMatch,
                Take = this.Take > 0 ? this.Take : null,
                ContinuationToken = this.ContinuationToken,
                AllowFullScan = this.AllowFullScan
            };
        }

        return new DocumentQuery
        {
            Take = this.Take > 0 ? this.Take : null,
            ContinuationToken = this.ContinuationToken,
            AllowFullScan = true
        };
    }

    private static DocumentQuery ContinueQuery(DocumentQuery query, string continuationToken) => new()
    {
        DocumentKey = query.DocumentKey,
        Filter = query.Filter,
        Take = query.Take,
        ContinuationToken = continuationToken,
        AllowFullScan = query.AllowFullScan
    };

    private IDocumentStoreClientAccessor ResolveAccessor(IAnsiConsole console, IDocumentStoreClientFactory factory)
    {
        var descriptors = factory.GetDescriptors();
        var clientId = this.ClientId;

        if (string.IsNullOrWhiteSpace(clientId))
        {
            if (descriptors.Count == 1)
            {
                clientId = descriptors[0].ClientId;
            }
            else
            {
                console.MarkupLine("[red]Document client is required when zero or multiple clients are registered.[/]");
                return null;
            }
        }

        var accessor = factory.Create(clientId);
        if (accessor is null)
        {
            console.MarkupLine($"[red]Document client '{Markup.Escape(clientId)}' is not registered.[/]");
        }

        return accessor;
    }
}
