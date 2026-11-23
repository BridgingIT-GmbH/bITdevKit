// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Common;

public sealed partial class LiteDbCacheStoreProvider
{
    /// <inheritdoc />
    public ValueTask<Result<CacheInvalidationOutcome>> RemoveAsync(CacheEntryIdentity identity, CacheWriteCondition condition = default,
        CancellationToken cancellationToken = default) => this.RemoveAsync(new CacheProviderRemoval(identity, condition), cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheInvalidationOutcome>> RemoveAsync(CacheProviderRemoval request, CancellationToken cancellationToken = default) => this.Run(db =>
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = this.Definition(request.Identity);
        if (request.Condition.CreateOnly || request.Owner.HasValue || (request.OnlyExpectedVersion && !request.Condition.ExpectedVersion.HasValue))
        { throw Failure(CacheErrorCode.Validation, "Invalid exact removal preconditions."); }

        if (request.Fence is { } fence) { this.VerifyFence(db, request.Identity, fence); }

        CheckDeadline(request.Deadline, this.timeProvider.GetUtcNow());
        CheckCondition(this.Entry(db, request.Identity), request.Condition, this.timeProvider.GetUtcNow());
        var store = this.Required<StoreState>(db, "state", "store");
        var scope = Scope(request.Identity);
        var state = this.Required<CollectionState>(db, "state", scope);
        var key = this.Get<KeyState>(db, "keys", LogicalKey(request.Identity));
        if (key is not null) { key.Epoch++; this.Put(db, "keys", LogicalKey(request.Identity), key); }

        var entries = request.OnlyExpectedVersion ? new[] { this.Entry(db, request.Identity) }.Where(e => e is not null).ToArray() :
            this.Rows(db, "entries").Find(Query.EQ("LogicalKey", LogicalKey(request.Identity)), limit: 1025).Select(Deserialize<EntryState>).ToArray();
        if (entries.Length > 1024) { throw Failure(CacheErrorCode.Capacity, "Exact removal exceeded the retained contract bound."); }

        foreach (var entry in entries) { this.Retire(db, entry, store, state); }

        state.Revision++;
        this.SaveAccounting(db, store, scope, state);
        return new CacheInvalidationOutcome { Completed = true, AffectedCount = entries.Length };
    }, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheMetadataPage>> QueryAsync(CacheMetadataQuery query, CancellationToken cancellationToken = default) => this.MetadataCommand(db =>
    {
        var definition = this.ScopeDefinition(query.Namespace, query.Collection);
        this.ValidateQuery(definition, query);
        var scope = Scope(new(definition.Namespace, definition.Collection, definition.ContractVersion, "catalog"));
        var fingerprint = Hash(JsonSerializer.Serialize(query.Selection));
        var cursor = this.Cursor(db, query.Continuation, scope, fingerprint, query.Cutoff, "query");
        var page = this.ReadPage(db, definition, query, cursor, cancellationToken);
        return page.Page;
    }, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheInvalidationOutcome>> InvalidateAsync(CacheProviderInvalidation request, CancellationToken cancellationToken = default) => this.MetadataCommand(db =>
    {
        ArgumentNullException.ThrowIfNull(request);
        var definition = this.ScopeDefinition(request.Namespace, request.Collection);
        var selection = request.Clear ? CacheSelection.Pattern("*") : request.Selection;
        var query = new CacheMetadataQuery(definition.Namespace, definition.Collection, selection, request.Cutoff,
            Math.Min(128, definition.Policies.Limits.MaxMetadataPageSize));
        this.ValidateQuery(definition, query);
        var scope = Scope(new(definition.Namespace, definition.Collection, definition.ContractVersion, "catalog"));
        var state = this.Required<CollectionState>(db, "state", scope);
        var store = this.Required<StoreState>(db, "state", "store");
        var cursor = this.Cursor(db, request.Continuation, scope, Hash(JsonSerializer.Serialize(selection)), request.Cutoff,
            (request.Clear ? "clear:" : "invalidate:") + request.OperationId);
        var tracked = this.Tracked(db, request);
        if (tracked?.CancellationRequested == true)
        {
            var cancelled = tracked.Outcome ?? new CacheInvalidationOutcome { Completed = false, OperationId = request.OperationId };
            this.Checkpoint(db, tracked, cancelled, CacheOperationState.Cancelled);
            return cancelled;
        }

        if (request.Continuation is null && !selection.IsEmpty)
        {
            state.Epoch++; state.Revision++;
            cursor = cursor with { Revision = state.Revision };
            this.SaveAccounting(db, store, scope, state);
        }

        var page = this.ReadPage(db, definition, query, cursor, cancellationToken);
        var count = cursor.Observed;
        foreach (var metadata in page.Page.Entries)
        {
            var entry = this.Entry(db, metadata.Identity);
            if (entry?.Metadata.Version != metadata.Version) { continue; }

            this.Retire(db, entry, store, state); count++;
        }

        if (page.Page.Entries.Count > 0) { state.Revision++; }

        var next = page.Cursor with { Observed = count, Revision = state.Revision };
        var outcome = new CacheInvalidationOutcome { Completed = page.Page.Completed, AffectedCount = count,
            CountIsExact = page.Page.Completed && !next.Changed, Continuation = page.Page.Completed ? null : this.Encode(db, next), OperationId = request.OperationId };
        this.SaveAccounting(db, store, scope, state);
        if (tracked is not null) { this.Checkpoint(db, tracked, outcome, outcome.Completed ? CacheOperationState.Completed : CacheOperationState.Running); }

        return outcome;
    }, cancellationToken);

    private NativePage ReadPage(LiteDatabase db, CacheProviderCollection definition, CacheMetadataQuery query, PageCursor cursor, CancellationToken token,
        Func<CacheEntryMetadata, bool> filter = null, string extraPredicate = null, BsonValue[] extraValues = null)
    {
        var state = this.Required<CollectionState>(db, "state", cursor.Scope);
        cursor = cursor with { Changed = cursor.Changed || cursor.Revision != state.Revision };
        var entries = new List<CacheEntryMetadata>();
        var examined = 0;
        var bytes = 0L;
        var evaluator = new CacheSelectionEvaluator(query.Selection, definition.Dimensions, query.Cutoff);
        var started = this.timeProvider.GetTimestamp();
        // The caller supplies its remaining allowance; each invalidation unit gets a fresh bounded allowance.
        var available = Math.Min(query.MaximumCandidates, definition.Policies.Limits.MaxQueryCandidates);
        var values = new List<BsonValue> { cursor.Scope, cursor.After, cursor.Upper, query.Cutoff.UtcTicks };
        var predicate = "$.Scope = @0 AND $.Sequence > @1 AND $.Sequence <= @2 AND $.Published <= @3 AND (" + this.SelectionPredicate(db, query.Selection, values) + ")";
        if (extraPredicate is not null)
        {
            for (var i = 0; i < extraValues.Length; i++) { extraPredicate = extraPredicate.Replace("#" + i + "#", "@" + values.Count, StringComparison.Ordinal); values.Add(extraValues[i]); }

            predicate += " AND (" + extraPredicate + ")";
        }

        var completed = query.Selection.IsEmpty;
        string reason = null;
        var stop = false;
        while (!completed && !stop && examined < available && this.timeProvider.GetElapsedTime(started) < definition.Policies.Limits.MaxQueryWork)
        {
            token.ThrowIfCancellationRequested();
            values[1] = cursor.After;
            var batch = Math.Min(Math.Min(64, this.native.MaxMaterializedRows), available - examined);
            var rows = this.Rows(db, "entries").Query().Where(BsonExpression.Create(predicate, values.ToArray())).OrderBy("$.Sequence").Limit(batch).ToList();
            if (rows.Count == 0) { completed = true; break; }

            foreach (var row in rows)
            {
                token.ThrowIfCancellationRequested();
                if (this.timeProvider.GetElapsedTime(started) >= definition.Policies.Limits.MaxQueryWork) { reason = "WorkLimit"; stop = true; break; }

                var entry = Deserialize<EntryState>(row);
                var matches = evaluator.Matches(entry.Metadata) && (filter?.Invoke(entry.Metadata) ?? true);
                if (matches && bytes + entry.SerializedMetadataBytes > definition.Policies.Limits.MaxMetadataPageBytes)
                {
                    if (entries.Count == 0) { throw Failure(CacheErrorCode.Capacity, "The stored entry metadata exceeds the configured page byte limit."); }

                    reason = "ByteLimit";
                    stop = true;
                    break;
                }

                examined++; cursor = cursor with { After = entry.Sequence };
                if (matches)
                {
                    entries.Add(entry.Metadata); bytes += entry.SerializedMetadataBytes;
                    cursor = cursor with { Observed = cursor.Observed + 1 };
                    if (entries.Count == query.PageSize) { stop = true; break; }
                }
            }

            if (!stop && rows.Count < batch) { completed = true; }
        }

        if (!completed && !stop) { reason = examined >= available ? "CandidateLimit" : "WorkLimit"; }

        if (!completed)
        {
            values[1] = cursor.After;
            completed = !this.Rows(db, "entries").Exists(BsonExpression.Create(predicate, values.ToArray()));
        }

        var continuation = completed ? null : this.Encode(db, cursor);
        return new(new(entries.AsReadOnly(), continuation, cursor.Observed, completed && !cursor.Changed, examined, completed, reason), cursor);
    }

    private CacheProviderCollection ScopeDefinition(string @namespace, string collection)
    {
        var ns = CacheIdentityNormalizer.Namespace(@namespace);
        var name = CacheIdentityNormalizer.Name(collection);
        return this.definitions.Values.FirstOrDefault(d => d.Namespace == ns && d.Collection == name)
            ?? throw Failure(CacheErrorCode.Contract, "The logical collection was not bound during initialization.");
    }

    private void ValidateQuery(CacheProviderCollection definition, CacheMetadataQuery query)
    {
        if (query.PageSize <= 0 || query.PageSize > Math.Min(1000, definition.Policies.Limits.MaxMetadataPageSize) || query.MaximumCandidates is <= 0 or > 100000)
        { throw Failure(CacheErrorCode.Validation, "Invalid finite metadata query bounds."); }

        var valid = CacheSelectionValidator.Validate(query.Selection, definition.Dimensions);
        if (valid.IsFailure) { throw new LiteDbProviderException(valid); }
    }

    private string SelectionPredicate(LiteDatabase db, CacheSelection selection, List<BsonValue> values)
    {
        string Parameter(BsonValue value) { values.Add(value); return "@" + (values.Count - 1); }

        string Field(string prefix)
        {
            var field = prefix + Hash(selection.Name)[..24];
            this.Rows(db, "entries").EnsureIndex(field, "$." + field);
            return "$." + field;
        }

        if (selection.IsEmpty) { return "1 = 0"; }

        return selection.Kind switch
        {
            CacheSelectionKind.Key => "$.KeyHash = " + Parameter(Hash(selection.Text)),
            CacheSelectionKind.Equal => Field("E") + " = " + Parameter(DimensionToken(selection.Values[0])),
            CacheSelectionKind.Contains => Field("D") + " ANY = " + Parameter(DimensionToken(selection.Values[0])),
            CacheSelectionKind.ContainsAny => "(" + string.Join(" OR ", selection.Values.Select(v => Field("D") + " ANY = " + Parameter(DimensionToken(v)))) + ")",
            CacheSelectionKind.Present => Field("P") + " = true",
            CacheSelectionKind.Absent => Field("P") + " = null",
            CacheSelectionKind.Overlap => "(" + Field("L") + " < " + Parameter(Number(selection.Values[1])) + " AND " + Field("U") + " > " + Parameter(Number(selection.Values[0])) + ")",
            CacheSelectionKind.PublishedRange => "($.Published >= " + Parameter(Number(selection.Values[0])) + " AND $.Published < " + Parameter(Number(selection.Values[1])) + ")",
            CacheSelectionKind.StoredSizeRange => "($.Size >= " + Parameter(Number(selection.Values[0])) + " AND $.Size < " + Parameter(Number(selection.Values[1])) + ")",
            CacheSelectionKind.And => "(" + string.Join(" AND ", selection.Children.Select(s => this.SelectionPredicate(db, s, values))) + ")",
            CacheSelectionKind.Or => "(" + string.Join(" OR ", selection.Children.Select(s => this.SelectionPredicate(db, s, values))) + ")",
            _ => "1 = 1"
        };
    }

    private static string DimensionToken(CacheDimensionValue value) => Hash((int)value.Kind + ":" + (value.IsNull ? "null" : "value:" + value.CanonicalValue));
    private static long Number(CacheDimensionValue value) => long.Parse(value.CanonicalValue, CultureInfo.InvariantCulture);

    private PageCursor Cursor(LiteDatabase db, string continuation, string scope, string fingerprint, DateTimeOffset cutoff, string purpose)
    {
        if (continuation is null) { return new(scope, fingerprint, cutoff.UtcTicks, purpose, 0, this.Required<StoreState>(db, "state", "store").Sequence, 0,
            this.Required<CollectionState>(db, "state", scope).Revision, false); }

        if (continuation.Length > 4096) { throw Failure(CacheErrorCode.Validation, "The metadata cursor exceeds its bound."); }

        var envelope = Convert.FromBase64String(continuation);
        if (envelope.Length <= 32) { throw Failure(CacheErrorCode.Validation, "The metadata cursor is incomplete."); }

        var data = envelope.AsSpan(32);
        var key = this.Required<StoreState>(db, "state", "store").Secret;
        if (!CryptographicOperations.FixedTimeEquals(envelope.AsSpan(0, 32), HMACSHA256.HashData(key, data))) { throw Failure(CacheErrorCode.Validation, "The metadata cursor signature is invalid."); }

        var cursor = JsonSerializer.Deserialize<PageCursor>(data);
        if (cursor.Scope != scope || cursor.Fingerprint != fingerprint || cursor.Cutoff != cutoff.UtcTicks || cursor.Purpose != purpose ||
            cursor.After < 0 || cursor.After > cursor.Upper || cursor.Observed < 0)
        { throw Failure(CacheErrorCode.Validation, "The cursor belongs to a different metadata operation."); }

        return cursor;
    }

    private string Encode(LiteDatabase db, PageCursor cursor)
    {
        var data = JsonSerializer.SerializeToUtf8Bytes(cursor);
        var key = this.Required<StoreState>(db, "state", "store").Secret;
        var envelope = new byte[32 + data.Length];
        HMACSHA256.HashData(key, data).CopyTo(envelope, 0); data.CopyTo(envelope, 32);
        return Convert.ToBase64String(envelope);
    }

    private async ValueTask<Result<T>> MetadataCommand<T>(Func<LiteDatabase, T> action, CancellationToken token)
    {
        var admitted = await this.queries.AcquireAsync(this.options.AttemptTimeout, token).ConfigureAwait(false);
        if (admitted.IsFailure) { return Result<T>.Failure(admitted); }

        using var admission = admitted.Value;
        if (!ResourceCharge.TryReserve(8 * CacheDefaults.MiB, out var memory, this.transient, this.resident))
        { return Result<T>.Failure(Error(CacheErrorCode.Capacity, "Metadata query memory admission was rejected.")); }

        using (memory) { return await this.Run(action, token).ConfigureAwait(false); }
    }

    /// <inheritdoc />
    public ValueTask<Result<CacheRefreshPage>> QueryRefreshAsync(CacheRefreshQuery query, CancellationToken cancellationToken = default) => this.MetadataCommand(db =>
    {
        var identity = new CacheEntryIdentity(query.Namespace, query.Collection, query.ContractVersion, "catalog");
        var definition = this.Definition(identity);
        if (query.Limit is <= 0 or > 1000 || query.RefreshAhead <= TimeSpan.Zero || query.ActivityWindow <= TimeSpan.Zero)
        { throw Failure(CacheErrorCode.Validation, "Invalid refresh query bounds."); }

        var fingerprint = Hash(JsonSerializer.Serialize(new { identity.ContractVersion, query.RefreshAhead, query.ActivityWindow }));
        var cursor = this.Cursor(db, query.Continuation, Scope(identity), fingerprint, query.Now, "refresh");
        var page = this.ReadPage(db, definition, new(query.Namespace, query.Collection, CacheSelection.Pattern("*"), query.Now, query.Limit,
            MaximumCandidates: Math.Min(query.Limit, definition.Policies.Limits.MaxQueryCandidates)), cursor, cancellationToken,
            metadata => metadata.Identity.ContractVersion == identity.ContractVersion && CacheRefreshPolicy.IsEligible(metadata, query),
            "$.Expires > #0# AND $.Expires <= #1#", [query.Now.UtcTicks, query.Now.Add(query.RefreshAhead).UtcTicks]);
        return new CacheRefreshPage(page.Page.Entries, page.Page.Continuation, page.Page.CandidatesExamined);
    }, cancellationToken);

    /// <inheritdoc />
    public async ValueTask<Result> RecordRefreshAttemptAsync(CacheRefreshAttempt attempt, CancellationToken cancellationToken = default)
    {
        var result = await this.Run(db =>
        {
            var definition = this.Definition(attempt.Identity);
            var entry = this.Entry(db, attempt.Identity);
            if (entry?.Metadata.Version != attempt.Version) { return true; }

            var options = definition.Policies.Refresh;
            var failures = attempt.SourceFailed ? Math.Min(options.FailuresBeforePause, entry.Metadata.RefreshFailureCount + 1) : 0;
            entry.Metadata = entry.Metadata with { RefreshFailureCount = failures,
                RefreshNotBefore = attempt.SourceFailed ? CacheRefreshPolicy.NextAttempt(options, failures, attempt.CompletedAt) : null };
            this.SaveEntry(db, entry);
            return true;
        }, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Result.Success() : Result.Failure().WithErrors(result.Errors);
    }

    private sealed record PageCursor(string Scope, string Fingerprint, long Cutoff, string Purpose, long After, long Upper, long Observed, long Revision, bool Changed);
    private sealed record NativePage(CacheMetadataPage Page, PageCursor Cursor);
}
