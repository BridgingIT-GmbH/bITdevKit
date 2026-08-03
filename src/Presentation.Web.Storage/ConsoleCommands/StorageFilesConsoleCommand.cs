// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

/// <summary>
/// Provides basic File Storage console operations.
/// </summary>
/// <example>
/// <code>
/// storage files providers
/// storage files list --provider documents --path exports --recursive
/// storage files read --provider documents --path exports/report.txt
/// storage files delete --provider documents --path exports/report.txt
/// storage files delete-all --provider documents --path exports --recursive --yes
/// </code>
/// </example>
public sealed class StorageFilesConsoleCommand() : StorageConsoleCommandBase("files", "File Storage operations")
{
    /// <summary>
    /// Gets or sets the file operation: providers, list, read, delete, or delete-all.
    /// </summary>
    /// <example>
    /// <code>
    /// storage files list
    /// </code>
    /// </example>
    [ConsoleCommandArgument(0, Description = "Operation: providers, list, read, delete, delete-all", Required = false)]
    public string Operation { get; set; }

    /// <summary>
    /// Gets or sets the configured file storage provider name.
    /// </summary>
    /// <example>
    /// <code>
    /// storage files list --provider documents
    /// </code>
    /// </example>
    [ConsoleCommandOption("provider", Alias = "p", Description = "File storage provider name")]
    public string Provider { get; set; }

    /// <summary>
    /// Gets or sets the provider-relative path.
    /// </summary>
    /// <example>
    /// <code>
    /// storage files read --path exports/report.txt
    /// </code>
    /// </example>
    [ConsoleCommandOption("path", Description = "Provider-relative path")]
    public string Path { get; set; }

    /// <summary>
    /// Gets or sets the file search pattern for listing.
    /// </summary>
    /// <example>
    /// <code>
    /// storage files list --pattern *.json
    /// </code>
    /// </example>
    [ConsoleCommandOption("pattern", Description = "File search pattern", Default = "*.*")]
    public string SearchPattern { get; set; } = "*.*";

    /// <summary>
    /// Gets or sets whether list operations include nested paths.
    /// </summary>
    /// <example>
    /// <code>
    /// storage files list --recursive
    /// </code>
    /// </example>
    [ConsoleCommandOption("recursive", Alias = "r", Description = "List recursively")]
    public bool Recursive { get; set; }

    /// <summary>
    /// Gets or sets the opaque continuation token for list paging.
    /// </summary>
    /// <example>
    /// <code>
    /// storage files list --continuation eyJ2IjoxfQ
    /// </code>
    /// </example>
    [ConsoleCommandOption("continuation", Description = "Opaque continuation token")]
    public string ContinuationToken { get; set; }

    /// <summary>
    /// Gets or sets the local output path used by read operations.
    /// </summary>
    /// <example>
    /// <code>
    /// storage files read --path report.pdf --output .\report.pdf
    /// </code>
    /// </example>
    [ConsoleCommandOption("output", Alias = "o", Description = "Local output path for read")]
    public string Output { get; set; }

    /// <summary>
    /// Gets or sets whether destructive delete-all operations are confirmed.
    /// </summary>
    /// <example>
    /// <code>
    /// storage files delete-all --provider documents --path exports --yes
    /// </code>
    /// </example>
    [ConsoleCommandOption("yes", Alias = "y", Description = "Confirm destructive delete-all")]
    public bool Yes { get; set; }

    /// <summary>
    /// Gets or sets whether delete-all may target the provider root.
    /// </summary>
    /// <example>
    /// <code>
    /// storage files delete-all --provider documents --root --recursive --yes
    /// </code>
    /// </example>
    [ConsoleCommandOption("root", Description = "Allow delete-all from provider root")]
    public bool Root { get; set; }

    /// <summary>
    /// Gets or sets whether delete-all only reports candidates.
    /// </summary>
    /// <example>
    /// <code>
    /// storage files delete-all --provider documents --path exports --dry-run
    /// </code>
    /// </example>
    [ConsoleCommandOption("dry-run", Description = "Preview delete-all candidates without deleting")]
    public bool DryRun { get; set; }

    /// <summary>
    /// Gets or sets whether delete-all continues after individual delete failures.
    /// </summary>
    /// <example>
    /// <code>
    /// storage files delete-all --provider documents --path exports --yes --continue-on-error
    /// </code>
    /// </example>
    [ConsoleCommandOption("continue-on-error", Description = "Continue delete-all after item failures")]
    public bool ContinueOnError { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of items processed by delete-all.
    /// </summary>
    /// <example>
    /// <code>
    /// storage files delete-all --provider documents --path exports --yes --max 100
    /// </code>
    /// </example>
    [ConsoleCommandOption("max", Alias = "m", Description = "Maximum delete-all candidates")]
    public int MaxItems { get; set; }

    /// <inheritdoc />
    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var factory = services.GetService<IFileStorageProviderFactory>();
        if (factory is null)
        {
            console.MarkupLine("[red]File Storage is not registered.[/]");
            return;
        }

        switch ((this.Operation ?? "providers").ToLowerInvariant())
        {
            case "providers":
                this.WriteProviders(console, factory);
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
                console.MarkupLine($"[yellow]Unknown File Storage operation '{Markup.Escape(this.Operation)}'.[/]");
                WriteUsage(console, "storage files [providers|list|read|delete|delete-all] --provider <name>");
                break;
        }
    }

    private void WriteProviders(IAnsiConsole console, IFileStorageProviderFactory factory)
    {
        var providerNames = factory.GetProviderNames();
        if (providerNames.Count == 0)
        {
            console.MarkupLine("[yellow]No file storage providers are registered.[/]");
            return;
        }

        var table = new Table().Border(TableBorder.Minimal);
        table.AddColumn("Provider");

        foreach (var name in providerNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            table.AddRow(Markup.Escape(name));
        }

        console.Write(table);
    }

    private async Task ListAsync(IAnsiConsole console, IFileStorageProviderFactory factory, CancellationToken cancellationToken)
    {
        var provider = this.ResolveProvider(console, factory);
        if (provider is null)
        {
            return;
        }

        var result = await provider.ListFilesAsync(
            this.Path ?? string.Empty,
            string.IsNullOrWhiteSpace(this.SearchPattern) ? "*.*" : this.SearchPattern,
            this.Recursive,
            this.ContinuationToken,
            cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            WriteErrors(console, result);
            return;
        }

        var table = new Table().Border(TableBorder.Minimal);
        table.AddColumn("Path");

        var files = result.Value.Files?.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray() ?? [];
        foreach (var file in files)
        {
            table.AddRow(Markup.Escape(file ?? string.Empty));
        }

        console.Write(table);
        console.MarkupLine($"[grey]{files.Length} file(s)[/]");
        WriteContinuation(console, result.Value.NextContinuationToken);
    }

    private async Task ReadAsync(IAnsiConsole console, IFileStorageProviderFactory factory, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(this.Path))
        {
            WriteUsage(console, "storage files read --provider <name> --path <path> [--output <path>]");
            return;
        }

        var provider = this.ResolveProvider(console, factory);
        if (provider is null)
        {
            return;
        }

        var result = await provider.ReadFileAsync(this.Path, null, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            WriteErrors(console, result);
            return;
        }

        await using var stream = result.Value;
        if (!string.IsNullOrWhiteSpace(this.Output))
        {
            var outputPath = System.IO.Path.GetFullPath(this.Output);
            await using var output = File.Create(outputPath);
            await stream.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            console.MarkupLine($"[green]Wrote file to[/] {Markup.Escape(outputPath)}");
            return;
        }

        var contentType = ContentTypeExtensions.FromFileName(this.Path, ContentType.TXT);
        if (contentType.IsBinary())
        {
            console.MarkupLine("[yellow]File content appears to be binary by extension. Use --output to write it to a local file.[/]");
            return;
        }

        using var reader = new StreamReader(stream, leaveOpen: true);
        var text = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        console.WriteLine(text);
    }

    private async Task DeleteAsync(IAnsiConsole console, IFileStorageProviderFactory factory, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(this.Path))
        {
            WriteUsage(console, "storage files delete --provider <name> --path <path>");
            return;
        }

        var provider = this.ResolveProvider(console, factory);
        if (provider is null)
        {
            return;
        }

        var result = await provider.DeleteFileAsync(this.Path, null, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            WriteErrors(console, result);
            return;
        }

        console.MarkupLine("[green]File deleted.[/]");
    }

    private async Task DeleteAllAsync(IAnsiConsole console, IFileStorageProviderFactory factory, CancellationToken cancellationToken)
    {
        if (!this.DryRun && !this.Yes)
        {
            console.MarkupLine("[red]Refusing to delete files without --yes. Use --dry-run to preview candidates.[/]");
            return;
        }

        if (string.IsNullOrWhiteSpace(this.Path) && !this.Root)
        {
            console.MarkupLine("[red]Refusing to delete from provider root without --root.[/]");
            return;
        }

        var provider = this.ResolveProvider(console, factory);
        if (provider is null)
        {
            return;
        }

        var path = this.Root ? string.Empty : this.Path ?? string.Empty;
        var continuation = this.ContinuationToken;
        var deleted = 0;
        var candidates = 0;
        var failures = 0;

        while (true)
        {
            var pageResult = await provider.ListFilesAsync(
                path,
                string.IsNullOrWhiteSpace(this.SearchPattern) ? "*.*" : this.SearchPattern,
                this.Recursive,
                continuation,
                cancellationToken).ConfigureAwait(false);
            if (pageResult.IsFailure)
            {
                WriteErrors(console, pageResult);
                return;
            }

            var files = pageResult.Value.Files?.ToArray() ?? [];
            foreach (var file in files)
            {
                candidates++;
                if (this.MaxItems > 0 && candidates > this.MaxItems)
                {
                    console.MarkupLine(this.DryRun
                        ? $"[yellow]File delete-all dry run found {candidates - 1} candidate(s).[/]"
                        : $"[green]Deleted {deleted} file(s).[/]");
                    return;
                }

                if (this.DryRun)
                {
                    continue;
                }

                var deleteResult = await provider.DeleteFileAsync(file, null, cancellationToken).ConfigureAwait(false);
                if (deleteResult.IsFailure)
                {
                    failures++;
                    WriteErrors(console, deleteResult);
                    if (!this.ContinueOnError)
                    {
                        console.MarkupLine($"[red]File delete-all stopped after {deleted} delete(s).[/]");
                        return;
                    }
                }
                else
                {
                    deleted++;
                }
            }

            continuation = pageResult.Value.NextContinuationToken;
            if (string.IsNullOrWhiteSpace(continuation))
            {
                break;
            }
        }

        if (this.DryRun)
        {
            console.MarkupLine($"[yellow]File delete-all dry run found {candidates} candidate(s).[/]");
            return;
        }

        console.MarkupLine(failures == 0
            ? $"[green]Deleted {deleted} file(s).[/]"
            : $"[yellow]Deleted {deleted} file(s) with {failures} failure(s).[/]");
    }

    private IFileStorageProvider ResolveProvider(IAnsiConsole console, IFileStorageProviderFactory factory)
    {
        var providerNames = factory.GetProviderNames();
        var providerName = this.Provider;

        if (string.IsNullOrWhiteSpace(providerName))
        {
            if (providerNames.Count == 1)
            {
                providerName = providerNames.First();
            }
            else
            {
                console.MarkupLine("[red]File storage provider is required when zero or multiple providers are registered.[/]");
                return null;
            }
        }

        try
        {
            return factory.CreateProvider(providerName);
        }
        catch (Exception ex)
        {
            console.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return null;
        }
    }
}
