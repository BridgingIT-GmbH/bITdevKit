// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Auditing;

using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

/// <summary>Resolves trusted EF relational names and provider parameters behind the single audit storage contract.</summary>
/// <example><code>var dialect = new AuditSqlDialect(context);</code></example>
public sealed class AuditSqlDialect(DbContext context)
{
    /// <summary>The validated supported relational backend.</summary>
    /// <example><code>var backend = dialect.Backend;</code></example>
    public string Backend { get; } = context.Database.ProviderName switch
    {
        "Microsoft.EntityFrameworkCore.SqlServer" => "SqlServer",
        "Npgsql.EntityFrameworkCore.PostgreSQL" => "Postgres",
        "Microsoft.EntityFrameworkCore.Sqlite" => "Sqlite",
        _ => throw new InvalidOperationException("AuditProviderUnsupported")
    };

    /// <summary>Quotes one host/model-owned identifier, never a client field expression.</summary>
    /// <example><code>var quoted = dialect.Quote("AuditRecords");</code></example>
    public string Quote(string name) => this.Backend == "SqlServer" ? "[" + name.Replace("]", "]]", StringComparison.Ordinal) + "]" : "\"" + name.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    /// <summary>Returns the configured table/schema for one configured audit entity.</summary>
    /// <example><code>var table = dialect.Table&lt;AuditRecordEntity&gt;();</code></example>
    public string Table<TEntity>()
    {
        var entity = context.Model.FindEntityType(typeof(TEntity)) ?? throw new InvalidOperationException("AuditModelMissing");
        var schema = entity.GetSchema();
        return (schema is null || this.Backend == "Sqlite" ? "" : this.Quote(schema) + ".") + this.Quote(entity.GetTableName());
    }

    /// <summary>Resolves a trusted property binding through EF's configured column mapping.</summary>
    /// <example><code>var id = dialect.Column&lt;AuditRecordEntity&gt;("r", nameof(AuditRecordEntity.AuditId));</code></example>
    public string Column<TEntity>(string alias, string property)
    {
        var entity = context.Model.FindEntityType(typeof(TEntity)) ?? throw new InvalidOperationException("AuditModelMissing");
        var store = StoreObjectIdentifier.Table(entity.GetTableName(), entity.GetSchema());
        var name = entity.FindProperty(property)?.GetColumnName(store) ?? throw new InvalidOperationException("AuditModelMissing");
        return (alias is null ? "" : alias + ".") + this.Quote(name);
    }

    /// <summary>Creates an operation-owned parameterized command with the current audit transaction.</summary>
    /// <example><code>await using var command = dialect.Command(sql, parameters);</code></example>
    public DbCommand Command(string sql, IReadOnlyList<object> values = null)
    {
        var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 10;
        command.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();
        foreach (var pair in (values ?? []).Select((value, index) => (value, index)))
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@p" + pair.index;
            parameter.Value = pair.value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        return command;
    }

    /// <summary>Applies explicit ordinal ASCII comparison to a normalized expression.</summary>
    /// <example><code>var ordinal = dialect.Ordinal(expression);</code></example>
    public string Ordinal(string expression) => "(" + expression + ") COLLATE " + (this.Backend switch { "SqlServer" => "Latin1_General_100_BIN2", "Postgres" => "\"C\"", _ => "BINARY" });
}
