// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Profiling;

using System.Data;
using System.Data.Common;
using System.Text;
using BridgingIT.DevKit.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

// EF translates and parameterizes every predicate. This small SQL layer adds portable binary
// keyset comparison, binary concatenation and exact bounded aggregate projections without
// depending on an engine's text collation, GUID ordering or floating-point SUM behavior.
internal static class EntityFrameworkProfilingQuerySql<TContext> where TContext : DbContext, IProfilingDbContext
{
    internal static async Task<Guid[]> PageIdsAsync(TContext context, IQueryable<OperationProfilingEntity> query, OperationProfilingView view,
        ProfilingPageCursor cursor, int maximum, CancellationToken token)
    {
        await using var sql = Create(context, query);
        var recent = view == OperationProfilingView.Recent;
        var sort = sql.Quote(recent ? "CompletedUtcTicks" : "DurationTicks");
        var tie = sql.Quote("CanonicalIdBytes");
        var where = "";
        if (cursor is not null)
        {
            var value = sql.Parameter(cursor.SortValue, DbType.Int64);
            var id = sql.Parameter(Encoding.ASCII.GetBytes(cursor.Identity), DbType.Binary);
            where = $"WHERE ({sort} < {value} OR ({sort} = {value} AND {tie} {(recent ? "<" : ">")} {id}))";
        }

        sql.Command.CommandText = $"WITH {sql.Quote("Base")} AS ({sql.Base}) SELECT {sql.Top(maximum)}{sql.Quote("Id")} FROM {sql.Quote("Base")} {where} ORDER BY {sort} DESC, {tie} {(recent ? "DESC" : "ASC")}{sql.Limit(maximum)}";
        var values = new List<Guid>(maximum);
        await using var reader = await ExecuteAsync(context, sql.Command, token).ConfigureAwait(false);
        while (await reader.ReadAsync(token).ConfigureAwait(false)) { values.Add(reader.GetGuid(0)); }

        return values.ToArray();
    }

    internal static async Task<(long Operations, long Groups)> CountsAsync(TContext context, IQueryable<OperationProfilingEntity> roots, OperationProfilingQuery query, CancellationToken token)
    {
        await using var sql = Create(context, roots);
        var grouped = Grouped(context, sql, query);
        sql.Command.CommandText = $"{grouped} SELECT (SELECT {sql.Count} FROM {sql.Quote("Grouped")}), (SELECT {sql.Count} FROM (SELECT {sql.Quote("GroupKey")} FROM {sql.Quote("Grouped")} GROUP BY {sql.Quote("GroupKey")}) {sql.Quote("Counts")})";
        await using var reader = await ExecuteAsync(context, sql.Command, token).ConfigureAwait(false);
        await reader.ReadAsync(token).ConfigureAwait(false);
        return (reader.GetInt64(0), reader.GetInt64(1));
    }

    internal static async Task<GroupRow[]> GroupsAsync(TContext context, IQueryable<OperationProfilingEntity> roots, OperationProfilingQuery query,
        ProfilingPageCursor cursor, string[] keys, CancellationToken token)
    {
        await using var sql = Create(context, roots);
        var prefix = Grouped(context, sql, query);
        var group = sql.Quote("GroupKey");
        var duration = sql.Quote("DurationTicks");
        var filter = "";
        if (keys is not null)
        {
            filter = " WHERE " + group + " IN (" + string.Join(",", keys.Select(key => sql.Parameter(Encoding.BigEndianUnicode.GetBytes(key), DbType.Binary))) + ")";
        }

        // Exact Int64 duration sum: four 16-bit limb sums never overflow at the configured
        // Int32 root-count bound, even when the combined duration is greater than Int64.MaxValue.
        var aggregates = $"SELECT {group}, {sql.Count} AS {sql.Quote("Count")}, "
            + $"SUM({duration} % 65536) AS {sql.Quote("L0")}, SUM(({duration} / 65536) % 65536) AS {sql.Quote("L1")}, "
            + $"SUM(({duration} / CAST(4294967296 AS BIGINT)) % 65536) AS {sql.Quote("L2")}, SUM(({duration} / CAST(281474976710656 AS BIGINT)) % 65536) AS {sql.Quote("L3")}, "
            + $"MIN({duration}) AS {sql.Quote("Minimum")}, MAX({duration}) AS {sql.Quote("Maximum")}, MAX({sql.Quote("CompletedUtcTicks")}) AS {sql.Quote("Latest")} "
            + $"FROM {sql.Quote("Grouped")}{filter} GROUP BY {group}";
        var where = "";
        if (cursor is not null)
        {
            var count = sql.Parameter(cursor.SortValue, DbType.Int64);
            var identity = sql.Parameter(Encoding.BigEndianUnicode.GetBytes(cursor.Identity), DbType.Binary);
            where = $"WHERE (a.{sql.Quote("Count")} < {count} OR (a.{sql.Quote("Count")} = {count} AND a.{group} > {identity}))";
        }

        var maximum = query.PageSize + 1;
        var bounded = keys is null;
        sql.Command.CommandText = prefix + $", {sql.Quote("Aggregates")} AS ({aggregates}), {sql.Quote("Ranked")} AS ("
            + $"SELECT {sql.Quote("Id")}, {group}, ROW_NUMBER() OVER (PARTITION BY {group} ORDER BY {sql.Quote("StartedUtcTicks")} ASC, {sql.Quote("CanonicalIdBytes")} ASC) AS {sql.Quote("LabelRank")}, "
            + $"ROW_NUMBER() OVER (PARTITION BY {group} ORDER BY {duration} DESC, {sql.Quote("CanonicalIdBytes")} ASC) AS {sql.Quote("SlowRank")} FROM {sql.Quote("Grouped")}{filter}) "
            + $"SELECT {(bounded ? sql.Top(maximum) : "")}a.{group}, a.{sql.Quote("Count")}, a.{sql.Quote("L0")}, a.{sql.Quote("L1")}, a.{sql.Quote("L2")}, a.{sql.Quote("L3")}, "
            + $"a.{sql.Quote("Minimum")}, a.{sql.Quote("Maximum")}, a.{sql.Quote("Latest")}, l.{sql.Quote("Id")}, r.{sql.Quote("Id")} "
            + $"FROM {sql.Quote("Aggregates")} a JOIN {sql.Quote("Ranked")} l ON l.{group} = a.{group} AND l.{sql.Quote("LabelRank")} = 1 "
            + $"JOIN {sql.Quote("Ranked")} r ON r.{group} = a.{group} AND r.{sql.Quote("SlowRank")} = 1 {where} ORDER BY a.{sql.Quote("Count")} DESC, a.{group} ASC{(bounded ? sql.Limit(maximum) : "")}";
        var values = new List<GroupRow>();
        await using var reader = await ExecuteAsync(context, sql.Command, token).ConfigureAwait(false);
        while (await reader.ReadAsync(token).ConfigureAwait(false))
        {
            var total = (decimal)reader.GetInt64(2) + (decimal)reader.GetInt64(3) * 65536m + (decimal)reader.GetInt64(4) * 4294967296m + (decimal)reader.GetInt64(5) * 281474976710656m;
            values.Add(new(Encoding.BigEndianUnicode.GetString(reader.GetFieldValue<byte[]>(0)), reader.GetInt64(1), total > long.MaxValue ? 0 : (long)total,
                total > long.MaxValue, reader.GetInt64(6), reader.GetInt64(7), reader.GetInt64(8), reader.GetGuid(9), reader.GetGuid(10)));
        }

        return values.ToArray();
    }

    private static string Grouped(TContext context, Sql sql, OperationProfilingQuery query)
    {
        var parts = new List<string> { "b." + sql.Quote("BaseGroupBytes") };
        if (query.GroupByHttpMethod) { parts.Add("b." + sql.Quote("HttpMethodGroupBytes")); }

        var joins = new StringBuilder();
        var metadata = context.Model.FindEntityType(typeof(OperationProfilingDimensionEntity));
        var identifier = StoreObjectIdentifier.Table(metadata.GetTableName(), metadata.GetSchema());
        string Column(string property) => sql.Quote(metadata.FindProperty(property).GetColumnName(identifier));
        var table = metadata.GetSchema() is { } schema ? sql.Quote(schema) + "." + sql.Quote(metadata.GetTableName()) : sql.Quote(metadata.GetTableName());
        for (var index = 0; index < query.GroupingDimensions.Count; index++)
        {
            var name = query.GroupingDimensions[index];
            var bytes = ProfilingOperationComparisons.Exact(name);
            var alias = "d" + index;
            joins.Append(" LEFT JOIN ").Append(table).Append(' ').Append(alias).Append(" ON ").Append(alias).Append('.').Append(Column("OperationId")).Append(" = b.").Append(sql.Quote("Id"))
                .Append(" AND ").Append(alias).Append('.').Append(Column("SegmentId")).Append(" IS NULL AND ").Append(alias).Append('.').Append(Column("KeyHash")).Append(" = ").Append(sql.Parameter(ProfilingOperationComparisons.Hash(bytes), DbType.Binary))
                .Append(" AND ").Append(alias).Append('.').Append(Column("KeyBytes")).Append(" = ").Append(sql.Parameter(bytes, DbType.Binary));
            parts.Add(sql.Parameter(ProfilingOperationComparisons.Part(name), DbType.Binary));
            parts.Add($"COALESCE({alias}.{Column("GroupPartBytes")}, {sql.Parameter(ProfilingOperationComparisons.ValuePart(null), DbType.Binary)})");
        }

        var key = sql.Concat(parts);
        return $"WITH {sql.Quote("Base")} AS ({sql.Base}), {sql.Quote("Grouped")} AS (SELECT b.*, {key} AS {sql.Quote("GroupKey")} FROM {sql.Quote("Base")} b{joins})";
    }

    private static Sql Create(TContext context, IQueryable<OperationProfilingEntity> roots) =>
        new(CreateCommand(context, roots), context.Database.ProviderName);

    /// <summary>Creates the portable base projection using the query-owned context's configured command timeout.</summary>
    /// <remarks>The caller owns command disposal. Direct aggregate commands do not use EF execution interceptors.</remarks>
    /// <example><code>using var command = EntityFrameworkProfilingQuerySql&lt;TContext&gt;.CreateCommand(context, roots);</code></example>
    public static DbCommand CreateCommand(TContext context, IQueryable<OperationProfilingEntity> roots)
    {
        return roots.Select(root => new Projection
        {
            Id = root.Id, CanonicalIdBytes = root.CanonicalIdBytes, StartedUtcTicks = root.StartedUtcTicks, CompletedUtcTicks = root.CompletedUtcTicks,
            DurationTicks = root.DurationTicks, BaseGroupBytes = root.BaseGroupBytes, HttpMethodGroupBytes = root.HttpMethodGroupBytes,
        }).CreateDbCommand();
    }

    private static async Task<DbDataReader> ExecuteAsync(TContext context, DbCommand command, CancellationToken token)
    {
        if (command.Connection.State != ConnectionState.Open)
        {
            await context.Database.OpenConnectionAsync(token).ConfigureAwait(false);
        }

        return await command.ExecuteReaderAsync(token).ConfigureAwait(false);
    }

    internal sealed record GroupRow(string Key, long Count, long DurationTicks, bool Unavailable, long MinimumTicks, long MaximumTicks, long LatestTicks, Guid LabelId, Guid RepresentativeId);

    private sealed class Projection
    {
        public Guid Id { get; init; }
        public byte[] CanonicalIdBytes { get; init; }
        public long StartedUtcTicks { get; init; }
        public long CompletedUtcTicks { get; init; }
        public long DurationTicks { get; init; }
        public byte[] BaseGroupBytes { get; init; }
        public byte[] HttpMethodGroupBytes { get; init; }
    }

    private sealed class Sql(DbCommand command, string provider) : IAsyncDisposable
    {
        public DbCommand Command { get; } = command;
        public string Base { get; } = command.CommandText.TrimEnd(';');
        public string Count => provider == "Microsoft.EntityFrameworkCore.SqlServer" ? "COUNT_BIG(*)" : "COUNT(*)";
        public string Quote(string name) => provider == "Microsoft.EntityFrameworkCore.SqlServer" ? "[" + name.Replace("]", "]]", StringComparison.Ordinal) + "]" : "\"" + name.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        public string Parameter(object value, DbType type)
        {
            var parameter = this.Command.CreateParameter();
            parameter.ParameterName = "@__profiling_" + this.Command.Parameters.Count;
            parameter.DbType = type;
            parameter.Value = value;
            this.Command.Parameters.Add(parameter);
            return parameter.ParameterName;
        }

        public string Top(int count) => provider == "Microsoft.EntityFrameworkCore.SqlServer" ? $"TOP ({this.Parameter(count, DbType.Int32)}) " : "";
        public string Limit(int count) => provider == "Microsoft.EntityFrameworkCore.SqlServer" ? "" : " LIMIT " + this.Parameter(count, DbType.Int32);
        public string Concat(IEnumerable<string> parts)
        {
            var joined = string.Join(provider == "Microsoft.EntityFrameworkCore.SqlServer" ? " + " : " || ", parts);
            return provider == "Microsoft.EntityFrameworkCore.Sqlite" ? $"CAST(({joined}) AS BLOB)" : "(" + joined + ")";
        }

        public ValueTask DisposeAsync() => this.Command.DisposeAsync();
    }
}
