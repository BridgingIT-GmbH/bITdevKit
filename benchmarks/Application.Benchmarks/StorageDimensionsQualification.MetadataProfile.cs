// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Benchmarks;

using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public static partial class StorageDimensionsQualification
{
    /// <summary>Profiles metadata operations on a private backup of a completed SQLite fixture, outside qualification.</summary>
    /// <param name="args">Source fixture, diagnostic model/workload, reader count, duration, fresh output directory, and optional idle checkpoint.</param>
    /// <returns>Zero when all diagnostic operations succeed; no performance gate is evaluated.</returns>
    /// <example><code>await StorageDimensionsQualification.RunMetadataProfileAsync(args);</code></example>
    public static async Task<int> RunMetadataProfileAsync(string[] args)
    {
        if (Argument(args, "--engine", "sqlite") != "sqlite") throw new ArgumentException("Metadata profiling currently supports SQLite only.");
        var budgeted = StorageDimensionsQualificationBudget.StartBudgetedChild(args);
        if (budgeted.HasValue) return budgeted.Value;
        var fixture = Path.GetFullPath(Argument(args, "--fixture", ""));
        var output = Path.GetFullPath(Argument(args, "--output", ""));
        var model = Argument(args, "--model", "dimensions");
        var workload = Argument(args, "--workload", "all");
        var readerCount = int.Parse(Argument(args, "--readers", "16"), System.Globalization.CultureInfo.InvariantCulture);
        var seconds = int.Parse(Argument(args, "--seconds", "30"), System.Globalization.CultureInfo.InvariantCulture);
        if (model is not ("parent" or "dimensions" or "snapshot" or "hidden" or "discard") || seconds is < 10 or > 120) throw new ArgumentException("Use parent/dimensions/snapshot/hidden/discard and 10..120 diagnostic seconds.");
        if (workload is not ("all" or "populated" or "dimensionless") || readerCount is < 0 or > 16)
            throw new ArgumentException("Use all/populated/dimensionless workload and 0..16 diagnostic readers.");
        if (Directory.Exists(output)) throw new ArgumentException("Diagnostic output must be a new owned directory.");
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "bITdevKit.slnx"))) root = root.Parent;
        if (root is null) throw new InvalidOperationException("Cannot locate the workspace.");
        var artifacts = Path.Combine(root.FullName, "artifacts", "storage-dimensions") + Path.DirectorySeparatorChar;
        if (!fixture.StartsWith(artifacts, StringComparison.OrdinalIgnoreCase) || !output.StartsWith(artifacts, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Diagnostic paths must stay under the workspace storage-dimensions artifacts directory.");
        using var completed = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(fixture, "qualification.json")));
        var preflight = completed.RootElement.GetProperty("Preflight");
        if (preflight.GetProperty("Engine").GetString() != "sqlite" || preflight.GetProperty("Feature").GetString() != "combined")
            throw new ArgumentException("Use a completed combined SQLite fixture.");
        DimensionQualificationData.Profile = preflight.TryGetProperty("Profile", out var profile) ? profile.GetString() : "reference";
        DimensionsParentOnlyContext.IncludeDimensionSnapshot = model is "snapshot" or "discard";
        DimensionsParentOnlyContext.DiscardDimensionSnapshot = model == "discard";
        var parentModel = model is "parent" or "snapshot" or "hidden" or "discard";
        Directory.CreateDirectory(output);
        var destinationFile = Path.Combine(output, "diagnostic.db");
        var sourceFile = Path.Combine(fixture, model == "parent" ? "owned-parent-baseline.db" : "owned-fixture.db");
        // A read-only backup preserves completed measurement data; diagnostics mutate only the new copy.
        using (var source = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = sourceFile, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString()))
        using (var destination = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = destinationFile, Pooling = false }.ToString()))
        {
            await source.OpenAsync();
            await destination.OpenAsync();
            source.BackupDatabase(destination);
        }

        var connection = new SqliteConnectionStringBuilder { DataSource = destinationFile, DefaultTimeout = 60, Pooling = true }.ToString();
        var commands = new QualificationCommands();
        await using var services = new ServiceCollection()
            .AddDbContext<DimensionsQualificationContext>(options => options.UseSqlite(connection).AddInterceptors(commands))
            .AddDbContext<DimensionsParentOnlyContext>(options => options.UseSqlite(connection).AddInterceptors(commands))
            .BuildServiceProvider();
        var scopes = services.GetRequiredService<IServiceScopeFactory>();
        var blobs = new BlobDimensionsQualification(scopes);
        var documents = new DocumentDimensionsQualification(scopes);
        var jobs = blobs.BaselineJobs(parentModel).Concat(documents.BaselineJobs(parentModel))
            .Where(job => workload == "all" || job.Name.Contains(workload == "dimensionless" ? "/dimensionless-" : "/properties-", StringComparison.Ordinal))
            .Where(job => readerCount != 0 || job.Writer).ToArray();
        var errors = new ConcurrentQueue<string>();
        using (var setup = new SqliteConnection(connection))
        {
            await setup.OpenAsync();
            using var command = setup.CreateCommand();
            command.CommandText = "PRAGMA journal_mode=WAL;";
            await command.ExecuteScalarAsync();
            if (args.Contains("--checkpoint"))
            {
                command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                await command.ExecuteScalarAsync();
            }
        }

        commands.Enabled = true;
        foreach (var job in jobs) await job.Execute(0);
        commands.Enabled = false;
        await WriteAsync(output, "commands.json", commands.Text.ToArray());
        await LoadAsync(jobs, "sqlite", TimeSpan.FromSeconds(15), errors, false, readerCount: readerCount);
        Console.WriteLine($"Metadata diagnostic ready; pid={Environment.ProcessId}, model={model}; no qualification claim.");
        await WriteAsync(output, "ready.json", new { ProcessId = Environment.ProcessId, Model = model, Diagnostic = true });
        var countsBefore = commands.Capture();
        var workBefore = await CaptureDatabaseWorkAsync(scopes, "sqlite", parentModel);
        var results = await LoadAsync(jobs, "sqlite", TimeSpan.FromSeconds(seconds), errors, true, readerCount: readerCount);
        await WriteAsync(output, "metadata-profile.json", new
        {
            DiagnosticOnly = true, QualificationComplete = false, Model = model, SourceFixture = fixture,
            Workload = workload, ReaderCount = readerCount,
            MaterializedSnapshotCharacters = Interlocked.Read(ref DimensionsParentOnlyContext.MaterializedSnapshotCharacters),
            DurationSeconds = seconds, Budget = StorageDimensionsQualificationBudget.CaptureEffectiveBudget("sqlite", requireMatch: true),
            CommandWork = commands.Capture().Subtract(countsBefore), DatabaseWorkBefore = workBefore,
            DatabaseWorkAfter = await CaptureDatabaseWorkAsync(scopes, "sqlite", parentModel),
            Cases = results, Errors = errors.ToArray()
        });
        return errors.IsEmpty ? 0 : 1;
    }
}
