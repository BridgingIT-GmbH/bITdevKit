// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using System.Collections.Immutable;
using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Common;

/// <summary>Native provider query, retention, and committed-window operations.</summary>
/// <example><code>var page = await provider.SearchAsync(plan);</code></example>
public sealed partial class LiteDbAuditingStorageProvider
{
    private Guid StoreId { get => this.Control.StoreId; set => this.Control.StoreId = value; }

    private Guid StoreEpoch { get => this.Control.StoreEpoch; set => this.Control.StoreEpoch = value; }

    private long PublishedPosition { get => this.Control.PublishedPosition; set => this.Control.PublishedPosition = value; }

    private long LifecycleGeneration { get => this.Control.LifecycleGeneration; set => this.Control.LifecycleGeneration = value; }

    private long RemovedThrough { get => this.Control.RemovedThrough; set => this.Control.RemovedThrough = value; }

    private long RejectCompletedBefore { get => this.Control.RejectCompletedBefore; set => this.Control.RejectCompletedBefore = value; }

    private long ArchiveCompletedBefore { get => this.Control.ArchiveCompletedBefore; set => this.Control.ArchiveCompletedBefore = value; }

    /// <summary>Applies mandatory scope before returning a complete original graph.</summary>
    /// <example><code>var record = await provider.FindAsync(id, scope);</code></example>
    public Task<Result<AuditRecord>> FindAsync(Guid auditId, AuditPredicate scope, CancellationToken cancellationToken = default)
    {
        return this.RunAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var evaluator = new AuditQueryEvaluator(this.fieldCatalog);
                return Task.FromResult(this.records.TryGetValue(auditId, out var row) && evaluator.Matches(scope, row.Record, State(row))
                    ? Result<AuditRecord>.Success(row.Record) : Result<AuditRecord>.Failure(new AuditError("AuditNotFound")));
            }
            catch (ArgumentException) { return Task.FromResult(Result<AuditRecord>.Failure(new AuditError("AuditQueryInvalid"))); }
        }, failure => Result<AuditRecord>.Failure(failure), cancellationToken);
    }

    /// <summary>Reads original capture and lifecycle metadata in a single trusted native snapshot.</summary>
    /// <example><code>var stored = await provider.FindStoredAsync(id, scope);</code></example>
    public Task<Result<AuditExtractionItem>> FindStoredAsync(Guid auditId, AuditPredicate scope, CancellationToken cancellationToken = default)
    {
        return this.RunAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var evaluator = new AuditQueryEvaluator(this.fieldCatalog);
                return Task.FromResult(this.records.TryGetValue(auditId, out var row) && evaluator.Matches(scope, row.Record, State(row))
                    ? Result<AuditExtractionItem>.Success(new() { Record = row.Record, Position = row.Position, StorageState = State(row) })
                    : Result<AuditExtractionItem>.Failure(new AuditError("AuditNotFound")));
            }
            catch (ArgumentException) { return Task.FromResult(Result<AuditExtractionItem>.Failure(new AuditError("AuditQueryInvalid"))); }
        }, failure => Result<AuditExtractionItem>.Failure(failure), cancellationToken);
    }

    /// <summary>Filters, counts, orders and selects one page under one stable native operation.</summary>
    /// <example><code>var page = await provider.SearchAsync(plan);</code></example>
    public Task<ResultPaged<AuditRecordSummary>> SearchAsync(AuditSearchPlan plan, CancellationToken cancellationToken = default)
    {
        return this.RunAsync(() =>
        {
            try
            {
                var rows = this.Select(plan, cancellationToken);
                var items = Page(rows, plan).Select(row => Summary(row.Record, State(row), plan.AsOfUtc)).ToImmutableArray();
                return Task.FromResult(ResultPaged<AuditRecordSummary>.Success(items, rows.Count, plan.Page, plan.PageSize));
            }
            catch (ArgumentException) { return Task.FromResult(ResultPaged<AuditRecordSummary>.Failure(new AuditError("AuditQueryInvalid"))); }
        }, failure => ResultPaged<AuditRecordSummary>.Failure().WithErrors(failure.Errors), cancellationToken);
    }

    /// <summary>Returns one execution per exact typed-resource match with at most twenty action summaries.</summary>
    /// <example><code>var page = await provider.GetResourceHistoryAsync(plan, resource);</code></example>
    public Task<ResultPaged<AuditResourceHistoryItem>> GetResourceHistoryAsync(AuditSearchPlan plan, AuditResourceReference resource, CancellationToken cancellationToken = default)
    {
        return this.RunAsync(() =>
        {
            try
            {
                var effective = plan with { Predicate = AuditSearchPlanBuilder.And(plan.Predicate, new AuditPredicate { Kind = "ResourceMatch", Resource = resource }) };
                var rows = this.Select(effective, cancellationToken);
                var items = Page(rows, plan).Select(row =>
                {
                    var matches = row.Record.Actions.Where(a => a.Resources.Any(r => AuditQueryEvaluator.ResourceEquals(r, resource))).ToArray();
                    return new AuditResourceHistoryItem
                    {
                        Record = Summary(row.Record, State(row), plan.AsOfUtc), MatchingActionCount = matches.Length,
                        RootResourceMatched = row.Record.Resources.Any(r => AuditQueryEvaluator.ResourceEquals(r, resource)),
                        MatchingActions = matches.Take(20).Select(a => new AuditMatchingAction { ActionId = a.Id, ParentActionId = a.ParentActionId, Key = a.Key, DisplayName = a.DisplayName, Outcome = a.Outcome, StartedUtc = a.StartedUtc }).ToImmutableArray()
                    };
                }).ToImmutableArray();
                return Task.FromResult(ResultPaged<AuditResourceHistoryItem>.Success(items, rows.Count, plan.Page, plan.PageSize));
            }
            catch (ArgumentException) { return Task.FromResult(ResultPaged<AuditResourceHistoryItem>.Failure(new AuditError("AuditQueryInvalid"))); }
        }, failure => ResultPaged<AuditResourceHistoryItem>.Failure().WithErrors(failure.Errors), cancellationToken);
    }

    /// <summary>Finds explicit origin/cause links under the same trusted selection.</summary>
    /// <example><code>var page = await provider.FindRelatedAsync(query);</code></example>
    public Task<ResultPaged<AuditRecordSummary>> FindRelatedAsync(AuditRelatedExecutionsQuery query, CancellationToken cancellationToken = default)
    {
        return this.RunAsync(() =>
        {
            var links = new List<AuditPredicate>
            {
                IdMatch("OriginAuditId", query.OriginAuditId), IdMatch("CausationAuditId", query.AnchorAuditId)
            };
            links.AddRange(query.ParentIds.Where(id => id != Guid.Empty).Select(id => IdMatch("AuditId", id)));
            var predicate = AuditSearchPlanBuilder.And(query.Selection.Predicate,
                new AuditPredicate { Kind = "Or", Children = links.ToImmutableArray() },
                new AuditPredicate { Kind = "Comparison", Field = "AuditId", Operator = FilterOperator.NotEqual, Value = new AuditQueryValue { Kind = "Guid", Text = query.AnchorAuditId.ToString("D") } });
            return this.SearchAsync(query.Selection with { Predicate = predicate }, cancellationToken);
        }, failure => ResultPaged<AuditRecordSummary>.Failure().WithErrors(failure.Errors), cancellationToken);
    }

    /// <summary>Counts approved groups after all selection and scope predicates.</summary>
    /// <example><code>var groups = await provider.AggregateAsync(query);</code></example>
    public Task<Result<ImmutableArray<AuditAggregateItem>>> AggregateAsync(AuditAggregateQuery query, CancellationToken cancellationToken = default)
    {
        return this.RunAsync(() =>
        {
            if (query.Limit is < 1 or > 100) return Task.FromResult(Result<ImmutableArray<AuditAggregateItem>>.Failure(new AuditError("AuditQueryInvalid")));
            try
            {
                var evaluator = new AuditQueryEvaluator(this.fieldCatalog);
                var field = this.fieldCatalog.Find("Record", query.Field);
                if (field.IsFailure || field.Value.Kind == "Collection") throw new ArgumentException("AuditQueryInvalid");
                var groups = this.Select(query.Selection, cancellationToken).GroupBy(row => Key(evaluator.Read(row.Record, query.Field, State(row))), field.Value.IgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
                    .Select(group => new AuditAggregateItem { Key = group.Select(row => Key(evaluator.Read(row.Record, query.Field, State(row)))).OrderBy(value => value, StringComparer.Ordinal).First(), Count = group.LongCount() })
                    .OrderByDescending(group => group.Count).ThenBy(group => group.Key, StringComparer.Ordinal).Take(query.Limit).ToImmutableArray();
                return Task.FromResult(Result<ImmutableArray<AuditAggregateItem>>.Success(groups));
            }
            catch (ArgumentException) { return Task.FromResult(Result<ImmutableArray<AuditAggregateItem>>.Failure(new AuditError("AuditQueryInvalid"))); }
        }, failure => Result<ImmutableArray<AuditAggregateItem>>.Failure(failure), cancellationToken);
    }

    /// <summary>Returns bounded approved scoped suggestions without arbitrary payloads.</summary>
    /// <example><code>var facets = await provider.FindFacetsAsync(query);</code></example>
    public Task<Result<ImmutableArray<string>>> FindFacetsAsync(AuditFacetQuery query, CancellationToken cancellationToken = default)
    {
        return this.RunAsync(() =>
        {
            if (query.Limit is < 1 or > 100 || query.Prefix?.Length > 128) return Task.FromResult(Result<ImmutableArray<string>>.Failure(new AuditError("AuditQueryInvalid")));
            try
            {
                var field = this.fieldCatalog.Find("Record", query.Field);
                if (field.IsFailure || field.Value.Kind != "String") throw new ArgumentException("AuditQueryInvalid");
                var evaluator = new AuditQueryEvaluator(this.fieldCatalog);
                var comparison = field.Value.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                var values = this.Select(query.Selection, cancellationToken).Select(row => Key(evaluator.Read(row.Record, query.Field, State(row))))
                    .Where(key => key is not null && (query.Prefix is null || key.StartsWith(query.Prefix, comparison)))
                    .GroupBy(key => key, field.Value.IgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal).Select(group => group.OrderBy(key => key, StringComparer.Ordinal).First())
                    .OrderBy(key => key, StringComparer.Ordinal).Take(query.Limit).ToImmutableArray();
                return Task.FromResult(Result<ImmutableArray<string>>.Success(values));
            }
            catch (ArgumentException) { return Task.FromResult(Result<ImmutableArray<string>>.Failure(new AuditError("AuditQueryInvalid"))); }
        }, failure => Result<ImmutableArray<string>>.Failure(failure), cancellationToken);
    }

    /// <summary>Opens a fixed committed window, preserving an absolute event selection.</summary>
    /// <example><code>var cursor = await provider.OpenExtractionAsync(plan);</code></example>
    public Task<Result<AuditExtractionCursor>> OpenExtractionAsync(AuditExtractionPlan plan, CancellationToken cancellationToken = default)
    {
        return this.RunAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (plan is null || plan.Selection is null || plan.AvailabilityBaseline is < 0 || plan.AvailabilityBaseline > this.RemovedThrough || plan.MaxBatchBytes is < 128 or > 134_217_728 || plan.MaxRecordBytes is < 128 or > 16_777_216 || plan.BatchSize is < 1 or > 200 || plan.AfterPosition < 0 || plan.AfterPosition > this.PublishedPosition)
                return Task.FromResult(Result<AuditExtractionCursor>.Failure(new AuditError("AuditCursorInvalid")));
            if (plan.StoreId.HasValue && plan.StoreId != this.StoreId || plan.StoreEpoch.HasValue && plan.StoreEpoch != this.StoreEpoch)
                return Task.FromResult(Result<AuditExtractionCursor>.Failure(new AuditError("AuditEpochChanged")));
            if (plan.AfterPosition > 0 && (!plan.StoreId.HasValue || !plan.StoreEpoch.HasValue || !plan.AvailabilityBaseline.HasValue))
                return Task.FromResult(Result<AuditExtractionCursor>.Failure(new AuditError("AuditCursorInvalid")));
            if (plan.AvailabilityBaseline.HasValue && this.RemovedThrough > plan.AvailabilityBaseline && plan.AfterPosition < this.RemovedThrough)
                return Task.FromResult(Result<AuditExtractionCursor>.Failure(new AuditError("AuditHistoryGap")));
            return Task.FromResult(Result<AuditExtractionCursor>.Success(new()
            {
                StoreId = this.StoreId, StoreEpoch = this.StoreEpoch, Selection = plan.Selection,
                AfterPosition = plan.AfterPosition, UpperPosition = this.PublishedPosition,
                LifecycleGeneration = this.LifecycleGeneration, AvailabilityBaseline = this.RemovedThrough, BatchSize = plan.BatchSize, MaxBatchBytes = plan.MaxBatchBytes, MaxRecordBytes = plan.MaxRecordBytes
            }));
        }, failure => Result<AuditExtractionCursor>.Failure(failure), cancellationToken);
    }

    /// <summary>Reads complete selected graphs in publication order and validates the fixed window.</summary>
    /// <example><code>var batch = await provider.ReadExtractionAsync(cursor);</code></example>
    public Task<Result<AuditExtractionBatch>> ReadExtractionAsync(AuditExtractionCursor cursor, CancellationToken cancellationToken = default)
    {
        return this.RunAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var error = this.ValidateWindow(cursor);
                if (error is not null) return Task.FromResult(Result<AuditExtractionBatch>.Failure(new AuditError(error)));
                var evaluator = new AuditQueryEvaluator(this.fieldCatalog);
                var items = new List<AuditExtractionItem>();
                var after = cursor.AfterPosition;
                long bytes = 0;
                var exhausted = false;
                var scanned = 0;
                var stoppedForBytes = false;
                var scanLimit = Math.Min(cursor.BatchSize, this.database.Options.MaxMaterializedRows - 1);
                foreach (var row in this.database.Query<LiteDbAuditRecord>("records", BsonExpression.Create("$.Position > @0 AND $.Position <= @1", cursor.AfterPosition, cursor.UpperPosition), scanLimit, "$.Position"))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    scanned++;
                    if (MatchesLifecycle(cursor.Selection, row) && evaluator.Matches(cursor.Selection.Predicate, row.Record, State(row)))
                    {

                        if (row.Bytes > cursor.MaxRecordBytes || row.Bytes > cursor.MaxBatchBytes) return Task.FromResult(Result<AuditExtractionBatch>.Failure(new AuditError("AuditRecordTooLarge")));
                        if (bytes + row.Bytes > cursor.MaxBatchBytes) { stoppedForBytes = true; break; }

                        bytes += row.Bytes;

                        items.Add(new AuditExtractionItem { Record = row.Record, Position = row.Position, StorageState = State(row) });
                    }

                    after = row.Position;
                }

                exhausted = !stoppedForBytes && (after == cursor.UpperPosition || scanned < scanLimit);
                if (exhausted) after = cursor.UpperPosition;
                return Task.FromResult(Result<AuditExtractionBatch>.Success(new() { Items = items.ToImmutableArray(), Cursor = cursor with { AfterPosition = after }, Exhausted = exhausted }));
            }
            catch (ArgumentException) { return Task.FromResult(Result<AuditExtractionBatch>.Failure(new AuditError("AuditQueryInvalid"))); }
        }, failure => Result<AuditExtractionBatch>.Failure(failure), cancellationToken);
    }

    /// <summary>Archives eligible finalized graphs by total age from CompletedUtc.</summary>
    /// <example><code>var count = await provider.ArchiveBeforeAsync(cutoff);</code></example>
    public Task<Result<long>> ArchiveBeforeAsync(DateTimeOffset cutoff, int batchSize = 100, CancellationToken cancellationToken = default)
    {
        return this.RunAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (batchSize is < 1 or > 1_000) return Task.FromResult(Result<long>.Failure(new AuditError("AuditRetentionInvalid")));
            var rows = this.database.Query<LiteDbAuditRecord>("records", BsonExpression.Create("$.Archived = false AND $.CompletedTicks < @0", cutoff.UtcTicks), Math.Min(batchSize, this.database.Options.MaxMaterializedRows - 1), "$.CompletedTicks").ToArray();
            this.ArchiveCompletedBefore = Math.Max(this.ArchiveCompletedBefore, cutoff.UtcTicks);
            foreach (var row in rows) row.Archived = true;
            if (rows.Length > 0) this.LifecycleGeneration++;
            return Task.FromResult(Result<long>.Success(rows.LongLength));
        }, failure => Result<long>.Failure(failure), cancellationToken);
    }

    /// <summary>Removes entire archived graphs and rejects delayed expired replays.</summary>
    /// <example><code>var count = await provider.RemoveArchivedBeforeAsync(cutoff);</code></example>
    public Task<Result<long>> RemoveArchivedBeforeAsync(DateTimeOffset cutoff, int batchSize = 100, CancellationToken cancellationToken = default)
    {
        return this.RunAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (batchSize is < 1 or > 1_000) return Task.FromResult(Result<long>.Failure(new AuditError("AuditRetentionInvalid")));
            var rows = this.database.Query<LiteDbAuditRecord>("records", BsonExpression.Create("$.Archived = true AND $.CompletedTicks < @0", cutoff.UtcTicks), Math.Min(batchSize, this.database.Options.MaxMaterializedRows - 1), "$.CompletedTicks").ToArray();
            foreach (var row in rows) this.RemoveRow(row);
            this.RejectCompletedBefore = Math.Max(this.RejectCompletedBefore, cutoff.UtcTicks);
            return Task.FromResult(Result<long>.Success(rows.LongLength));
        }, failure => Result<long>.Failure(failure), cancellationToken);
    }

    private string ValidateWindow(AuditExtractionCursor cursor)
    {
        if (cursor is null) return "AuditCursorInvalid";
        if (cursor.StoreId != this.StoreId || cursor.StoreEpoch != this.StoreEpoch) return "AuditEpochChanged";
        if (cursor.Selection is null || cursor.AvailabilityBaseline < 0 || cursor.AvailabilityBaseline > this.RemovedThrough || cursor.LifecycleGeneration < 0 || cursor.LifecycleGeneration > this.LifecycleGeneration) return "AuditCursorInvalid";
        if (cursor.RequiredAfterPosition is < 0 || cursor.RequiredAfterPosition > cursor.AfterPosition || cursor.AfterPosition < 0 || cursor.UpperPosition > this.PublishedPosition || cursor.AfterPosition > cursor.UpperPosition || cursor.BatchSize is < 1 or > 200 || cursor.MaxBatchBytes is < 128 or > 134_217_728 || cursor.MaxRecordBytes is < 128 or > 16_777_216) return "AuditCursorInvalid";
        if (this.RemovedThrough > cursor.AvailabilityBaseline && Math.Min(cursor.AfterPosition, cursor.RequiredAfterPosition ?? cursor.AfterPosition) < this.RemovedThrough) return "AuditHistoryGap";
        if (cursor.Selection.RequiresStableLifecycle && this.LifecycleGeneration != cursor.LifecycleGeneration) return "AuditLifecycleChanged";
        return null;
    }

    private List<LiteDbAuditRecord> Select(AuditSearchPlan plan, CancellationToken cancellationToken)
    {
        if (plan is null || plan.Page < 1 || plan.PageSize is < 1 or > AuditQueryOptions.MAX_PAGE_SIZE) throw new ArgumentException("AuditQueryInvalid");
        var evaluator = new AuditQueryEvaluator(this.fieldCatalog);
        var result = new List<LiteDbAuditRecord>();
        foreach (var row in this.records.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (MatchesLifecycle(plan, row) && evaluator.Matches(plan.Predicate, row.Record, State(row))) result.Add(row);
        }

        result.Sort((left, right) =>
        {
            foreach (var order in plan.Orderings)
            {
                var compared = evaluator.CompareRecords(left.Record, State(left), right.Record, State(right), order);
                if (compared != 0) return compared;
            }

            return StringComparer.Ordinal.Compare(left.Record.AuditId.ToString("D"), right.Record.AuditId.ToString("D"));
        });
        return result;
    }

    private void RemoveRow(LiteDbAuditRecord row)
    {
        this.records.Remove(row.Record.AuditId);
        this.RemovedThrough = Math.Max(this.RemovedThrough, row.Position);
        this.LifecycleGeneration++;
    }

    private static IEnumerable<LiteDbAuditRecord> Page(List<LiteDbAuditRecord> rows, AuditSearchPlan plan)
    {
        var offset = ((long)plan.Page - 1) * plan.PageSize;
        return offset >= rows.Count ? [] : rows.Skip((int)offset).Take(plan.PageSize);
    }

    private static bool MatchesLifecycle(AuditSearchPlan plan, LiteDbAuditRecord row) => plan.Lifecycle switch
    {
        "Active" => !row.Archived,
        "Archived" => row.Archived,
        "All" => true,
        _ => throw new ArgumentException("AuditQueryInvalid")
    };
    private static string State(LiteDbAuditRecord row) => row.Archived ? "Archived" : "Active";
    private static AuditPredicate IdMatch(string field, Guid id) => new() { Kind = "Comparison", Field = field, Value = new AuditQueryValue { Kind = "Guid", Text = id.ToString("D") } };
    private static string Key(AuditQueryValue value) => value.Kind switch
    {
        "String" or "Guid" => value.Text,
        "Integer" or "Timestamp" => value.Ticks?.ToString(System.Globalization.CultureInfo.InvariantCulture),
        "Decimal" => value.Number?.ToString("G29", System.Globalization.CultureInfo.InvariantCulture),
        "Boolean" => value.Boolean?.ToString(),
        _ => null
    };
    private static AuditRecordSummary Summary(AuditRecord record, string state, DateTimeOffset asOf) => new()
    {
        AuditId = record.AuditId, Key = record.Key, DisplayName = record.DisplayName, Kind = record.Kind,
        StartedUtc = record.StartedUtc, CompletedUtc = record.CompletedUtc, Outcome = record.Outcome,
        Completeness = record.Completeness, Actor = record.Actor, InitiatingActor = record.InitiatingActor,
        ModuleName = record.ModuleName, CorrelationId = record.CorrelationId, OriginAuditId = record.OriginAuditId,
        CausationAuditId = record.CausationAuditId, StorageState = state, ActionCount = record.TotalActionCount,
        RetainedActionCount = record.Actions.Length, ChangeCount = record.EntityChanges.Length,
        HasFailedActions = record.HasFailedActions, AsOfUtc = asOf
    };
}
