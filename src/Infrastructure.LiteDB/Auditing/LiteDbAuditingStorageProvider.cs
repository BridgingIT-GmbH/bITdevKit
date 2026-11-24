// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Common;

/// <summary>Stores original audit documents with durable scoped query, extraction, and retention state.</summary>
/// <example><code>services.AddAuditing().WithLiteDbProvider();</code></example>
public sealed partial class LiteDbAuditingStorageProvider : IAuditingStorageProvider
{
    private readonly LiteDbFeatureDatabase database;
    private readonly LiteDbKeyedCollection<Guid, LiteDbAuditRecord> records;
    private readonly LiteDbKeyedCollection<string, LiteDbAuditState> control;
    private readonly AuditingOptions options;
    private readonly AuditIntegrityReadPolicy readPolicy;
    private readonly AuditRetentionOptions retention;
    private readonly AuditFieldCatalog fieldCatalog;
    private readonly TimeProvider clock;
    private readonly string filename;
    private readonly SemaphoreSlim appendGate = new(1, 1);
    private readonly Guid receiptEpoch = Guid.NewGuid();
    private long sequence;
    private bool initialized;

    /// <summary>Creates the native provider without opening or changing a database.</summary>
    /// <param name="database">The owned database and declared namespace.</param>
    /// <param name="options">The existing capture limits.</param>
    /// <param name="catalog">The approved query field catalog.</param>
    /// <param name="integrity">The existing source integrity policy.</param>
    /// <param name="retention">The existing lifecycle thresholds.</param>
    /// <param name="clock">The retention and receipt clock.</param>
    /// <param name="filename">The registered file path used for side-effect-free readiness probes.</param>
    public LiteDbAuditingStorageProvider(LiteDbFeatureDatabase database, AuditingOptions options, AuditFieldCatalog catalog,
        AuditIntegrityReadPolicy integrity, AuditRetentionOptions retention, TimeProvider clock, string filename)
    {
        this.database = database;
        this.options = options;
        this.fieldCatalog = catalog;
        this.readPolicy = integrity;
        this.retention = retention;
        this.clock = clock;
        this.filename = filename;
        this.records = database.Collection<Guid, LiteDbAuditRecord>("records");
        this.control = database.Collection<string, LiteDbAuditState>("state");
    }

    /// <inheritdoc />
    public AuditProviderCapabilities Capabilities => new()
    {
        Provider = "LiteDB", IsDurable = true, IsShared = false, Buffered = false,
        Visibility = "ImmediateProcessLocal", SupportsQueries = true, SupportsResumableExtraction = true, Backends = ["LiteDB"]
    };

    /// <inheritdoc />
    public AuditFlushResult DeliveryStatus
    {
        get
        {
            var through = Interlocked.Read(ref this.sequence);
            return new() { ReceiptEpoch = this.receiptEpoch, ThroughSequence = through, Persisted = through };
        }
    }

    /// <summary>Initializes native collections and durable control state without relational migrations.</summary>
    /// <param name="cancellationToken">Cancels initialization.</param>
    /// <returns>The committed initialization result.</returns>
    public async Task<Result> InitializeAsync(CancellationToken cancellationToken = default)
    {
        var result = await this.RunAsync(() =>
        {
            _ = this.Control;
            _ = this.database.Query<LiteDbAuditRecord>("records", null, 1, "$.Position");
            return Task.FromResult(Result.Success());
        }, failure => Result.Failure().WithErrors(failure.Errors), cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess) { this.initialized = true; }

        return result;
    }

    /// <inheritdoc />
    public async Task<Result<AuditProviderCapabilities>> CheckReadinessAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!this.initialized && !File.Exists(this.filename)) { return Result<AuditProviderCapabilities>.Failure(new AuditError("AuditStoreNotReady")); }

        try
        {
            var ready = await this.database.InspectAsync(db =>
            {
                var prefix = this.database.Options.CollectionPrefix;
                if (!db.CollectionExists(prefix + "_state") || !db.CollectionExists(prefix + "_records")) { return false; }

                var row = db.GetCollection<BsonDocument>(prefix + "_state").FindOne(BsonExpression.Create("$.StoreId != @0 AND $.StoreEpoch != @0", Guid.Empty));
                return row is not null && row["Position"].AsInt64 >= 0;
            }, cancellationToken).ConfigureAwait(false);
            return ready ? Result<AuditProviderCapabilities>.Success(this.Capabilities) : Result<AuditProviderCapabilities>.Failure(new AuditError("AuditStoreNotReady"));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { return Result<AuditProviderCapabilities>.Failure(new AuditError("AuditStorageUnavailable")); }
    }

    /// <inheritdoc />
    public async Task<Result<AuditAppendReceipt>> AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var frozen = AuditRecordCodec.Freeze(record);
        if (frozen.IsFailure) { return Result<AuditAppendReceipt>.Failure(frozen); }

        var bytes = AuditRecordCodec.Encode(frozen.Value);
        if (bytes.Length > this.options.Limits.MaxRecordBytes) { return Result<AuditAppendReceipt>.Failure(new AuditError("AuditRecordTooLarge")); }

        await this.appendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (this.readPolicy.IsConfigured)
            {
                var verified = await this.readPolicy.VerifyOriginalAsync(frozen.Value, cancellationToken).ConfigureAwait(false);
                if (verified.IsFailure) { return Result<AuditAppendReceipt>.Failure(verified); }

                if (verified.Value != "Verified") { return Result<AuditAppendReceipt>.Failure(new AuditError("AuditIntegrityMissingProtection")); }
            }

            var probe = await this.FindAsync(record.AuditId, cancellationToken).ConfigureAwait(false);
            byte[] original = null;
            if (probe.IsSuccess)
            {
                var verified = await this.readPolicy.VerifyOriginalAsync(probe.Value, cancellationToken).ConfigureAwait(false);
                if (verified.IsFailure) { return Result<AuditAppendReceipt>.Failure(verified); }

                if (verified.Value is not ("Verified" or "NotProtected")) { return Result<AuditAppendReceipt>.Failure(new AuditError("AuditIntegrityInvalid")); }

                original = AuditRecordCodec.Encode(probe.Value);
            }
            else if (!probe.Errors.OfType<AuditError>().Any(error => error.Code == "AuditNotFound")) { return Result<AuditAppendReceipt>.Failure(probe); }

            var stored = await this.RunAsync(() =>
            {
                if (this.records.TryGetValue(record.AuditId, out var existing))
                {
                    if (existing.Record.ContentHash != frozen.Value.ContentHash) { return Task.FromResult(Result<string>.Failure(new AuditError("AuditIdentityConflict"))); }

                    if (original is null || !existing.Content.AsSpan().SequenceEqual(original)) { return Task.FromResult(Result<string>.Failure(new AuditError("AuditStoreChanged"))); }

                    return Task.FromResult(Result<string>.Success("AlreadyPresent"));
                }

                var state = this.Control;
                var now = this.clock.GetUtcNow();
                var reject = this.retention.RemoveAfter is TimeSpan remove ? Math.Max(state.RejectCompletedBefore, (now - remove).UtcTicks) : state.RejectCompletedBefore;
                var archive = this.retention.ArchiveAfter is TimeSpan age ? Math.Max(state.ArchiveCompletedBefore, (now - age).UtcTicks) : state.ArchiveCompletedBefore;
                if (frozen.Value.CompletedUtc.UtcTicks < reject) { return Task.FromResult(Result<string>.Failure(new AuditError("AuditEvidenceExpired"))); }

                this.records.Add(record.AuditId, new() { Content = bytes, Position = checked(++state.PublishedPosition), Archived = frozen.Value.CompletedUtc.UtcTicks < archive });
                return Task.FromResult(Result<string>.Success("Persisted"));
            }, failure => Result<string>.Failure(failure), cancellationToken).ConfigureAwait(false);
            if (stored.IsFailure) { return Result<AuditAppendReceipt>.Failure(stored); }

            return Result<AuditAppendReceipt>.Success(new() { AuditId = record.AuditId, ReceiptEpoch = this.receiptEpoch, AcceptanceSequence = Interlocked.Increment(ref this.sequence), State = stored.Value });
        }
        finally { this.appendGate.Release(); }
    }

    /// <inheritdoc />
    public Task<Result<AuditRecord>> FindAsync(Guid auditId, CancellationToken cancellationToken = default) => this.FindAsync(auditId, new AuditPredicate(), cancellationToken);

    /// <inheritdoc />
    public Task<Result<AuditFlushResult>> FlushAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var through = Interlocked.Read(ref this.sequence);
        return Task.FromResult(Result<AuditFlushResult>.Success(new() { ReceiptEpoch = this.receiptEpoch, ThroughSequence = through, Persisted = through }));
    }

    /// <summary>Rotates a known position epoch after an operator-controlled restore.</summary>
    /// <param name="expectedEpoch">The current expected source epoch.</param>
    /// <param name="cancellationToken">Cancels the atomic epoch update.</param>
    /// <returns>The replacement epoch, or a typed stale-epoch failure.</returns>
    public Task<Result<Guid>> RotateStoreEpochAsync(Guid expectedEpoch, CancellationToken cancellationToken = default) => this.RunAsync(() =>
    {
        var state = this.Control;
        if (expectedEpoch == Guid.Empty || state.StoreEpoch != expectedEpoch) { return Task.FromResult(Result<Guid>.Failure(new AuditError("AuditEpochChanged"))); }

        state.StoreEpoch = Guid.NewGuid();
        return Task.FromResult(Result<Guid>.Success(state.StoreEpoch));
    }, failure => Result<Guid>.Failure(failure), cancellationToken);

    private LiteDbAuditState Control => this.control.GetOrAdd("current", _ => new());

    private async Task<T> RunAsync<T>(Func<Task<T>> action, Func<IResult, T> failure, CancellationToken cancellationToken) where T : IResult
    {
        try { return await this.database.ExecuteAsync(action, cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) { throw; }
        catch (ArgumentException) { return failure(Result.Failure(new AuditError("AuditQueryInvalid"))); }
        catch (LiteDbProviderException error) { return failure(Result.Failure().WithErrors(error.Errors)); }
        catch (Exception) { return failure(Result.Failure(new AuditError("AuditStorageUnavailable"))); }
    }
}
