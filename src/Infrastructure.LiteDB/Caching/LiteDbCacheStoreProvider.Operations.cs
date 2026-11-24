// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using System.Text.Json;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Common;

public sealed partial class LiteDbCacheStoreProvider
{
    private const long OperationCharge = 132 * CacheDefaults.KiB;

    /// <inheritdoc />
    public ValueTask<Result<CacheProviderOperation>> SaveOperationAsync(CacheProviderOperation operation, string expectedRevision = null,
        CancellationToken cancellationToken = default) => this.Run(db =>
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.Request is null || !Guid.TryParseExact(operation.Id, "N", out _) || operation.Request.Continuation is not null ||
            operation.Origin?.Length > 128 || !Enum.IsDefined(operation.State))
        { throw Failure(CacheErrorCode.Validation, "Invalid tracked operation metadata."); }

        var definition = this.ScopeDefinition(operation.Request.Namespace, operation.Request.Collection);
        var selection = operation.Request.Clear ? CacheSelection.Pattern("*") : operation.Request.Selection;
        this.ValidateQuery(definition, new(definition.Namespace, definition.Collection, selection, operation.Cutoff, PageSize: 1));
        var normalized = operation with { Collection = definition.Collection, Cutoff = operation.Cutoff.ToUniversalTime(),
            Request = operation.Request with { Namespace = definition.Namespace, Collection = definition.Collection, Cutoff = operation.Cutoff, OperationId = operation.Id } };
        if (JsonSerializer.SerializeToUtf8Bytes(normalized).Length > (expectedRevision is null ? 32768 : 65536))
        { throw Failure(CacheErrorCode.Capacity, "Tracked operation metadata exceeds its finite byte bound."); }

        var previous = this.Get<CacheProviderOperation>(db, "operations", operation.Id);
        if (previous is not null)
        {
            if (expectedRevision is null || previous.Revision != expectedRevision || !SameOperation(previous, normalized) || !Transition(previous.State, normalized.State)) { throw Conflict(); }

            normalized = normalized with { Revision = Guid.NewGuid().ToString("N"), CreatedAt = previous.CreatedAt, UpdatedAt = this.timeProvider.GetUtcNow(), Outcome = previous.Outcome };
        }
        else
        {
            if (expectedRevision is not null) { throw Failure(CacheErrorCode.NotFound, "The retained operation is unavailable."); }

            if (operation.State != CacheOperationState.Pending || this.Rows(db, "operations").Count() >= this.diagnostics.MaximumOperationRecords)
            { throw Failure(CacheErrorCode.Capacity, "The bounded tracked-operation catalog is full."); }

            var scope = Scope(new(definition.Namespace, definition.Collection, definition.ContractVersion, "catalog"));
            var store = this.Required<StoreState>(db, "state", "store");
            var state = this.Required<CollectionState>(db, "state", scope);
            this.ChargeMetadata(store, state, definition, OperationCharge);
            this.SaveAccounting(db, store, scope, state);
            normalized = normalized with { Revision = Guid.NewGuid().ToString("N"), CreatedAt = this.timeProvider.GetUtcNow(), UpdatedAt = this.timeProvider.GetUtcNow() };
        }

        this.SaveOperation(db, normalized);
        return normalized;
    }, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheProviderOperation>> GetOperationAsync(string operationId, CancellationToken cancellationToken = default) => this.Run(db =>
    {
        var operation = this.Get<CacheProviderOperation>(db, "operations", operationId);
        if (operation is null || !this.AllowedScope(Scope(new(operation.Request.Namespace, operation.Request.Collection, "scope", "catalog"))))
        { throw Failure(CacheErrorCode.NotFound, "The retained operation is unavailable in this catalog."); }

        return operation;
    }, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<CacheProviderOperation>>> GetOperationsAsync(int limit = 100, CancellationToken cancellationToken = default) => this.Run(db =>
    {
        if (limit is <= 0 or > 1000) { throw Failure(CacheErrorCode.Validation, "Invalid tracked-operation page size."); }

        var maximum = Math.Min(limit, 32);
        var scope = Query.In("Scope", this.AllowedScopes());
        var collection = this.Rows(db, "operations");
        // Maintenance filters this bounded page for runnable work, so retained statuses must follow it.
        var rows = collection.Query().Where(Query.And(scope, Query.EQ("Runnable", true))).OrderBy("$.Sequence").Limit(maximum).ToList();
        if (rows.Count < maximum)
        {
            rows.AddRange(collection.Query().Where(Query.And(scope, Query.Not("Runnable", true))).OrderBy("$.Sequence").Limit(maximum - rows.Count).ToList());
        }

        return (IReadOnlyList<CacheProviderOperation>)rows.Select(Deserialize<CacheProviderOperation>).ToArray();
    }, cancellationToken);

    private void SaveOperation(LiteDatabase db, CacheProviderOperation operation)
    {
        var scope = Scope(new(operation.Request.Namespace, operation.Request.Collection, "scope", "catalog"));
        this.Put(db, "operations", operation.Id, operation, scope, operation.UpdatedAt.UtcTicks);
        var row = this.Rows(db, "operations").FindById(operation.Id);
        row["Terminal"] = Terminal(operation.State);
        row["Runnable"] = operation.State is CacheOperationState.Pending or CacheOperationState.Running;
        this.Rows(db, "operations").Update(row);
    }

    private CacheProviderOperation Tracked(LiteDatabase db, CacheProviderInvalidation request)
    {
        if (request.OperationId is null) { return null; }

        var operation = this.Get<CacheProviderOperation>(db, "operations", request.OperationId) ?? throw Failure(CacheErrorCode.NotFound, "The tracked operation is unavailable.");
        if (operation.State != CacheOperationState.Running || operation.Outcome?.Continuation != request.Continuation ||
            operation.Cutoff != request.Cutoff || operation.Request.Clear != request.Clear ||
            operation.Request.Namespace != CacheIdentityNormalizer.Namespace(request.Namespace) || operation.Request.Collection != CacheIdentityNormalizer.Name(request.Collection) ||
            (!request.Clear && Hash(JsonSerializer.Serialize(operation.Request.Selection)) != Hash(JsonSerializer.Serialize(request.Selection)))) { throw Conflict(); }

        return operation;
    }

    private void Checkpoint(LiteDatabase db, CacheProviderOperation operation, CacheInvalidationOutcome outcome, CacheOperationState state) => this.SaveOperation(db,
        operation with { Outcome = outcome, State = state, Revision = Guid.NewGuid().ToString("N"), UpdatedAt = this.timeProvider.GetUtcNow() });

    /// <inheritdoc />
    public ValueTask<Result<CacheMaintenanceOutcome>> MaintainAsync(CacheMaintenanceRequest request, CancellationToken cancellationToken = default) => this.Run(db =>
    {
        if (request.BatchSize is <= 0 or > 1000) { throw Failure(CacheErrorCode.Validation, "Invalid finite maintenance batch size."); }

        var left = Math.Min(request.BatchSize, this.native.MaxMaterializedRows);
        var reclaimed = 0L;
        var bytes = 0L;
        var now = this.timeProvider.GetUtcNow();
        bool Admit() => left > 0 && this.timeProvider.GetUtcNow() < request.NewWorkDeadline;
        this.RecoverAuthority(db, ref left, request.NewWorkDeadline, cancellationToken);
        // Metadata and generation descriptors are independent from payload chunks. Each chunk consumes one cleanup unit.
        var entries = this.Rows(db, "entries").Query().Where(BsonExpression.Create("$.Expires <= @0", Min(request.RetentionCutoff, now).UtcTicks)).OrderBy("$.Expires").Limit(left).ToList();
        foreach (var row in entries)
        {
            if (!Admit()) { break; }

            cancellationToken.ThrowIfCancellationRequested();
            var entry = Deserialize<EntryState>(row);
            var definition = this.definitions.Values.FirstOrDefault(d => d.Namespace == entry.Metadata.Identity.Namespace && d.Collection == entry.Metadata.Identity.Collection);
            var grace = definition?.Policies.Expiration.RetentionGrace ?? TimeSpan.Zero;
            if (entry.Metadata.ExpiresAt > now.Subtract(grace)) { continue; }

            var store = this.Required<StoreState>(db, "state", "store");
            var state = this.Required<CollectionState>(db, "state", Scope(entry.Metadata.Identity));
            this.Retire(db, entry, store, state); state.Revision++;
            this.SaveAccounting(db, store, Scope(entry.Metadata.Identity), state);
            left--; reclaimed++;
        }

        var payloads = this.Rows(db, "payloads").Query().Where(BsonExpression.Create("($.State = 'retired' OR ($.State = 'staging' AND $.Owner != @0)) AND ($.Pins = 0 OR $.PinOwner != @0)", this.instance)).Limit(Math.Max(1, left)).ToList();
        foreach (var row in payloads)
        {
            if (!Admit()) { break; }

            var payload = Deserialize<PayloadState>(row);
            if (payload.Pins > 0 && payload.PinOwner == this.instance) { continue; }

            if (payload.State == "staging" && payload.Owner == this.instance) { continue; }

            var store = this.Required<StoreState>(db, "state", "store");
            var state = this.Required<CollectionState>(db, "state", payload.Scope);
            if (payload.State == "staging")
            {
                store.Staging -= payload.Length; state.Staging -= payload.Length;
                store.Retired += payload.Length; state.Retired += payload.Length;
                payload.State = "retired";
            }

            while (payload.ReclaimedChunks < payload.Chunks && Admit())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var id = ChunkId(payload.Id, payload.ReclaimedChunks);
                var chunk = this.Rows(db, "chunks").FindById(id);
                var size = chunk?["Bytes"].AsBinary.LongLength ?? 0;
                this.Rows(db, "chunks").Delete(id);
                payload.ReclaimedChunks++;
                payload.ReclaimedBytes += size;
                store.Retired -= size; state.Retired -= size;
                left--; bytes += size;
            }

            if (payload.ReclaimedChunks == payload.Chunks)
            {
                var remainder = payload.Length - payload.ReclaimedBytes;
                store.Retired -= remainder; state.Retired -= remainder; bytes += remainder;
                store.Metadata -= PayloadCharge; state.Metadata -= PayloadCharge;
                this.Rows(db, "payloads").Delete(payload.Id);
                if (payload.Chunks == 0) { left--; }
            }
            else { this.SavePayload(db, payload); }

            this.SaveAccounting(db, store, payload.Scope, state);
        }

        if (Admit())
        {
            this.PurgeMetadata(db, "receipts", this.diagnostics.OperationRetention, ref left, request.NewWorkDeadline, cancellationToken);
            this.PurgeMetadata(db, "operations", this.diagnostics.OperationRetention, ref left, request.NewWorkDeadline, cancellationToken);
        }

        var hasMore = this.Rows(db, "fences").Exists(BsonExpression.Create("$.Owner != @0", this.instance)) ||
            this.Rows(db, "entries").Exists(BsonExpression.Create("$.Expires <= @0", Min(request.RetentionCutoff, now).UtcTicks)) ||
            this.Rows(db, "payloads").Exists(Query.EQ("State", "retired"));
        return new CacheMaintenanceOutcome(reclaimed, bytes, hasMore, this.Rows(db, "receipts").Count(Query.EQ("Completed", false)));
    }, cancellationToken);

    private void RecoverAuthority(LiteDatabase db, ref int left, DateTimeOffset deadline, CancellationToken token)
    {
        if (left == 0 || this.timeProvider.GetUtcNow() >= deadline) { return; }

        var fences = this.Rows(db, "fences").Query().Where(BsonExpression.Create("$.Owner != @0", this.instance)).Limit(left).ToList();
        foreach (var row in fences)
        {
            if (left == 0 || this.timeProvider.GetUtcNow() >= deadline) { break; }

            token.ThrowIfCancellationRequested();
            var capture = Deserialize<FenceState>(row);
            var keyId = LogicalKey(capture.Identity);
            var key = this.Required<KeyState>(db, "keys", keyId);
            if (--key.Captures == 0) { this.Rows(db, "keys").Delete(keyId); }
            else { this.Put(db, "keys", keyId, key); }

            this.Rows(db, "fences").Delete(row["_id"]);
            var scope = Scope(capture.Identity);
            var store = this.Required<StoreState>(db, "state", "store");
            var state = this.Required<CollectionState>(db, "state", scope);
            store.Metadata -= FenceCharge; state.Metadata -= FenceCharge;
            this.SaveAccounting(db, store, scope, state);
            left--;
        }

        if (left == 0 || this.timeProvider.GetUtcNow() >= deadline) { return; }

        var receipts = this.Rows(db, "receipts").Query().Where(BsonExpression.Create("$.Owner != @0 AND $.Completed = false", this.instance)).Limit(left).ToList();
        foreach (var row in receipts)
        {
            if (left == 0 || this.timeProvider.GetUtcNow() >= deadline) { break; }

            token.ThrowIfCancellationRequested();
            var receipt = Deserialize<WriteReceipt>(row);
            receipt.Completed = true; receipt.Outcome = new(CacheStorageOutcome.Failed); receipt.UpdatedAt = this.timeProvider.GetUtcNow();
            this.SaveReceipt(db, row["_id"].AsString, receipt);
            left--;
        }
    }

    private void PurgeMetadata(LiteDatabase db, string suffix, TimeSpan retention, ref int left, DateTimeOffset deadline, CancellationToken token)
    {
        if (left == 0 || this.timeProvider.GetUtcNow() >= deadline) { return; }

        var rows = this.Rows(db, suffix).Query().Where(BsonExpression.Create("$.Terminal = true AND $.Sequence <= @0", this.timeProvider.GetUtcNow().Subtract(retention).UtcTicks))
            .OrderBy("$.Sequence").Limit(left).ToList();
        foreach (var row in rows)
        {
            if (left == 0 || this.timeProvider.GetUtcNow() >= deadline) { break; }

            token.ThrowIfCancellationRequested();
            var store = this.Required<StoreState>(db, "state", "store");
            var scope = row["Scope"].AsString;
            var state = this.Required<CollectionState>(db, "state", scope);
            var charge = suffix == "operations" ? OperationCharge : Deserialize<WriteReceipt>(row).Charge;
            store.Metadata -= charge; state.Metadata -= charge;
            this.Rows(db, suffix).Delete(row["_id"]);
            this.SaveAccounting(db, store, scope, state);
            left--;
        }
    }

    private BsonValue[] AllowedScopes() => this.definitions.Values.Select(d => new BsonValue(Scope(new(d.Namespace, d.Collection, d.ContractVersion, "catalog")))).Distinct().ToArray();
    private bool AllowedScope(string scope) => this.AllowedScopes().Any(value => value.AsString == scope);

    private static bool SameOperation(CacheProviderOperation left, CacheProviderOperation right) => left.Collection == right.Collection && left.Cutoff == right.Cutoff &&
        left.Request.Namespace == right.Request.Namespace && left.Request.Collection == right.Request.Collection && left.Request.Clear == right.Request.Clear &&
        (left.Request.Clear || JsonSerializer.Serialize(left.Request.Selection) == JsonSerializer.Serialize(right.Request.Selection));

    private static bool Transition(CacheOperationState previous, CacheOperationState next) => previous == next || previous switch
    {
        CacheOperationState.Pending => next is CacheOperationState.Running or CacheOperationState.Paused or CacheOperationState.Cancelled or CacheOperationState.Failed,
        CacheOperationState.Running => next is CacheOperationState.Paused or CacheOperationState.Cancelled or CacheOperationState.Completed or CacheOperationState.Failed,
        CacheOperationState.Paused => next is CacheOperationState.Pending or CacheOperationState.Cancelled,
        CacheOperationState.Cancelled or CacheOperationState.Failed => next is CacheOperationState.Pending or CacheOperationState.Cancelled,
        _ => false
    };
    private static bool Terminal(CacheOperationState state) => state is CacheOperationState.Completed or CacheOperationState.Cancelled or CacheOperationState.Failed;
}
