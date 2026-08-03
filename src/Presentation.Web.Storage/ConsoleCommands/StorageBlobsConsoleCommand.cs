// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using System.Globalization;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

/// <summary>
/// Provides basic Blob Storage console operations.
/// </summary>
/// <example>
/// <code>
/// storage blobs list --client reports --container exports --prefix 2026/
/// storage blobs read --client reports --container exports --name 2026/report.txt
/// storage blobs delete --client reports --container exports --name 2026/report.txt
/// storage blobs delete-all --client reports --container exports --prefix tmp/ --yes
/// </code>
/// </example>
public sealed class StorageBlobsConsoleCommand() : StorageConsoleCommandBase("blobs", "Blob Storage operations")
{
    /// <summary>
    /// Gets or sets the blob operation: clients, list, read, delete, or delete-all.
    /// </summary>
    /// <example>
    /// <code>
    /// storage blobs list
    /// </code>
    /// </example>
    [ConsoleCommandArgument(0, Description = "Operation: clients, list, read, delete, delete-all", Required = false)]
    public string Operation { get; set; }

    /// <summary>
    /// Gets or sets the configured blob client name.
    /// </summary>
    /// <example>
    /// <code>
    /// storage blobs list --client reports
    /// </code>
    /// </example>
    [ConsoleCommandOption("client", Description = "Blob client name")]
    public string Client { get; set; }

    /// <summary>
    /// Gets or sets the blob container name.
    /// </summary>
    /// <example>
    /// <code>
    /// storage blobs list --container exports
    /// </code>
    /// </example>
    [ConsoleCommandOption("container", Alias = "c", Description = "Blob container")]
    public string Container { get; set; }

    /// <summary>
    /// Gets or sets the blob name for exact-key operations.
    /// </summary>
    /// <example>
    /// <code>
    /// storage blobs read --name 2026/report.txt
    /// </code>
    /// </example>
    [ConsoleCommandOption("name", Alias = "n", Description = "Blob name")]
    public string BlobName { get; set; }

    /// <summary>
    /// Gets or sets the optional listing prefix.
    /// </summary>
    /// <example>
    /// <code>
    /// storage blobs list --prefix 2026/
    /// </code>
    /// </example>
    [ConsoleCommandOption("prefix", Alias = "p", Description = "Blob name prefix")]
    public string Prefix { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of list items to return.
    /// </summary>
    /// <example>
    /// <code>
    /// storage blobs list --take 25
    /// </code>
    /// </example>
    [ConsoleCommandOption("take", Alias = "t", Description = "Max items to return", Default = 50)]
    public int Take { get; set; } = 50;

    /// <summary>
    /// Gets or sets the opaque continuation token for list paging.
    /// </summary>
    /// <example>
    /// <code>
    /// storage blobs list --continuation eyJ2IjoxfQ
    /// </code>
    /// </example>
    [ConsoleCommandOption("continuation", Description = "Opaque continuation token")]
    public string ContinuationToken { get; set; }

    /// <summary>
    /// Gets or sets whether a full container scan is explicitly allowed.
    /// </summary>
    /// <example>
    /// <code>
    /// storage blobs list --full-scan
    /// </code>
    /// </example>
    [ConsoleCommandOption("full-scan", Description = "Allow a full container scan")]
    public bool AllowFullScan { get; set; }

    /// <summary>
    /// Gets or sets the local output path used by read operations.
    /// </summary>
    /// <example>
    /// <code>
    /// storage blobs read --name report.pdf --output .\report.pdf
    /// </code>
    /// </example>
    [ConsoleCommandOption("output", Alias = "o", Description = "Local output path for read")]
    public string Output { get; set; }

    /// <summary>
    /// Gets or sets whether destructive delete-all operations are confirmed.
    /// </summary>
    /// <example>
    /// <code>
    /// storage blobs delete-all --container reports --full-scan --yes
    /// </code>
    /// </example>
    [ConsoleCommandOption("yes", Alias = "y", Description = "Confirm destructive delete-all")]
    public bool Yes { get; set; }

    /// <summary>
    /// Gets or sets whether delete-all only reports candidates.
    /// </summary>
    /// <example>
    /// <code>
    /// storage blobs delete-all --container reports --prefix tmp/ --dry-run
    /// </code>
    /// </example>
    [ConsoleCommandOption("dry-run", Description = "Preview delete-all candidates without deleting")]
    public bool DryRun { get; set; }

    /// <summary>
    /// Gets or sets whether delete-all continues after individual delete failures.
    /// </summary>
    /// <example>
    /// <code>
    /// storage blobs delete-all --container reports --prefix tmp/ --yes --continue-on-error
    /// </code>
    /// </example>
    [ConsoleCommandOption("continue-on-error", Description = "Continue delete-all after item failures")]
    public bool ContinueOnError { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of items processed by delete-all.
    /// </summary>
    /// <example>
    /// <code>
    /// storage blobs delete-all --container reports --prefix tmp/ --yes --max 100
    /// </code>
    /// </example>
    [ConsoleCommandOption("max", Alias = "m", Description = "Maximum delete-all candidates")]
    public int MaxItems { get; set; }

    /// <inheritdoc />
    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var factory = services.GetService<IBlobStoreClientFactory>();
        if (factory is null)
        {
            console.MarkupLine("[red]Blob Storage is not registered.[/]");
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
                console.MarkupLine($"[yellow]Unknown Blob Storage operation '{Markup.Escape(this.Operation)}'.[/]");
                WriteUsage(console, "storage blobs [clients|list|read|delete|delete-all] --client <name> --container <container>");
                break;
        }
    }

    private void WriteClients(IAnsiConsole console, IBlobStoreClientFactory factory)
    {
        var registrations = factory.GetRegistrations();
        if (registrations.Count == 0)
        {
            console.MarkupLine("[yellow]No blob clients are registered.[/]");
            return;
        }

        var table = new Table().Border(TableBorder.Minimal);
        table.AddColumn("Client");
        table.AddColumn("Provider");
        table.AddColumn("Capabilities");

        foreach (var registration in registrations.OrderBy(registration => registration.Name, StringComparer.OrdinalIgnoreCase))
        {
            table.AddRow(
                Markup.Escape(registration.Name),
                Markup.Escape(registration.ProviderName ?? string.Empty),
                Markup.Escape(registration.Capabilities?.ToString() ?? string.Empty));
        }

        console.Write(table);
    }

    private async Task ListAsync(IAnsiConsole console, IBlobStoreClientFactory factory, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(this.Container))
        {
            WriteUsage(console, "storage blobs list --client <name> --container <container> [--prefix <prefix>]");
            return;
        }

        var client = this.ResolveClient(console, factory);
        if (client is null)
        {
            return;
        }

        var query = new BlobQuery
        {
            Container = this.Container,
            Prefix = this.Prefix,
            Take = this.Take > 0 ? this.Take : null,
            ContinuationToken = this.ContinuationToken,
            AllowFullScan = this.AllowFullScan
        };

        var result = await client.ListPageAsync(query, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            WriteErrors(console, result);
            return;
        }

        var table = new Table().Border(TableBorder.Minimal);
        table.AddColumn("Container");
        table.AddColumn("Name");
        table.AddColumn("Size");
        table.AddColumn("Content Type");
        table.AddColumn("Modified");

        foreach (var item in result.Value.Items.OrderBy(item => item.Key?.Name, StringComparer.OrdinalIgnoreCase))
        {
            table.AddRow(
                Markup.Escape(item.Key?.Container ?? string.Empty),
                Markup.Escape(item.Key?.Name ?? string.Empty),
                item.Length.ToString(CultureInfo.InvariantCulture),
                Markup.Escape(item.ContentType?.MimeType() ?? string.Empty),
                Markup.Escape(item.LastModifiedAt?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty));
        }

        console.Write(table);
        console.MarkupLine($"[grey]{result.Value.Items.Count} blob(s)[/]");
        WriteContinuation(console, result.Value.ContinuationToken);
    }

    private async Task ReadAsync(IAnsiConsole console, IBlobStoreClientFactory factory, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(this.Container) || string.IsNullOrWhiteSpace(this.BlobName))
        {
            WriteUsage(console, "storage blobs read --client <name> --container <container> --name <blob> [--output <path>]");
            return;
        }

        var client = this.ResolveClient(console, factory);
        if (client is null)
        {
            return;
        }

        var result = await client.DownloadAsync(new BlobKey(this.Container, this.BlobName), cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            WriteErrors(console, result);
            return;
        }

        await using var download = result.Value;
        if (!string.IsNullOrWhiteSpace(this.Output))
        {
            var outputPath = Path.GetFullPath(this.Output);
            await using var output = File.Create(outputPath);
            await download.Content.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            console.MarkupLine($"[green]Wrote blob to[/] {Markup.Escape(outputPath)}");
            return;
        }

        if (download.Info.ContentType is { } contentType && contentType.IsBinary())
        {
            console.MarkupLine("[yellow]Blob content is binary. Use --output to write it to a local file.[/]");
            return;
        }

        using var reader = new StreamReader(download.Content, leaveOpen: true);
        var text = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        console.WriteLine(text);
    }

    private async Task DeleteAsync(IAnsiConsole console, IBlobStoreClientFactory factory, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(this.Container) || string.IsNullOrWhiteSpace(this.BlobName))
        {
            WriteUsage(console, "storage blobs delete --client <name> --container <container> --name <blob>");
            return;
        }

        var client = this.ResolveClient(console, factory);
        if (client is null)
        {
            return;
        }

        var result = await client.DeleteAsync(new BlobKey(this.Container, this.BlobName), cancellationToken: cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            WriteErrors(console, result);
            return;
        }

        console.MarkupLine("[green]Blob deleted.[/]");
    }

    private async Task DeleteAllAsync(IAnsiConsole console, IBlobStoreClientFactory factory, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(this.Container))
        {
            WriteUsage(console, "storage blobs delete-all --client <name> --container <container> [--prefix <prefix>] [--full-scan] --yes");
            return;
        }

        if (!this.DryRun && !this.Yes)
        {
            console.MarkupLine("[red]Refusing to delete blobs without --yes. Use --dry-run to preview candidates.[/]");
            return;
        }

        if (string.IsNullOrEmpty(this.Prefix) && !this.AllowFullScan)
        {
            console.MarkupLine("[red]Refusing to delete a full container without --full-scan.[/]");
            return;
        }

        var client = this.ResolveClient(console, factory);
        if (client is null)
        {
            return;
        }

        var result = await client.DeleteByPrefixAsync(
            this.Container,
            this.Prefix ?? string.Empty,
            new BlobDeletePrefixOptions
            {
                Take = this.Take > 0 ? this.Take : null,
                MaxItems = this.MaxItems > 0 ? this.MaxItems : null,
                AllowFullScan = this.AllowFullScan,
                DryRun = this.DryRun,
                ContinueOnError = this.ContinueOnError
            },
            cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            WriteErrors(console, result);
            return;
        }

        console.MarkupLine(this.DryRun
            ? $"[yellow]Blob delete-all dry run found {result.Value.CandidateCount} candidate(s).[/]"
            : $"[green]Deleted {result.Value.DeletedCount} blob(s).[/]");
    }

    private IBlobStoreClient ResolveClient(IAnsiConsole console, IBlobStoreClientFactory factory)
    {
        var registrations = factory.GetRegistrations();
        var clientName = this.Client;

        if (string.IsNullOrWhiteSpace(clientName))
        {
            if (registrations.Count == 1)
            {
                clientName = registrations.First().Name;
            }
            else
            {
                console.MarkupLine("[red]Blob client is required when zero or multiple clients are registered.[/]");
                return null;
            }
        }

        try
        {
            return factory.CreateClient(clientName);
        }
        catch (Exception ex)
        {
            console.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return null;
        }
    }
}
