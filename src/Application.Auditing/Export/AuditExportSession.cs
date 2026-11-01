// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using BridgingIT.DevKit.Common;

/// <summary>Owns one bounded whole-record extraction and reconciles prospective progress with actual flushed output.</summary>
/// <example><code>await using var session = opened.Value; var completion = await session.WriteAsync(destination, cancellationToken);</code></example>
public sealed class AuditExportSession : IAsyncDisposable
{
    private readonly IAuditContext audit;
    private readonly IAuditingStorageProvider provider;
    private readonly AuditIntegrityReadPolicy integrity;
    private readonly AuditReadProjection projection;
    private readonly TimeProvider time;
    private readonly IDisposable admission;
    private readonly Func<CancellationToken, Task<Result>> validateCurrent;
    private readonly CancellationTokenSource deadline;
    private readonly long started;
    private readonly TaskCompletionSource sourceExited = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly long windowLower;
    private AuditExtractionCursor cursor;
    private AuditExportCompletion candidate;
    private readonly object lifecycle = new();
    private Task<Result<AuditExportCompletion>> confirming;
    private int enumerated;
    private int disposed;
    private int released;

    /// <summary>Creates an already authorized attempt over a validated fixed window. Applications normally use IAuditExportService.</summary>
    /// <example><code>await using var session = (await exports.OpenAsync(request)).Value;</code></example>
    public AuditExportSession(IAuditContext audit, IAuditingStorageProvider provider, IAuditExportFormatter formatter, AuditExportOptions options,
        AuditExtractionCursor cursor, AuditExportMetadata metadata, AuditIntegrityReadPolicy integrity, AuditReadProjection projection,
        TimeProvider time, IDisposable admission, Func<CancellationToken, Task<Result>> validateCurrent, long? startedTimestamp = null)
    {
        this.audit = audit; this.provider = provider; this.Formatter = formatter; this.Options = options; this.cursor = cursor; this.Metadata = metadata;
        this.integrity = integrity; this.projection = projection; this.time = time; this.admission = admission; this.validateCurrent = validateCurrent;
        this.windowLower = cursor.AfterPosition;
        this.started = startedTimestamp ?? time.GetTimestamp();
        this.deadline = new(this.Remaining > TimeSpan.Zero ? this.Remaining : TimeSpan.FromTicks(1), time);
        this.deadline.Token.Register(() => { if (Volatile.Read(ref this.enumerated) == 0) _ = this.DisposeAsync(); });
    }

    /// <summary>Bounded safe attempt identity and source selection metadata.</summary>
    /// <example><code>var id = session.Metadata.ExportId;</code></example>
    public AuditExportMetadata Metadata { get; }

    /// <summary>Selected pinned formatter.</summary>
    /// <example><code>var extension = session.Formatter.Extension;</code></example>
    public IAuditExportFormatter Formatter { get; }

    /// <summary>Captured immutable limits for output and cleanup.</summary>
    /// <example><code>var budget = session.Options.ResponseByteLimit;</code></example>
    public AuditExportOptions Options { get; }

    /// <summary>Candidate terminal facts. They remain unconfirmed until actual output and current-source checks pass.</summary>
    /// <example><code>var prospectiveCount = session.CandidateCompletion?.SourceRecordCount;</code></example>
    public AuditExportCompletion CandidateCompletion => this.candidate;

    /// <summary>The remaining monotonic output deadline, never extended by per-record activity.</summary>
    /// <example><code>var remaining = session.Remaining;</code></example>
    public TimeSpan Remaining => this.Options.MaxDuration - this.time.GetElapsedTime(this.started);

    /// <summary>Supplies owned exact frames once, retaining at most one original source graph and one formatted expansion.</summary>
    /// <example><code>await foreach (var frame in session.ReadFramesAsync(cancellationToken)) { using (frame) await output.WriteAsync(frame.Bytes, cancellationToken); }</code></example>
    public async IAsyncEnumerable<AuditExportFrame> ReadFramesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        lock (this.lifecycle)
        {
            if (this.enumerated != 0 || this.disposed != 0) throw new InvalidOperationException("An export attempt can be enumerated once.");
            this.enumerated = 1;
        }

        using var suppression = this.audit.Suppress();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, this.deadline.Token);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var context = new AuditExportFormatContext { GraphByteLimit = this.Options.GraphByteLimit, MetadataByteLimit = this.Options.MetadataByteReserve, CompletionByteLimit = this.Options.CompletionByteReserve };
        AuditExportFrame pending = null;
        long responses = 0, frames = 0, records = 0, items = 0, data = 0, recordBytes = 0;
        var verification = new Dictionary<string, long>(StringComparer.Ordinal);
        var completeness = new Dictionary<string, long>(StringComparer.Ordinal);
        var transformations = new Dictionary<string, long>(StringComparer.Ordinal);
        var exhausted = false;
        string limit = null;
        void Account(AuditExportFrame frame)
        {
            responses = checked(responses + frame.Bytes.Length); frames++;
            if (responses > this.Options.ResponseByteLimit) throw new AuditExportException("AuditExportRecordTooLarge");
            if (frame.IsData) { hash.AppendData(frame.Bytes.Span); data += frame.Bytes.Length; }

            items += frame.ItemCount;
            foreach (var fact in frame.Transformations) Add(transformations, fact.Key, fact.Value);
        }

        try
        {
            pending = await this.Formatter.StartAsync(this.Metadata, context, linked.Token).ConfigureAwait(false);
            if (pending is not null)
            {
                if (pending.Bytes.Length > this.Options.MetadataByteReserve) throw new AuditExportException("AuditExportMetadataTooLarge");
                Account(pending);
                var emitted = pending; pending = null;
                yield return emitted;
            }

            while (true)
            {
                linked.Token.ThrowIfCancellationRequested();
                if (this.Remaining <= TimeSpan.FromMilliseconds(100)) { limit = "DurationLimit"; break; }

                var batch = await this.ReadAsync(this.cursor, linked.Token).ConfigureAwait(false);
                if (batch is null || batch.Cursor is null || batch.Items.Length > 1 || batch.Cursor.StoreId != this.cursor.StoreId || batch.Cursor.StoreEpoch != this.cursor.StoreEpoch
                    || batch.Cursor.UpperPosition != this.cursor.UpperPosition || batch.Cursor.AvailabilityBaseline != this.cursor.AvailabilityBaseline || batch.Cursor.LifecycleGeneration != this.cursor.LifecycleGeneration
                    || batch.Cursor.Selection?.Fingerprint != this.cursor.Selection.Fingerprint || batch.Cursor.AfterPosition < this.cursor.AfterPosition || batch.Cursor.AfterPosition > this.cursor.UpperPosition
                    || (batch.Exhausted && batch.Cursor.AfterPosition != this.cursor.UpperPosition)) throw new AuditExportException("AuditExportProviderProtocolInvalid");
                if (batch.Items.IsEmpty)
                {
                    if (!batch.Exhausted || batch.Cursor.AfterPosition != this.cursor.UpperPosition) throw new AuditExportException("AuditExportProviderProtocolInvalid");
                    this.cursor = batch.Cursor; exhausted = true; break;
                }

                if (records >= this.Options.RecordLimit) { limit = "RecordLimit"; break; }

                var source = batch.Items[0];
                if (source?.Record is null || source.Record.AuditId == Guid.Empty || source.Position <= this.cursor.AfterPosition || source.Position > batch.Cursor.AfterPosition) throw new AuditExportException("AuditExportProviderProtocolInvalid");
                var checkedSource = await this.integrity.VerifyOriginalAsync(source.Record, linked.Token).ConfigureAwait(false);
                if (checkedSource.IsFailure || checkedSource.Value is not ("Verified" or "NotProtected")) throw new AuditExportException("AuditIntegrityInvalid");
                var approved = this.projection.Apply(source, checkedSource.Value);
                // A source hash is retained only as an explicitly named original-document reference.
                approved = approved with { Record = approved.Record with { ContentHash = null, IntegrityProtection = null } };
                pending = await this.Formatter.RecordAsync(new() { SourceStoreId = this.cursor.StoreId, Position = this.Metadata.PositionsExposed ? source.Position : null, OriginalContentHash = source.Record.ContentHash, Details = approved }, context, linked.Token).ConfigureAwait(false);
                if (pending is null || pending.Bytes.Length == 0 || !pending.IsData || pending.Bytes.Length > this.Options.GraphByteLimit) throw new AuditExportException("AuditExportRecordTooLarge");
                var usable = this.Options.ResponseByteLimit - this.Options.MetadataByteReserve - this.Options.CompletionByteReserve;
                if (pending.Bytes.Length > usable - recordBytes)
                {
                    if (records == 0) throw new AuditExportException("AuditExportRecordTooLarge");
                    pending.Dispose(); pending = null; limit = "ByteLimit"; break;
                }

                linked.Token.ThrowIfCancellationRequested();
                recordBytes += pending.Bytes.Length;
                Account(pending); records++;
                Add(verification, checkedSource.Value, 1); Add(completeness, source.Record.Completeness.ToString(), 1);
                if (approved.IsTransformed) Add(transformations, "ExposureTransformed", 1);
                this.cursor = batch.Cursor;
                exhausted = batch.Exhausted;
                var emitted = pending; pending = null;
                batch = null; source = null; approved = null;
                yield return emitted;
                if (exhausted) break;
            }

            var valid = await this.ValidateTerminalAsync(linked.Token).ConfigureAwait(false);
            if (valid.IsFailure) throw new AuditExportException("AuditExportWindowInvalid");
            var finished = this.time.GetUtcNow();
            var resume = new AuditExportResume
            {
                ChunkLowerPosition = this.windowLower, StoreId = this.cursor.StoreId, StoreEpoch = this.cursor.StoreEpoch, AfterPosition = this.cursor.AfterPosition, UpperPosition = this.cursor.UpperPosition,
                LifecycleGeneration = this.cursor.LifecycleGeneration, AvailabilityBaseline = this.cursor.AvailabilityBaseline,
                SelectionFingerprint = this.Metadata.SelectionFingerprint, ScopeFingerprint = this.Metadata.ScopeFingerprint, ExposureFingerprint = this.Metadata.ExposureFingerprint,
                Format = this.Formatter.Format, Profile = this.Formatter.Profile, AsOfUtc = this.Metadata.AsOfUtc, ExpiresUtc = finished + this.Options.TokenLifetime, IsCheckpoint = exhausted
            };
            this.candidate = new()
            {
                Metadata = this.Metadata, Status = exhausted ? "Completed" : "Partial", ReasonCode = limit, FinishedUtc = finished,
                LastPosition = this.Metadata.PositionsExposed ? this.cursor.AfterPosition : null, WindowExhausted = exhausted, SourceRecordCount = records, OutputItemCount = items,
                DataByteCount = data, Sha256 = Convert.ToHexStringLower(hash.GetHashAndReset()), FrameCount = frames + (this.Formatter.Format == "ndjson" ? 1 : 0),
                VerificationCounts = verification.ToImmutableDictionary(), CompletenessCounts = completeness.ToImmutableDictionary(), Transformations = transformations.ToImmutableDictionary(), Progress = resume
            };
            pending = await this.Formatter.EndAsync(this.candidate, context, linked.Token).ConfigureAwait(false);
            if (pending is not null)
            {
                if (pending.IsData || pending.Bytes.Length > this.Options.CompletionByteReserve) throw new AuditExportException("AuditExportMetadataTooLarge");
                Account(pending);
                var emitted = pending; pending = null;
                this.candidate = this.candidate with { ResponseByteCount = responses, FrameCount = frames };
                yield return emitted;
            }
            else this.candidate = this.candidate with { ResponseByteCount = responses, FrameCount = frames };
        }
        finally
        {
            pending?.Dispose();
            this.sourceExited.TrySetResult();
            if (Volatile.Read(ref this.disposed) != 0) this.ReleaseWhenSettled();
        }
    }

    /// <summary>Revalidates current scope and provider lifecycle without reconstructing another graph or advancing progress.</summary>
    /// <example><code>var valid = await session.ValidateTerminalAsync(cancellationToken);</code></example>
    public async Task<Result> ValidateTerminalAsync(CancellationToken cancellationToken = default, bool afterOutput = false)
    {
        using var suppression = this.audit.Suppress();
        var current = await this.validateCurrent(cancellationToken).ConfigureAwait(false);
        if (current.IsFailure) return current;
        var validation = await this.ReadAsync(this.cursor with { AfterPosition = this.cursor.UpperPosition, RequiredAfterPosition = this.windowLower }, cancellationToken, !afterOutput).ConfigureAwait(false);
        return validation.Exhausted && validation.Items.IsEmpty ? Result.Success() : Result.Failure(new AuditError("AuditExportProviderProtocolInvalid"));
    }

    /// <summary>Confirms only a completely flushed expected prefix, successful cleanup and current source checks.</summary>
    /// <example><code>var confirmed = await session.ConfirmAsync(outcome, cancellationToken);</code></example>
    public Task<Result<AuditExportCompletion>> ConfirmAsync(NdjsonStreamOutcome outcome, CancellationToken cancellationToken = default)
    {
        lock (this.lifecycle)
        {
            if (this.disposed != 0) return Task.FromResult(this.Unconfirmed("Failed", "AuditExportDisposed"));
            return this.confirming ??= this.ConfirmCoreAsync(outcome, cancellationToken);
        }
    }

    private async Task<Result<AuditExportCompletion>> ConfirmCoreAsync(NdjsonStreamOutcome outcome, CancellationToken cancellationToken)
    {
        if (this.candidate is null || outcome.Status != NdjsonStreamStatus.Succeeded || !outcome.SourceCompleted || !outcome.CleanupCompleted || outcome.PartialWritePossible
            || outcome.FlushedRecordCount != this.candidate.FrameCount || outcome.WrittenRecordCount != this.candidate.FrameCount
            || outcome.FlushedFramedByteCount != this.candidate.ResponseByteCount || outcome.WrittenFramedByteCount != this.candidate.ResponseByteCount)
            return this.Unconfirmed(outcome.Status == NdjsonStreamStatus.Canceled ? "Canceled" : "Failed", outcome.ReasonCode ?? "AuditExportOutputUnconfirmed");
        try
        {
            var validation = await this.ValidateTerminalAsync(cancellationToken, afterOutput: true).ConfigureAwait(false);
            return validation.IsSuccess ? Result<AuditExportCompletion>.Success(this.candidate with { OutputConfirmed = true, FinishedUtc = this.time.GetUtcNow() }) : this.Unconfirmed("Failed", "AuditExportWindowInvalid");
        }
        catch (OperationCanceledException) { return this.Unconfirmed("Canceled", "AuditExportConfirmationCanceled"); }
        catch (Exception) { return this.Unconfirmed("Failed", "AuditExportConfirmationUnavailable"); }
    }

    /// <summary>Writes one exact prefix with per-frame flushing and returns confirmed output metadata independently of HTTP.</summary>
    /// <example><code>var completed = await session.WriteAsync(file, cancellationToken);</code></example>
    public async Task<Result<AuditExportCompletion>> WriteAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        using var outputDeadline = new CancellationTokenSource(this.Remaining > TimeSpan.Zero ? this.Remaining : TimeSpan.FromTicks(1), this.time);
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, outputDeadline.Token);
        var operation = this.WriteCoreAsync(destination, cancellationToken);
        try { return await operation.WaitAsync(wait.Token).ConfigureAwait(false); }
        catch (OperationCanceledException)
        {
            if (!cancellationToken.IsCancellationRequested && this.sourceExited.Task.IsCompletedSuccessfully && this.candidate is not null)
            {
                using var observer = new CancellationTokenSource(this.Options.ObserverTimeout, this.time);
                try { return await operation.WaitAsync(observer.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { }
            }

            await this.DisposeAsync().ConfigureAwait(false);
            _ = operation.ContinueWith(completed => { _ = completed.Exception; }, TaskScheduler.Default);
            return this.Unconfirmed("Canceled", "AuditExportCanceled");
        }
    }

    private async Task<Result<AuditExportCompletion>> WriteCoreAsync(Stream destination, CancellationToken cancellationToken)
    {
        long written = 0, bytes = 0, flushed = 0, flushedBytes = 0;
        var uncertain = false;
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, this.deadline.Token);
            await foreach (var frame in this.ReadFramesAsync(linked.Token).ConfigureAwait(false))
            {
                using (frame)
                {
                    var size = frame.Bytes.Length;
                    uncertain = true;
                    if (this.Formatter.Format == "ndjson")
                    {
                        using var encoded = frame.TakeJson();
                        var writer = new NdjsonWriter(options: new() { MaxRecordBytes = Math.Max(this.Options.GraphByteLimit, Math.Max(this.Options.MetadataByteReserve, this.Options.CompletionByteReserve)) - 1 });
                        await writer.WriteEncodedRecordAsync(encoded, destination, linked.Token).ConfigureAwait(false);
                    }
                    else await destination.WriteAsync(frame.Bytes, linked.Token).ConfigureAwait(false);
                    uncertain = false; written++; bytes += size;
                    await destination.FlushAsync(linked.Token).ConfigureAwait(false);
                    flushed = written; flushedBytes = bytes;
                }
            }

            using var observer = new CancellationTokenSource(this.Options.ObserverTimeout, this.time);
            using var confirmation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, observer.Token);
            return await this.ConfirmAsync(new(NdjsonStreamStatus.Succeeded, true, true, written, bytes, flushed, flushedBytes, false), confirmation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { return this.Unconfirmed("Canceled", "AuditExportCanceled"); }
        catch (AuditExportException error) { return this.Unconfirmed("Failed", error.Code); }
        catch (Exception) { return this.Unconfirmed("Failed", uncertain ? "AuditExportWriteUnconfirmed" : "AuditExportFailed"); }
        finally { await this.DisposeAsync().ConfigureAwait(false); }
    }

    /// <summary>Cancels further output, retaining admission ownership until any active source or final validation actually settles.</summary>
    /// <example><code>await session.DisposeAsync();</code></example>
    public ValueTask DisposeAsync()
    {
        lock (this.lifecycle)
        {
            if (this.disposed != 0) return ValueTask.CompletedTask;
            this.disposed = 1;
            this.deadline.Cancel();
            if (this.enumerated == 0) this.sourceExited.TrySetResult();
            this.ReleaseWhenSettled();
        }

        return ValueTask.CompletedTask;
    }

    private async Task<AuditExtractionBatch> ReadAsync(AuditExtractionCursor cursor, CancellationToken cancellationToken, bool outputDeadline = true)
    {
        using var duration = new CancellationTokenSource(this.Options.ProviderReadTimeout, this.time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, outputDeadline ? this.deadline.Token : CancellationToken.None, duration.Token);
        var result = await this.provider.ReadExtractionAsync(cursor, linked.Token).ConfigureAwait(false);
        return result.IsSuccess ? result.Value : throw new AuditExportException(result.Errors.OfType<AuditError>().FirstOrDefault()?.Code ?? "AuditExportReadFailed");
    }

    private Result<AuditExportCompletion> Unconfirmed(string status, string reason) => Result<AuditExportCompletion>.Failure(new AuditExportFailureError((this.candidate ?? new() { Metadata = this.Metadata }) with
    { Status = status, ReasonCode = reason, FinishedUtc = this.time.GetUtcNow(), Progress = null, ContinuationToken = null, CheckpointToken = null, OutputConfirmed = false }));
    private void ReleaseWhenSettled()
    {
        var settled = Task.WhenAll(this.sourceExited.Task, this.confirming ?? Task.CompletedTask);
        _ = settled.ContinueWith(_ =>
        {
            if (Interlocked.Exchange(ref this.released, 1) != 0) return;
            this.admission.Dispose(); this.deadline.Dispose();
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private static void Add(Dictionary<string, long> counts, string name, long value)
    {
        if (counts.Count >= 64 && !counts.ContainsKey(name)) throw new AuditExportException("AuditExportMetadataTooLarge");
        counts[name] = checked(counts.GetValueOrDefault(name) + value);
    }
}
