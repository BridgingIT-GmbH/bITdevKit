// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Auditing;

using System.Collections.Immutable;
using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Common;

/// <summary>Reads fixed committed windows in publication order with bounded graph/byte transfer and durable gap checks.</summary>
/// <example><code>var cursor = await provider.OpenExtractionAsync(plan);</code></example>
public sealed partial class EntityFrameworkAuditStorageProvider<TContext>
{
    /// <inheritdoc />
    public Task<Result<AuditExtractionCursor>> OpenExtractionAsync(AuditExtractionPlan plan, CancellationToken cancellationToken = default) => this.work.ExecuteAsync((context, session, token) =>
    {
        var state = session.State;
        if (plan.Selection is null || plan.AfterPosition < 0 || plan.AfterPosition > state.PublishedPosition || !ValidBounds(plan.BatchSize, plan.MaxBatchBytes, plan.MaxRecordBytes)) return Task.FromResult(Fail<AuditExtractionCursor>("AuditCursorInvalid"));
        if (plan.StoreId.HasValue && plan.StoreId != state.StoreId || plan.StoreEpoch.HasValue && plan.StoreEpoch != state.StoreEpoch) return Task.FromResult(Fail<AuditExtractionCursor>("AuditEpochChanged"));
        if (plan.AfterPosition > 0 && (!plan.StoreId.HasValue || !plan.StoreEpoch.HasValue || !plan.AvailabilityBaseline.HasValue)) return Task.FromResult(Fail<AuditExtractionCursor>("AuditCursorInvalid"));
        if (plan.AvailabilityBaseline < 0 || plan.AvailabilityBaseline > state.RemovedThroughPosition) return Task.FromResult(Fail<AuditExtractionCursor>("AuditCursorInvalid"));
        if (plan.AvailabilityBaseline.HasValue && state.RemovedThroughPosition > plan.AvailabilityBaseline && plan.AfterPosition < state.RemovedThroughPosition) return Task.FromResult(Fail<AuditExtractionCursor>("AuditHistoryGap"));
        _ = new AuditQuerySql(session.Sql, this.catalog).Predicate(plan.Selection.Predicate);
        return Task.FromResult(Result<AuditExtractionCursor>.Success(new()
        {
            StoreId = state.StoreId, StoreEpoch = state.StoreEpoch, Selection = plan.Selection, AfterPosition = plan.AfterPosition, UpperPosition = state.PublishedPosition,
            LifecycleGeneration = state.LifecycleGeneration, AvailabilityBaseline = state.RemovedThroughPosition, BatchSize = plan.BatchSize,
            MaxBatchBytes = plan.MaxBatchBytes, MaxRecordBytes = plan.MaxRecordBytes
        }));
    }, false, cancellationToken);

    /// <inheritdoc />
    public Task<Result<AuditExtractionBatch>> ReadExtractionAsync(AuditExtractionCursor cursor, CancellationToken cancellationToken = default) => this.work.ExecuteAsync(async (context, session, token) =>
    {
        var error = ValidateWindow(cursor, session.State);
        if (error is not null) return Fail<AuditExtractionBatch>(error);
        var sql = new AuditQuerySql(session.Sql, this.catalog);
        var position = session.Sql.Column<AuditRecordEntity>("r", nameof(AuditRecordEntity.Position));
        var identity = session.Sql.Column<AuditRecordEntity>("r", nameof(AuditRecordEntity.AuditId));
        var state = session.Sql.Column<AuditRecordEntity>("r", nameof(AuditRecordEntity.StorageState));
        var where = sql.Predicate(cursor.Selection.Predicate) + " AND " + position + ">" + sql.Parameter(cursor.AfterPosition) + " AND " + position + "<=" + sql.Parameter(cursor.UpperPosition);
        var text = "SELECT " + identity + "," + position + "," + state + " FROM " + session.Sql.Table<AuditRecordEntity>() + " r WHERE " + where + " ORDER BY " + position + Limit(sql, session.Sql, cursor.BatchSize + 1);
        var candidates = new List<ExtractionCandidate>();
        await using (var command = session.Sql.Command(text, sql.Values))
        await using (var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(token).ConfigureAwait(false)) candidates.Add(new(reader.GetGuid(0), reader.GetInt64(1), (AuditStorageState)reader.GetInt32(2)));
        }

        var items = ImmutableArray.CreateBuilder<AuditExtractionItem>();
        long bytes = 0;
        var after = cursor.AfterPosition;
        var exhausted = candidates.Count <= cursor.BatchSize;
        foreach (var candidate in candidates.Take(cursor.BatchSize))
        {
            token.ThrowIfCancellationRequested();
            var source = await AuditGraphReader.ReadAsync(session, candidate.Id, Math.Min(cursor.MaxRecordBytes, cursor.MaxBatchBytes), token).ConfigureAwait(false);
            if (source.IsFailure) return Result<AuditExtractionBatch>.Failure(source);
            var size = AuditRecordCodec.Encode(source.Value).Length;
            if (bytes + size > cursor.MaxBatchBytes) { exhausted = false; break; }

            bytes += size;
            items.Add(new() { Record = source.Value, Position = candidate.Position, StorageState = candidate.State.ToString() });
            after = candidate.Position;
        }

        if (exhausted) after = cursor.UpperPosition;
        return Result<AuditExtractionBatch>.Success(new() { Items = items.ToImmutable(), Cursor = cursor with { AfterPosition = after }, Exhausted = exhausted });
    }, false, cancellationToken);

    private static string ValidateWindow(AuditExtractionCursor cursor, AuditStoreStateEntity state)
    {
        if (cursor is null || cursor.Selection is null) return "AuditCursorInvalid";
        if (cursor.StoreId != state.StoreId || cursor.StoreEpoch != state.StoreEpoch) return "AuditEpochChanged";
        if (cursor.RequiredAfterPosition is < 0 || cursor.RequiredAfterPosition > cursor.AfterPosition || cursor.AfterPosition < 0 || cursor.AfterPosition > cursor.UpperPosition || cursor.UpperPosition > state.PublishedPosition || cursor.AvailabilityBaseline < 0 || cursor.AvailabilityBaseline > state.RemovedThroughPosition || !ValidBounds(cursor.BatchSize, cursor.MaxBatchBytes, cursor.MaxRecordBytes)) return "AuditCursorInvalid";
        if (state.RemovedThroughPosition > cursor.AvailabilityBaseline && Math.Min(cursor.AfterPosition, cursor.RequiredAfterPosition ?? cursor.AfterPosition) < state.RemovedThroughPosition) return "AuditHistoryGap";
        if (cursor.Selection.RequiresStableLifecycle && cursor.LifecycleGeneration != state.LifecycleGeneration) return "AuditLifecycleChanged";
        return null;
    }
    private static bool ValidBounds(int count, int bytes, int recordBytes) => count is >= 1 and <= 200 && bytes is >= 128 and <= 134_217_728 && recordBytes is >= 128 and <= 16_777_216;
    private static Result<T> Fail<T>(string code) => Result<T>.Failure(new AuditError(code));
    private sealed record ExtractionCandidate(Guid Id, long Position, AuditStorageState State);
}
