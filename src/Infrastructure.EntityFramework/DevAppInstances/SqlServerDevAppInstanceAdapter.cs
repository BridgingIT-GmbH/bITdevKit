// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using BridgingIT.DevKit.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Gives each linked worktree its own SQL Server database <c>&lt;database&gt;_wNN</c> and drops recorded instance
/// databases on reset.
/// </summary>
/// <remarks>
/// Only <see cref="SqlConnectionStringBuilder.InitialCatalog"/> changes. Credentials and all other options are kept and never logged.
/// </remarks>
/// <example>
/// <code>
/// builder.AddDevAppInstance(o => o
///     .WithAppId("forecasthub")
///     .WithSqlServerDatabases());
/// </code>
/// </example>
public sealed class SqlServerDevAppInstanceAdapter : DevAppInstanceDatabaseAdapterBase
{
    /// <summary>The adapter kind written to inventories.</summary>
    public const string KindName = "sqlserver";

    /// <summary>Gets the shared instance.</summary>
    public static SqlServerDevAppInstanceAdapter Instance { get; } = new();

    /// <inheritdoc />
    public override string Kind => KindName;

    /// <inheritdoc />
    protected override int MaxIdentifierLength => 128;

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

            connection.InitialCatalog = "master";
            connection.Pooling = false;
            try
            {
                await using var sql = new SqlConnection(connection.ConnectionString);
                await sql.OpenAsync(cancellationToken);
                await using var command = sql.CreateCommand();
                command.CommandTimeout = 30;
                command.CommandText = """
                    IF DB_ID(@name) IS NOT NULL
                    BEGIN
                        DECLARE @quoted nvarchar(300) = QUOTENAME(@name);
                        EXEC(N'ALTER DATABASE ' + @quoted + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE ' + @quoted + N';');
                    END
                    """;
                command.Parameters.Add(new SqlParameter("@name", System.Data.SqlDbType.NVarChar, this.MaxIdentifierLength) { Value = target.Target });
                await command.ExecuteNonQueryAsync(cancellationToken);
                SqlConnection.ClearAllPools();
                completed.Add(target.Target);
            }
            catch (SqlException ex)
            {
                remaining.Add(target.Target);
                errors.Add($"Database '{target.Target}' cannot be dropped: {ex.Message}");
            }
        }

        return new DevAppInstanceResetOutcome(completed, remaining, errors);
    }

    /// <inheritdoc />
    protected override bool TryParse(string value, out string server, out string database)
    {
        var builder = Parse(value);
        server = builder?.DataSource;
        database = builder?.InitialCatalog;

        return builder is not null;
    }

    /// <inheritdoc />
    protected override string Rewrite(string value, string database)
    {
        var builder = Parse(value) ?? throw new ArgumentException("The value is not a SQL Server connection string.", nameof(value));
        builder.InitialCatalog = database;

        return builder.ConnectionString;
    }

    private static SqlConnectionStringBuilder Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.Contains('='))
        {
            return null;
        }

        try
        {
            return new SqlConnectionStringBuilder(value);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }
}
