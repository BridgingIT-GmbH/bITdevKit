// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System.Text;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Configuration;
using Npgsql;

/// <summary>
/// Gives each linked worktree its own PostgreSQL database <c>&lt;database&gt;_wNN</c> and drops recorded instance
/// databases on reset.
/// </summary>
/// <remarks>
/// Only <see cref="NpgsqlConnectionStringBuilder.Database"/> changes. Credentials and all other options are kept and never
/// logged. PostgreSQL limits identifiers to 63 bytes, and database names are compared case-sensitively.
/// </remarks>
/// <example>
/// <code>
/// builder.AddDevAppInstance(o => o
///     .WithAppId("forecasthub")
///     .WithPostgresDatabases());
/// </code>
/// </example>
public sealed class PostgresDevAppInstanceAdapter : DevAppInstanceDatabaseAdapterBase
{
    /// <summary>The adapter kind written to inventories.</summary>
    public const string KindName = "postgres";

    /// <summary>Gets the shared instance.</summary>
    public static PostgresDevAppInstanceAdapter Instance { get; } = new();

    /// <inheritdoc />
    public override string Kind => KindName;

    /// <inheritdoc />
    protected override int MaxIdentifierLength => 63;

    /// <inheritdoc />
    protected override StringComparison NameComparison => StringComparison.Ordinal;

    /// <inheritdoc />
    public override async Task<DevAppInstanceResetOutcome> ResetAsync(
        IReadOnlyList<DevAppInstanceInventoryResource> targets,
        DevAppInstanceContext context,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var completed = new List<string>();
        var remaining = new List<string>();
        var errors = new List<string>();
        foreach (var target in targets ?? [])
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (target.Shared || string.IsNullOrWhiteSpace(target.Target))
            {
                continue;
            }

            var connection = target.SourceKeys?.Select(k => Parse(configuration[k])).FirstOrDefault(b => b is not null);
            if (connection is null)
            {
                remaining.Add(target.Target);
                errors.Add($"No connection string is configured for database '{target.Target}'.");
                continue;
            }

            connection.Database = "postgres";
            connection.Pooling = false;
            try
            {
                await using var sql = new NpgsqlConnection(connection.ConnectionString);
                await sql.OpenAsync(cancellationToken);
                await using var command = sql.CreateCommand();
                command.CommandTimeout = 30;
                command.CommandText = $"DROP DATABASE IF EXISTS {QuoteIdentifier(target.Target)} WITH (FORCE)";
                await command.ExecuteNonQueryAsync(cancellationToken);
                NpgsqlConnection.ClearAllPools();
                completed.Add(target.Target);
            }
            catch (NpgsqlException ex)
            {
                remaining.Add(target.Target);
                errors.Add($"Database '{target.Target}' cannot be dropped: {ex.Message}");
            }
        }

        return new DevAppInstanceResetOutcome(completed, remaining, errors);
    }

    /// <summary>
    /// Quotes a PostgreSQL identifier.
    /// </summary>
    /// <param name="name">The identifier.</param>
    /// <returns>The quoted identifier.</returns>
    internal static string QuoteIdentifier(string name) => $"\"{name.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    /// <inheritdoc />
    protected override bool TryParse(string value, out string server, out string database)
    {
        var builder = Parse(value);
        server = builder is null ? null : $"{builder.Host}:{builder.Port}";
        database = builder?.Database;

        return builder is not null;
    }

    /// <inheritdoc />
    protected override string Rewrite(string value, string database)
    {
        var builder = Parse(value) ?? throw new ArgumentException("The value is not a PostgreSQL connection string.", nameof(value));
        builder.Database = database;

        return builder.ConnectionString;
    }

    /// <inheritdoc />
    protected override int GetIdentifierLength(string name) => Encoding.UTF8.GetByteCount(name);

    private static NpgsqlConnectionStringBuilder Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.Contains('='))
        {
            return null;
        }

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(value);

            return string.IsNullOrWhiteSpace(builder.Host) ? null : builder;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }
}
