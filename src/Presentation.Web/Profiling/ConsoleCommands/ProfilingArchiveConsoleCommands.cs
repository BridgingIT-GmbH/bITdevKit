// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Common;
using Spectre.Console;

/// <summary>Exports a portable archive or a one-way Perfetto visualization trace.</summary>
/// <example><code>profiling export --session abc12345 --format perfetto --output run.perfetto.json</code></example>
public sealed class ProfilingExportConsoleCommand()
    : ProfilingConsoleCommandBase("export", "Export profiling JSON as an archive or Perfetto trace")
{
    /// <summary>Gets or sets the required source session key.</summary>
    /// <example><code>command.SessionKey = "abc12345";</code></example>
    [ConsoleCommandOption("session", Alias = "s", Description = "Source session key", Required = true)]
    public string SessionKey { get; set; }

    /// <summary>Gets or sets the optional source node key for snapshot export.</summary>
    /// <example><code>command.NodeKey = "def67890";</code></example>
    [ConsoleCommandOption("node", Alias = "n", Description = "Source node key for snapshot export")]
    public string NodeKey { get; set; }

    /// <summary>Gets or sets the optional source snapshot key.</summary>
    /// <example><code>command.SnapshotKey = "ghi12345";</code></example>
    [ConsoleCommandOption("snapshot", Description = "Source snapshot key")]
    public string SnapshotKey { get; set; }

    /// <summary>Gets or sets the export format: archive or perfetto.</summary>
    /// <example><code>command.Format = "perfetto";</code></example>
    [ConsoleCommandOption(
        "format",
        Description = "Export format: archive or perfetto (default: archive)"
    )]
    public string Format { get; set; } = "archive";

    /// <summary>Gets or sets the required destination file path.</summary>
    /// <example><code>command.OutputPath = "profile.json";</code></example>
    [ConsoleCommandOption("output", Alias = "o", Description = "Destination JSON file", Required = true)]
    public string OutputPath { get; set; }

    /// <summary>Gets or sets whether an existing destination may be replaced.</summary>
    /// <example><code>command.Overwrite = true;</code></example>
    [ConsoleCommandOption("overwrite", Description = "Replace an existing destination file")]
    public bool Overwrite { get; set; }

    /// <inheritdoc />
    public override void OnAfterBind(IAnsiConsole console, string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(this.NodeKey) != string.IsNullOrWhiteSpace(this.SnapshotKey))
        {
            throw new ArgumentException("--node and --snapshot must be supplied together.");
        }

        this.Format = string.IsNullOrWhiteSpace(this.Format)
            ? "archive"
            : this.Format.Trim().ToLowerInvariant();
        if (this.Format is not ("archive" or "perfetto"))
        {
            throw new ArgumentException("--format must be archive or perfetto.");
        }

        if (this.Format == "perfetto" && !string.IsNullOrWhiteSpace(this.SnapshotKey))
        {
            throw new ArgumentException(
                "Perfetto export supports complete sessions and cannot be combined with --node or --snapshot."
            );
        }
    }

    /// <inheritdoc />
    public override async Task ExecuteAsync(
        IAnsiConsole console,
        IServiceProvider services,
        CancellationToken cancellationToken = default
    )
    {
        var perfetto = this.Format == "perfetto"
            ? GetRequired<IProfilingPerfettoExportService>(console, services)
            : null;
        var archives = this.Format == "archive"
            ? GetRequired<IProfilingArchiveService>(console, services)
            : null;
        if (perfetto is null && archives is null)
        {
            return;
        }

        var outputPath = Path.GetFullPath(this.OutputPath);
        var directory = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            console.MarkupLine("[red]The destination directory does not exist.[/]");
            return;
        }

        if (File.Exists(outputPath) && !this.Overwrite)
        {
            console.MarkupLine("[yellow]The destination exists. Use --overwrite to replace it.[/]");
            return;
        }

        var temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(outputPath)}.{Guid.NewGuid():N}.tmp"
        );
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan
            ))
            {
                var result = this.Format == "perfetto"
                    ? await perfetto
                        .ExportSessionAsync(this.SessionKey, stream, cancellationToken)
                        .ConfigureAwait(false)
                    : string.IsNullOrWhiteSpace(this.SnapshotKey)
                        ? await archives
                            .ExportSessionAsync(this.SessionKey, stream, cancellationToken)
                            .ConfigureAwait(false)
                        : await archives
                            .ExportSnapshotAsync(
                                this.SessionKey,
                                this.NodeKey,
                                this.SnapshotKey,
                                stream,
                                cancellationToken
                            )
                            .ConfigureAwait(false);
                if (result.IsFailure)
                {
                    WriteErrors(console, result);
                    return;
                }
            }

            File.Move(temporaryPath, outputPath, this.Overwrite);
            console.MarkupLine(
                $"[green]Profiling {(this.Format == "perfetto" ? "Perfetto trace" : "archive")} exported:[/] {Markup.Escape(outputPath)}"
            );
        }
        catch (IOException)
        {
            console.MarkupLine("[red]The Profiling export file could not be written.[/]");
        }
        catch (UnauthorizedAccessException)
        {
            console.MarkupLine("[red]The Profiling export file is not writable.[/]");
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}

/// <summary>Imports one portable Profiling JSON archive as a fresh terminal session.</summary>
/// <example><code>profiling import --file run.json</code></example>
public sealed class ProfilingImportConsoleCommand()
    : ProfilingConsoleCommandBase("import", "Import a portable profiling JSON archive")
{
    /// <summary>Gets or sets the required source file path.</summary>
    /// <example><code>command.FilePath = "profile.json";</code></example>
    [ConsoleCommandOption("file", Alias = "f", Description = "Source JSON archive", Required = true)]
    public string FilePath { get; set; }

    /// <inheritdoc />
    public override async Task ExecuteAsync(
        IAnsiConsole console,
        IServiceProvider services,
        CancellationToken cancellationToken = default
    )
    {
        var archives = GetRequired<IProfilingArchiveService>(console, services);
        if (archives is null)
        {
            return;
        }

        var filePath = Path.GetFullPath(this.FilePath);
        if (!File.Exists(filePath))
        {
            console.MarkupLine("[red]The Profiling archive file was not found.[/]");
            return;
        }

        try
        {
            await using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan
            );
            var result = await archives.ImportAsync(stream, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure)
            {
                WriteErrors(console, result);
                return;
            }

            console.MarkupLine(
                $"[green]Profiling archive imported as session[/] [bold]{Markup.Escape(result.Value.SessionKey)}[/]"
            );
        }
        catch (IOException)
        {
            console.MarkupLine("[red]The Profiling archive file could not be read.[/]");
        }
        catch (UnauthorizedAccessException)
        {
            console.MarkupLine("[red]The Profiling archive file is not readable.[/]");
        }
    }
}