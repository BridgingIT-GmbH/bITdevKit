// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Auditing;

using System.Collections.Immutable;
using System.Globalization;
using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Common;

/// <summary>Executes approved selection, exact counts, aggregates and distinct history before bounded paging.</summary>
/// <example><code>var page = await provider.SearchAsync(plan);</code></example>
public sealed partial class EntityFrameworkAuditStorageProvider<TContext>
{
    /// <inheritdoc />
    public async Task<ResultPaged<AuditRecordSummary>> SearchAsync(AuditSearchPlan plan, CancellationToken cancellationToken = default)
    {
        var result = await this.work.ExecuteAsync((context, session, token) => this.SearchPageAsync(session, plan, token), false, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? ResultPaged<AuditRecordSummary>.Success(result.Value.Items, result.Value.Count, plan.Page, plan.PageSize) : ResultPaged<AuditRecordSummary>.Failure(result.Messages, result.Errors);
    }

    /// <inheritdoc />
    public Task<ResultPaged<AuditRecordSummary>> FindRelatedAsync(AuditRelatedExecutionsQuery query, CancellationToken cancellationToken = default)
    {
        if (query.ParentIds.Length > 32 || query.AnchorAuditId == Guid.Empty) return Task.FromResult(ResultPaged<AuditRecordSummary>.Failure(new AuditError("AuditQueryInvalid")));
        var links = new List<AuditPredicate> { Id("CausationAuditId", query.AnchorAuditId) };
        if (query.OriginAuditId != Guid.Empty) links.Add(Id("OriginAuditId", query.OriginAuditId));
        links.AddRange(query.ParentIds.Where(id => id != Guid.Empty).Select(id => Id("AuditId", id)));
        var effective = query.Selection with { Predicate = AuditSearchPlanBuilder.And(query.Selection.Predicate, new() { Kind = "Or", Children = links.ToImmutableArray() }, Id("AuditId", query.AnchorAuditId) with { Operator = FilterOperator.NotEqual }) };
        return this.SearchAsync(effective, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<ImmutableArray<AuditAggregateItem>>> AggregateAsync(AuditAggregateQuery query, CancellationToken cancellationToken = default)
    {
        if (query.Limit is < 1 or > 100) return Result<ImmutableArray<AuditAggregateItem>>.Failure(new AuditError("AuditQueryInvalid"));
        return await this.work.ExecuteAsync(async (context, session, token) =>
        {
            var sql = new AuditQuerySql(session.Sql, this.catalog);
            var where = sql.Predicate(query.Selection.Predicate);
            var field = sql.RootField(query.Field);
            var source = "(SELECT " + field.Selected + " AS v," + field.Exact + " AS e,CASE WHEN " + field.Comparable + " THEN 1 ELSE 0 END AS c FROM " + session.Sql.Table<AuditRecordEntity>() + " r WHERE " + where + ") q";
            var group = "CASE WHEN q.c=1 THEN q.v ELSE NULL END";
            var representative = "MIN(CASE WHEN q.c=1 THEN q.e ELSE NULL END)";
            var count = session.Sql.Backend == "SqlServer" ? "COUNT_BIG(*)" : "COUNT(*)";
            var text = "SELECT " + representative + "," + count + " FROM " + source + " GROUP BY " + group +
                " ORDER BY " + count + " DESC, CASE WHEN " + representative + " IS NULL THEN 0 ELSE 1 END ASC," + representative + " ASC";
            text += Limit(sql, session.Sql, query.Limit);
            var items = ImmutableArray.CreateBuilder<AuditAggregateItem>();
            await using var command = session.Sql.Command(text, sql.Values);
            await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            while (await reader.ReadAsync(token).ConfigureAwait(false)) items.Add(new()
            {
                Key = reader.IsDBNull(0) ? null : field.IsStorageState ? ((AuditStorageState)Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture)).ToString() : AuditSearchEncoding.Decode(reader.GetString(0), field.Field.Kind),
                Count = Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture)
            });
            return Result<ImmutableArray<AuditAggregateItem>>.Success(items.ToImmutable());
        }, false, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Result<ImmutableArray<string>>> FindFacetsAsync(AuditFacetQuery query, CancellationToken cancellationToken = default)
    {
        if (query.Limit is < 1 or > 100 || query.Prefix?.Length > 128) return Result<ImmutableArray<string>>.Failure(new AuditError("AuditQueryInvalid"));
        return await this.work.ExecuteAsync(async (context, session, token) =>
        {
            var sql = new AuditQuerySql(session.Sql, this.catalog);
            var where = sql.Predicate(query.Selection.Predicate);
            var field = sql.RootField(query.Field);
            if (field.Field.Kind != "String") return Result<ImmutableArray<string>>.Failure(new AuditError("AuditQueryInvalid"));
            where += " AND " + field.Comparable;
            if (query.Prefix is not null)
            {
                if (field.IsStorageState)
                {
                    var states = new[] { "Active", "Archived" }.Select((name, index) => (name, index)).Where(state => state.name.StartsWith(query.Prefix, StringComparison.Ordinal)).Select(state => field.Selected + "=" + sql.Parameter(state.index)).ToArray();
                    where += " AND (" + (states.Length == 0 ? "1=0" : string.Join(" OR ", states)) + ")";
                }
                else where += " AND " + field.Selected + " LIKE (" + sql.Parameter(AuditSearchEncoding.Text(query.Prefix, field.Field.IgnoreCase)) + (session.Sql.Backend == "SqlServer" ? "+'%')" : "||'%')");
            }

            var source = "(SELECT " + field.Selected + " AS v," + field.Exact + " AS e FROM " + session.Sql.Table<AuditRecordEntity>() + " r WHERE " + where + ") q";
            var representative = "MIN(q.e)";
            var text = "SELECT " + representative + " FROM " + source + " GROUP BY q.v ORDER BY " + representative + " ASC" + Limit(sql, session.Sql, query.Limit);
            var items = ImmutableArray.CreateBuilder<string>();
            await using var command = session.Sql.Command(text, sql.Values);
            await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            while (await reader.ReadAsync(token).ConfigureAwait(false)) items.Add(field.IsStorageState ? ((AuditStorageState)Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture)).ToString() : AuditSearchEncoding.Decode(reader.GetString(0), "String"));
            return Result<ImmutableArray<string>>.Success(items.ToImmutable());
        }, false, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ResultPaged<AuditResourceHistoryItem>> GetResourceHistoryAsync(AuditSearchPlan plan, AuditResourceReference resource, CancellationToken cancellationToken = default)
    {
        if (resource is null) return ResultPaged<AuditResourceHistoryItem>.Failure(new AuditError("AuditResourceInvalid"));
        var selected = plan with { Predicate = AuditSearchPlanBuilder.And(plan.Predicate, new() { Kind = "ResourceMatch", Resource = resource }) };
        var result = await this.work.ExecuteAsync(async (context, session, token) =>
        {
            var page = await this.SearchPageAsync(session, selected, token).ConfigureAwait(false);
            if (page.IsFailure) return Result<Page<AuditResourceHistoryItem>>.Failure(page);
            var items = ImmutableArray.CreateBuilder<AuditResourceHistoryItem>();
            foreach (var summary in page.Value.Items)
            {
                var sql = new AuditQuerySql(session.Sql, this.catalog);
                var actionMatch = ResourcePredicate(resource, "Action");
                var where = sql.ActionPredicate(actionMatch) + " AND " + session.Sql.Column<AuditActionEntity>("a", nameof(AuditActionEntity.AuditId)) + "=" + sql.Parameter(summary.AuditId);
                var count = await CountAsync(session, sql, session.Sql.Table<AuditActionEntity>() + " a", where, token).ConfigureAwait(false);
                var columns = new[] { nameof(AuditActionEntity.Id), nameof(AuditActionEntity.ParentActionId), nameof(AuditActionEntity.Key), nameof(AuditActionEntity.Outcome), nameof(AuditActionEntity.StartedUtcTicks) }.Select(name => session.Sql.Column<AuditActionEntity>("a", name));
                var label = sql.OwnedField("Action", "DisplayName", "a");
                var text = "SELECT " + string.Join(",", columns) + "," + label.Exact + " FROM " + session.Sql.Table<AuditActionEntity>() + " a WHERE " + where + " ORDER BY " + session.Sql.Column<AuditActionEntity>("a", nameof(AuditActionEntity.Ordinal)) + Limit(sql, session.Sql, 20);
                var matches = ImmutableArray.CreateBuilder<AuditMatchingAction>();
                await using (var command = session.Sql.Command(text, sql.Values))
                await using (var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false)) matches.Add(new()
                    {
                        ActionId = reader.GetGuid(0), ParentActionId = reader.IsDBNull(1) ? null : reader.GetGuid(1), Key = reader.GetString(2), Outcome = Enum.Parse<AuditOutcome>(reader.GetString(3)),
                        StartedUtc = new(reader.GetInt64(4), TimeSpan.Zero), DisplayName = reader.IsDBNull(5) ? null : AuditSearchEncoding.Decode(reader.GetString(5), "String")
                    });
                }

                var rootSql = new AuditQuerySql(session.Sql, this.catalog);
                var rootWhere = rootSql.Predicate(ResourcePredicate(resource, "Record")) + " AND " + session.Sql.Column<AuditRecordEntity>("r", nameof(AuditRecordEntity.AuditId)) + "=" + rootSql.Parameter(summary.AuditId);
                var rootMatch = await CountAsync(session, rootSql, session.Sql.Table<AuditRecordEntity>() + " r", rootWhere, token).ConfigureAwait(false) > 0;
                items.Add(new() { Record = summary, MatchingActionCount = count, MatchingActions = matches.ToImmutable(), RootResourceMatched = rootMatch });
            }

            return Result<Page<AuditResourceHistoryItem>>.Success(new(items.ToImmutable(), page.Value.Count));
        }, false, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? ResultPaged<AuditResourceHistoryItem>.Success(result.Value.Items, result.Value.Count, plan.Page, plan.PageSize) : ResultPaged<AuditResourceHistoryItem>.Failure(result.Messages, result.Errors);
    }

    private async Task<Result<Page<AuditRecordSummary>>> SearchPageAsync(AuditStoreSession session, AuditSearchPlan plan, CancellationToken token)
    {
        var sql = new AuditQuerySql(session.Sql, this.catalog);
        var where = sql.Predicate(plan.Predicate);
        var table = session.Sql.Table<AuditRecordEntity>() + " r";
        var count = await CountAsync(session, sql, table, where, token).ConfigureAwait(false);
        var text = "SELECT " + AuditSummarySql.Columns(session.Sql) + " FROM " + table + " WHERE " + where + " ORDER BY " + sql.Order(plan) + sql.Page(plan);
        var rows = ImmutableArray.CreateBuilder<AuditRecordSummary>();
        await using var command = session.Sql.Command(text, sql.Values);
        await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        while (await reader.ReadAsync(token).ConfigureAwait(false)) rows.Add(AuditSummarySql.Read(reader, plan.AsOfUtc));
        return Result<Page<AuditRecordSummary>>.Success(new(rows.ToImmutable(), count));
    }
    private static async Task<long> CountAsync(AuditStoreSession session, AuditQuerySql sql, string table, string where, CancellationToken token)
    {
        await using var command = session.Sql.Command("SELECT " + (session.Sql.Backend == "SqlServer" ? "COUNT_BIG(*)" : "COUNT(*)") + " FROM " + table + " WHERE " + where, sql.Values);
        return Convert.ToInt64(await command.ExecuteScalarAsync(token).ConfigureAwait(false), CultureInfo.InvariantCulture);
    }
    private static string Limit(AuditQuerySql sql, AuditSqlDialect dialect, int count) => dialect.Backend == "SqlServer" ? " OFFSET 0 ROWS FETCH NEXT " + sql.Parameter(count) + " ROWS ONLY" : " LIMIT " + sql.Parameter(count);
    private static AuditPredicate Id(string field, Guid value) => new() { Kind = "Comparison", Field = field, Value = new() { Kind = "Guid", Text = value.ToString("D") } };
    private static AuditPredicate ResourcePredicate(AuditResourceReference resource, string domain) => new()
    {
        Kind = "Collection", Domain = domain, Collection = "Resources", Operator = FilterOperator.Any,
        Children = [AuditSearchPlanBuilder.And(new() { Kind = "Comparison", Domain = "Resource", Field = "Type", Value = new() { Kind = "String", Text = resource.Type } },
            new() { Kind = "Comparison", Domain = "Resource", Field = "Id", Value = new() { Kind = "String", Text = resource.Id } })]
    };
    private sealed record Page<T>(ImmutableArray<T> Items, long Count);
}
