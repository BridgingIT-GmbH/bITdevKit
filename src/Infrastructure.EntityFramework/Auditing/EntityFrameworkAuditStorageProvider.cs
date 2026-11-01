// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Auditing;

using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Owns durable independent audit transactions, explicit delivery receipts and committed extraction metadata.</summary>
/// <example><code>auditing.WithEntityFrameworkProvider&lt;CustomerDbContext&gt;();</code></example>
public sealed partial class EntityFrameworkAuditStorageProvider<TContext> : IAuditingStorageProvider where TContext : DbContext, IAuditingDbContext
{
    private readonly AuditEntityFrameworkUnitOfWork<TContext> work;
    private readonly AuditEntityFrameworkOptions storage;
    private readonly AuditingOptions capture;
    private readonly AuditFieldCatalog catalog;
    private readonly AuditIntegrityReadPolicy integrity;
    private readonly AuditRetentionOptions retention;
    private readonly TimeProvider time;
    private readonly Guid receiptEpoch = Guid.NewGuid();
    private long confirmedSequence;
    private readonly AuditWriteQueue queue;

    /// <summary>Creates the provider without opening a database, migrating, seeding, or selecting a fallback.</summary>
    /// <example><code>var provider = new EntityFrameworkAuditStorageProvider&lt;Store&gt;(scopes, storage, capture, catalog, integrity, retention, time);</code></example>
    public EntityFrameworkAuditStorageProvider(IServiceScopeFactory scopes, AuditEntityFrameworkOptions storage, AuditingOptions capture, AuditFieldCatalog catalog,
        AuditIntegrityReadPolicy integrity, AuditRetentionOptions retention, TimeProvider time)
    {
        storage.Validate();
        this.storage = storage;
        this.capture = capture;
        this.catalog = catalog;
        this.integrity = integrity;
        this.retention = retention;
        this.time = time;
        this.work = new(scopes, storage.OperationTimeout);
        if (storage.Buffered) this.queue = new(this.AppendInlineAsync, storage, time, (id, reason) =>
        {
            using var scope = scopes.CreateScope();
            scope.ServiceProvider.GetRequiredService<AuditRuntime>().Report(reason);
        });
    }

    /// <summary>Declares actual configured persistence and visibility guarantees, independent of individual source verification.</summary>
    /// <example><code>var capabilities = provider.Capabilities;</code></example>
    public AuditProviderCapabilities Capabilities => new()
    {
        Provider = "EntityFramework", CoordinatesShutdown = true, IsDurable = true, IsShared = true, Buffered = this.storage.Buffered,
        Visibility = this.storage.Buffered ? "EventualShared" : "ImmediateShared",
        Backends = ["SqlServer", "Postgres", "Sqlite"], SupportsQueries = true, SupportsResumableExtraction = true
    };

    /// <summary>Validates durable schema/control state through a read-only startup or health-check operation.</summary>
    /// <example><code>var ready = await provider.CheckReadinessAsync(cancellationToken);</code></example>
    public Task<Result<AuditProviderCapabilities>> CheckReadinessAsync(CancellationToken cancellationToken = default) => this.work.ExecuteAsync(
        async (context, session, token) =>
        {
            // Validate every mapped scalar column, including child tables, without loading evidence.
            await context.AuditRecords.IgnoreQueryFilters().AsNoTracking().Where(_ => false).ToListAsync(token).ConfigureAwait(false);
            await context.AuditActions.IgnoreQueryFilters().AsNoTracking().Where(_ => false).ToListAsync(token).ConfigureAwait(false);
            await context.AuditEntityChanges.IgnoreQueryFilters().AsNoTracking().Where(_ => false).ToListAsync(token).ConfigureAwait(false);
            return Result<AuditProviderCapabilities>.Success(this.Capabilities with { Backends = [session.Sql.Backend] });
        }, false, cancellationToken);

    /// <summary>Invalidates extraction checkpoints after an operator-controlled restore changes position meaning.</summary>
    /// <remarks>Quiesce producers and drain accepted writes before restore and epoch rotation. This does not detect or perform a database restore.</remarks>
    /// <example><code>var epoch = await provider.RotateStoreEpochAsync(expectedEpoch, cancellationToken);</code></example>
    public Task<Result<Guid>> RotateStoreEpochAsync(Guid expectedEpoch, CancellationToken cancellationToken = default) => this.work.ExecuteAsync(
        (context, session, token) =>
        {
            if (expectedEpoch == Guid.Empty || session.State.StoreEpoch != expectedEpoch) return Task.FromResult(Result<Guid>.Failure(new AuditError("AuditEpochChanged")));
            session.State.StoreEpoch = Guid.NewGuid();
            return Task.FromResult(Result<Guid>.Success(session.State.StoreEpoch));
        }, true, cancellationToken);

    /// <inheritdoc />
    public async Task<Result<AuditAppendReceipt>> AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        if (this.queue is null) return await this.AppendInlineAsync(record, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var frozen = AuditRecordCodec.Freeze(record);
        if (frozen.IsFailure) return Result<AuditAppendReceipt>.Failure(frozen);
        if (AuditRecordCodec.Encode(frozen.Value).Length > this.capture.Limits.MaxRecordBytes) return Result<AuditAppendReceipt>.Failure(new AuditError("AuditRecordTooLarge"));
        if (this.integrity.IsConfigured)
        {
            var verified = await this.integrity.VerifyOriginalAsync(frozen.Value, cancellationToken).ConfigureAwait(false);
            if (verified.IsFailure) return Result<AuditAppendReceipt>.Failure(verified);
            if (verified.Value != "Verified") return Result<AuditAppendReceipt>.Failure(new AuditError("AuditIntegrityMissingProtection"));
        }

        cancellationToken.ThrowIfCancellationRequested();
        return this.queue.TryAppend(frozen.Value);
    }

    /// <summary>Runs the provider-owned writer; inline mode needs no background loop.</summary>
    /// <example><code>await provider.RunWriterAsync(stoppingToken);</code></example>
    public Task RunWriterAsync(CancellationToken stoppingToken) => this.queue?.RunAsync(stoppingToken) ?? Task.CompletedTask;

    /// <summary>Closes queue admissions only after real root owners have finalized.</summary>
    /// <example><code>provider.CloseWriterAdmissions();</code></example>
    public void CloseWriterAdmissions() => this.queue?.CloseAdmissions();

    /// <summary>Copies process-local delivery counts without claiming global producer completion.</summary>
    /// <example><code>var delivery = provider.DeliveryStatus;</code></example>
    public AuditFlushResult DeliveryStatus => this.queue?.Snapshot() ?? new() { ReceiptEpoch = this.receiptEpoch, ThroughSequence = Interlocked.Read(ref this.confirmedSequence), Persisted = Interlocked.Read(ref this.confirmedSequence) };

    /// <summary>Persists one complete frozen graph; the buffered writer also uses this exact durable path.</summary>
    /// <example><code>var confirmed = await provider.AppendInlineAsync(sealedRecord, cancellationToken);</code></example>
    public async Task<Result<AuditAppendReceipt>> AppendInlineAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var frozen = AuditRecordCodec.Freeze(record);
        if (frozen.IsFailure) return Result<AuditAppendReceipt>.Failure(frozen);
        record = frozen.Value;
        if (AuditRecordCodec.Encode(record).Length > this.capture.Limits.MaxRecordBytes) return Result<AuditAppendReceipt>.Failure(new AuditError("AuditRecordTooLarge"));
        if (this.integrity.IsConfigured)
        {
            var incoming = await this.integrity.VerifyOriginalAsync(record, cancellationToken).ConfigureAwait(false);
            if (incoming.IsFailure) return Result<AuditAppendReceipt>.Failure(incoming);
            if (incoming.Value != "Verified") return Result<AuditAppendReceipt>.Failure(new AuditError("AuditIntegrityMissingProtection"));
        }

        var mapped = AuditEntityMapper.ToEntity(record);
        if (mapped.IsFailure) return Result<AuditAppendReceipt>.Failure(mapped);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            byte[] verifiedExisting = null;
            var probe = await this.FindAsync(record.AuditId, cancellationToken).ConfigureAwait(false);
            if (probe.IsSuccess)
            {
                if (probe.Value.ContentHash != record.ContentHash) return Result<AuditAppendReceipt>.Failure(new AuditError("AuditIdentityConflict"));
                var verified = await this.integrity.VerifyOriginalAsync(probe.Value, cancellationToken).ConfigureAwait(false);
                if (verified.IsFailure) return Result<AuditAppendReceipt>.Failure(verified);
                if (verified.Value is not ("Verified" or "NotProtected")) return Result<AuditAppendReceipt>.Failure(new AuditError("AuditIntegrityInvalid"));
                verifiedExisting = AuditRecordCodec.Encode(probe.Value);
            }
            else if (!probe.Errors.OfType<AuditError>().Any(error => error.Code == "AuditNotFound")) return Result<AuditAppendReceipt>.Failure(probe);

            var result = await this.work.ExecuteAsync(async (context, session, token) =>
            {
                var existing = await context.AuditRecords.IgnoreQueryFilters().AsNoTracking().AnyAsync(row => row.AuditId == record.AuditId, token).ConfigureAwait(false);
                if (existing)
                {
                    if (verifiedExisting is null) return Result<string>.Failure(new AuditError("AuditDuplicateNeedsVerification"));
                    var current = await AuditGraphReader.ReadAsync(session, record.AuditId, 16_777_216, token).ConfigureAwait(false);
                    if (current.IsFailure) return Result<string>.Failure(current);
                    if (current.Value.ContentHash != record.ContentHash) return Result<string>.Failure(new AuditError("AuditIdentityConflict"));
                    if (!AuditRecordCodec.Encode(current.Value).AsSpan().SequenceEqual(verifiedExisting)) return Result<string>.Failure(new AuditError("AuditDuplicateNeedsVerification"));
                    return Result<string>.Success("AlreadyPresent");
                }

                var now = this.time.GetUtcNow();
                var state = session.State;
                var rejectBefore = this.retention.RemoveAfter is TimeSpan remove ? Math.Max(state.RejectCompletedBeforeUtcTicks, (now - remove).UtcTicks) : state.RejectCompletedBeforeUtcTicks;
                var archiveBefore = this.retention.ArchiveAfter is TimeSpan archive ? Math.Max(state.ArchiveCompletedBeforeUtcTicks, (now - archive).UtcTicks) : state.ArchiveCompletedBeforeUtcTicks;
                if (record.CompletedUtc.UtcTicks < rejectBefore) return Result<string>.Failure(new AuditError("AuditEvidenceExpired"));
                var graph = mapped.Value;
                graph.Position = checked(++state.PublishedPosition);
                graph.StorageState = record.CompletedUtc.UtcTicks < archiveBefore ? AuditStorageState.Archived : AuditStorageState.Active;
                graph.ArchivedUtcTicks = graph.StorageState == AuditStorageState.Archived ? now.UtcTicks : null;
                context.AuditRecords.Add(graph);
                return Result<string>.Success("Persisted");
            }, true, cancellationToken).ConfigureAwait(false);
            if (result.IsSuccess) return Result<AuditAppendReceipt>.Success(new()
            {
                AuditId = record.AuditId, ReceiptEpoch = this.receiptEpoch, AcceptanceSequence = Interlocked.Increment(ref this.confirmedSequence), State = result.Value
            });
            if (!result.Errors.OfType<AuditError>().Any(error => error.Code == "AuditDuplicateNeedsVerification")) return Result<AuditAppendReceipt>.Failure(result);
        }

        return Result<AuditAppendReceipt>.Failure(new AuditError("AuditStoreChanged"));
    }

    /// <inheritdoc />
    public async Task<Result<AuditRecord>> FindAsync(Guid auditId, CancellationToken cancellationToken = default)
    {
        var stored = await this.FindStoredAsync(auditId, new(), cancellationToken).ConfigureAwait(false);
        return stored.IsSuccess ? Result<AuditRecord>.Success(stored.Value.Record) : Result<AuditRecord>.Failure(stored);
    }

    /// <inheritdoc />
    public async Task<Result<AuditRecord>> FindAsync(Guid auditId, AuditPredicate scope, CancellationToken cancellationToken = default)
    {
        var stored = await this.FindStoredAsync(auditId, scope, cancellationToken).ConfigureAwait(false);
        return stored.IsSuccess ? Result<AuditRecord>.Success(stored.Value.Record) : Result<AuditRecord>.Failure(stored);
    }

    /// <inheritdoc />
    public Task<Result<AuditExtractionItem>> FindStoredAsync(Guid auditId, AuditPredicate scope, CancellationToken cancellationToken = default) => this.work.ExecuteAsync(async (context, session, token) =>
    {
        var sql = new AuditQuerySql(session.Sql, this.catalog);
        var condition = sql.Predicate(scope);
        var id = sql.Parameter(auditId);
        var identity = session.Sql.Column<AuditRecordEntity>("r", nameof(AuditRecordEntity.AuditId));
        var stateColumn = session.Sql.Column<AuditRecordEntity>("r", nameof(AuditRecordEntity.StorageState));
        var positionColumn = session.Sql.Column<AuditRecordEntity>("r", nameof(AuditRecordEntity.Position));
        long position;
        int state;
        await using (var command = session.Sql.Command("SELECT " + positionColumn + "," + stateColumn + " FROM " + session.Sql.Table<AuditRecordEntity>() + " r WHERE " + identity + "=" + id + " AND (" + condition + ")", sql.Values))
        await using (var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
        {
            if (!await reader.ReadAsync(token).ConfigureAwait(false)) return Result<AuditExtractionItem>.Failure(new AuditError("AuditNotFound"));
            position = reader.GetInt64(0);
            state = reader.GetInt32(1);
        }

        var original = await AuditGraphReader.ReadAsync(session, auditId, 16_777_216, token).ConfigureAwait(false);
        return original.IsSuccess ? Result<AuditExtractionItem>.Success(new() { Record = original.Value, Position = position, StorageState = ((AuditStorageState)state).ToString() }) : Result<AuditExtractionItem>.Failure(original);
    }, false, cancellationToken);

    /// <inheritdoc />
    public Task<Result<AuditFlushResult>> FlushAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (this.queue is not null) return this.queue.FlushAsync(cancellationToken);
        var through = Interlocked.Read(ref this.confirmedSequence);
        return Task.FromResult(Result<AuditFlushResult>.Success(new() { ReceiptEpoch = this.receiptEpoch, ThroughSequence = through, Persisted = through }));
    }
}
