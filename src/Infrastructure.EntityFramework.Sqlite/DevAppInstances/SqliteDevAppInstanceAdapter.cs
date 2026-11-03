// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using BridgingIT.DevKit.Common;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Gives each linked worktree its own SQLite database file <c>&lt;name&gt;_wNN&lt;extension&gt;</c> and deletes recorded
/// instance files on reset.
/// </summary>
/// <remarks>
/// Only <see cref="SqliteConnectionStringBuilder.DataSource"/> changes. The instance file stays in the directory of the
/// baseline file, and a relative path stays relative. In-memory databases (<c>:memory:</c> or <c>Mode=Memory</c>) are
/// not claimed. Inventories record the full path, resolved against the current directory.
/// </remarks>
/// <example>
/// <code>
/// builder.AddDevAppInstance(o => o
///     .WithAppId("forecasthub")
///     .WithSqliteDatabases());
/// </code>
/// </example>
public sealed class SqliteDevAppInstanceAdapter : DevAppInstanceDatabaseAdapterBase
{
    /// <summary>The adapter kind written to inventories.</summary>
    public const string KindName = "sqlite";

    private static readonly string[] SidecarSuffixes = [string.Empty, "-wal", "-shm", "-journal"];

    /// <summary>Gets the shared instance.</summary>
    public static SqliteDevAppInstanceAdapter Instance { get; } = new();

    /// <inheritdoc />
    public override string Kind => KindName;

    /// <inheritdoc />
    protected override int MaxIdentifierLength => int.MaxValue;

    /// <inheritdoc />
    protected override StringComparison NameComparison =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <inheritdoc />
    public override Task<DevAppInstanceResetOutcome> ResetAsync(
        IReadOnlyList<DevAppInstanceInventoryResource> targets,
        DevAppInstanceContext context,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var completed = new List<string>();
        var remaining = new List<string>();
        var errors = new List<string>();
        var poolsCleared = false;
        foreach (var target in targets ?? [])
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (target.Shared || string.IsNullOrWhiteSpace(target.Target))
            {
                continue;
            }

            if (!poolsCleared)
            {
                SqliteConnection.ClearAllPools();
                poolsCleared = true;
            }

            try
            {
                foreach (var suffix in SidecarSuffixes)
                {
                    var path = target.Target + suffix;
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }

                completed.Add(target.Target);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                remaining.Add(target.Target);
                errors.Add($"Database file '{target.Target}' cannot be deleted: {ex.Message}");
            }
        }

        return Task.FromResult(new DevAppInstanceResetOutcome(completed, remaining, errors));
    }

    /// <inheritdoc />
    public override string ValidateOrphanTarget(DevAppInstanceInventoryResource target, DevAppInstanceContext orphanContext, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(orphanContext);
        ArgumentNullException.ThrowIfNull(configuration);

        if (target.Shared)
        {
            return "Shared resources are never dropped.";
        }

        if (!this.TryGetHostDatabase(target, configuration, out _, out var database))
        {
            return $"No connection string is configured for database '{target.Target}'.";
        }

        var expectedName = Path.GetFileName(this.GetInstanceDatabaseName(database, orphanContext));
        if (string.IsNullOrWhiteSpace(target.Target) || !string.Equals(Path.GetFileName(target.Target), expectedName, this.NameComparison))
        {
            return $"Database '{target.Target}' does not match the generated name '{expectedName}' of the orphaned slot.";
        }

        var hostFile = this.GetTarget(database);
        if (string.Equals(hostFile, target.Target, this.NameComparison))
        {
            return $"Database '{target.Target}' is the host's own database.";
        }

        var directory = Path.GetDirectoryName(target.Target);
        var underHostDirectory = string.Equals(directory, Path.GetDirectoryName(hostFile), this.NameComparison);
        var underOrphanWorktree = !string.IsNullOrWhiteSpace(orphanContext.WorktreeRoot) &&
            IsUnder(directory, Path.GetFullPath(orphanContext.WorktreeRoot), this.NameComparison);
        if (!underHostDirectory && !underOrphanWorktree)
        {
            return $"Database '{target.Target}' is outside the configured directory and the orphaned worktree.";
        }

        return null;
    }

    /// <inheritdoc />
    protected override bool TryParse(string value, out string server, out string database)
    {
        server = null;
        var builder = Parse(value);
        database = builder?.DataSource;

        return builder is not null;
    }

    /// <inheritdoc />
    protected override string Rewrite(string value, string database)
    {
        var builder = Parse(value) ?? throw new ArgumentException("The value is not a SQLite connection string.", nameof(value));
        builder.DataSource = database;

        return builder.ConnectionString;
    }

    /// <inheritdoc />
    protected override string GetInstanceDatabaseName(string database, DevAppInstanceContext context)
    {
        var fileName = Path.GetFileName(database);
        var directory = database[..^fileName.Length];
        var extension = Path.GetExtension(fileName);
        var stem = fileName[..^extension.Length];

        return $"{directory}{context.GetInstanceDatabaseName(stem)}{extension}";
    }

    /// <inheritdoc />
    protected override string GetTarget(string database) => Path.GetFullPath(database);

    /// <inheritdoc />
    protected override string GetResourceId(string database) => Path.GetFileNameWithoutExtension(database);

    private static bool IsUnder(string directory, string root, StringComparison comparison)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        var normalizedDirectory = Path.TrimEndingDirectorySeparator(directory ?? string.Empty) + Path.DirectorySeparatorChar;

        return normalizedDirectory.StartsWith(normalizedRoot, comparison);
    }

    private static SqliteConnectionStringBuilder Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.Contains('='))
        {
            return null;
        }

        try
        {
            var builder = new SqliteConnectionStringBuilder(value);
            if (string.IsNullOrWhiteSpace(builder.DataSource) ||
                builder.Mode == SqliteOpenMode.Memory ||
                string.Equals(builder.DataSource, ":memory:", StringComparison.OrdinalIgnoreCase) ||
                builder.DataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return builder;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }
}
