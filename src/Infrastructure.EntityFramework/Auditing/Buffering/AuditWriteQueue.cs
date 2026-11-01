// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Auditing;

using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Common;
using Polly;
using Polly.Retry;

/// <summary>Bounds finalized queued and in-flight documents, preserving FIFO receipts through finite delivery attempts.</summary>
/// <example><code>var receipt = queue.TryAppend(sealedRecord);</code></example>
public sealed class AuditWriteQueue
{
    private readonly object gate = new();
    private readonly Queue<Envelope> pending = new();
    private readonly Dictionary<Guid, Envelope> admitted = [];
    private readonly Dictionary<long, TaskCompletionSource<AuditFlushResult>> waiters = [];
    private readonly SemaphoreSlim signal = new(0, 1);
    private readonly Func<AuditRecord, CancellationToken, Task<Result<AuditAppendReceipt>>> write;
    private readonly AuditEntityFrameworkOptions options;
    private readonly TimeProvider time;
    private readonly Action<Guid, string> report;
    private readonly ResiliencePipeline<Result<AuditAppendReceipt>> retry;
    private readonly Guid epoch = Guid.NewGuid();
    private long accepted;
    private long resolved;
    private long persisted;
    private long failed;
    private long bytes;
    private bool accepting = true;
    private bool ready;
    private int running;

    /// <summary>Creates one provider-owned queue without starting a worker or resolving a key source.</summary>
    /// <remarks>The owned writer delegate must honor cancellation; accepted in-flight data retains capacity until it returns.</remarks>
    /// <example><code>var queue = new AuditWriteQueue(provider.AppendInlineAsync, options, TimeProvider.System);</code></example>
    public AuditWriteQueue(Func<AuditRecord, CancellationToken, Task<Result<AuditAppendReceipt>>> write, AuditEntityFrameworkOptions options, TimeProvider time, Action<Guid, string> report = null)
    {
        ArgumentNullException.ThrowIfNull(write);
        options.Validate();
        this.write = write;
        this.options = options;
        this.time = time;
        this.report = report;
        var pipeline = new ResiliencePipelineBuilder<Result<AuditAppendReceipt>> { TimeProvider = time };
        if (options.RetryCount > 0)
        {
            pipeline.AddRetry(new RetryStrategyOptions<Result<AuditAppendReceipt>>
            {
                ShouldHandle = new Polly.PredicateBuilder<Result<AuditAppendReceipt>>().HandleResult(result => result.IsFailure && !Permanent(result)),
                MaxRetryAttempts = options.RetryCount,
                Delay = options.RetryDelay,
                BackoffType = DelayBackoffType.Exponential,
                MaxDelay = TimeSpan.FromSeconds(2),
                UseJitter = false
            });
        }

        this.retry = pipeline.Build();
    }

    /// <summary>Admits a copied frozen document without waiting for database work or capacity.</summary>
    /// <example><code>var result = queue.TryAppend(sealedRecord);</code></example>
    public Result<AuditAppendReceipt> TryAppend(AuditRecord record)
    {
        var frozen = AuditRecordCodec.Freeze(record);
        if (frozen.IsFailure) return Result<AuditAppendReceipt>.Failure(frozen);
        var encoded = AuditRecordCodec.Encode(frozen.Value);
        var copy = AuditRecordCodec.Decode(encoded);
        if (copy.IsFailure) return Result<AuditAppendReceipt>.Failure(copy);
        lock (this.gate)
        {
            if (!this.accepting || this.accepted == long.MaxValue) return Result<AuditAppendReceipt>.Failure(new AuditError("AuditQueueStopped"));
            if (this.admitted.TryGetValue(record.AuditId, out var existing)) return existing.Record.ContentHash == copy.Value.ContentHash
                ? Result<AuditAppendReceipt>.Success(existing.Receipt) : Result<AuditAppendReceipt>.Failure(new AuditError("AuditIdentityConflict"));
            if (this.admitted.Count >= this.options.QueueRecordLimit || this.bytes + encoded.Length > this.options.QueueByteLimit) return Result<AuditAppendReceipt>.Failure(new AuditError("AuditQueueFull"));
            var receipt = new AuditAppendReceipt { ReceiptEpoch = this.epoch, AcceptanceSequence = ++this.accepted, AuditId = record.AuditId, State = "Queued" };
            var envelope = new Envelope(copy.Value, receipt, encoded.Length);
            this.pending.Enqueue(envelope);
            this.admitted.Add(record.AuditId, envelope);
            this.bytes += encoded.Length;
            if (this.pending.Count >= this.options.BatchSize) { this.ready = true; this.Wake(); }

            return Result<AuditAppendReceipt>.Success(receipt);
        }
    }

    /// <summary>Copies cumulative confirmations, failures and still-pending admissions for this process receipt epoch.</summary>
    /// <example><code>var status = queue.Snapshot();</code></example>
    public AuditFlushResult Snapshot() { lock (this.gate) return this.Snapshot(this.accepted); }

    /// <summary>The encoded bytes still occupying queued or in-flight capacity.</summary>
    /// <example><code>var occupied = queue.PendingBytes;</code></example>
    public long PendingBytes { get { lock (this.gate) return this.bytes; } }

    /// <summary>Stops new admissions while retaining and waking all accepted pending work.</summary>
    /// <example><code>queue.CloseAdmissions();</code></example>
    public void CloseAdmissions()
    {
        lock (this.gate) { this.accepting = false; this.ready = true; this.Wake(); }
    }

    /// <summary>Runs the sole owned FIFO writer with periodic partial batches and finite retry policy.</summary>
    /// <example><code>await queue.RunAsync(stoppingToken);</code></example>
    public async Task RunAsync(CancellationToken stoppingToken)
    {
        if (Interlocked.CompareExchange(ref this.running, 1, 0) != 0) throw new InvalidOperationException("The audit writer is already running.");
        using var timer = this.time.CreateTimer(_ => this.ReadyBatch(), null, this.options.BatchInterval, this.options.BatchInterval);
        try
        {
            while (true)
            {
                await this.signal.WaitAsync(stoppingToken).ConfigureAwait(false);
                List<Envelope> batch;
                lock (this.gate)
                {
                    if (!this.accepting && this.admitted.Count == 0) return;
                    if (!this.ready && this.pending.Count < this.options.BatchSize) continue;
                    this.ready = false;
                    batch = [];
                    while (batch.Count < this.options.BatchSize && this.pending.Count > 0) batch.Add(this.pending.Dequeue());
                }

                foreach (var envelope in batch)
                {
                    var result = await this.DeliverAsync(envelope.Record, stoppingToken).ConfigureAwait(false);
                    this.Resolve(envelope, result);
                }

                lock (this.gate)
                {
                    if (!this.accepting || this.ready || this.pending.Count >= this.options.BatchSize) this.Wake();
                }
            }
        }
        finally { this.CloseAdmissions(); Volatile.Write(ref this.running, 0); }
    }

    /// <summary>Confirms exactly the cumulative acceptance boundary captured at invocation, including earlier failures.</summary>
    /// <remarks>Cancellation stops only this wait. Incomplete Results carry a safe AuditFlushIncompleteError with explicit counts.</remarks>
    /// <example><code>var confirmed = await queue.FlushAsync(cancellationToken);</code></example>
    public async Task<Result<AuditFlushResult>> FlushAsync(CancellationToken cancellationToken = default)
    {
        Task<AuditFlushResult> completion;
        long target;
        lock (this.gate)
        {
            target = this.accepted;
            if (this.resolved == target) return Outcome(this.Snapshot(target));
            if (!this.waiters.TryGetValue(target, out var waiter)) this.waiters.Add(target, waiter = new(TaskCreationOptions.RunContinuationsAsynchronously));
            completion = waiter.Task;
            this.ready = true;
            this.Wake();
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(this.options.OperationTimeout);
        try { return Outcome(await completion.WaitAsync(timeout.Token).ConfigureAwait(false)); }
        catch (OperationCanceledException)
        {
            if (completion.IsCompletedSuccessfully) return Outcome(await completion.ConfigureAwait(false));
            AuditFlushResult outcome = null;
            lock (this.gate) { if (!completion.IsCompletedSuccessfully) outcome = this.Snapshot(target); }

            return outcome is null ? Outcome(await completion.ConfigureAwait(false)) : Outcome(outcome);
        }
    }

    private async Task<Result<AuditAppendReceipt>> DeliverAsync(AuditRecord record, CancellationToken stoppingToken)
    {
        return await this.retry.ExecuteAsync(async token =>
        {
            Result<AuditAppendReceipt> result;
            using var deadline = new CancellationTokenSource(this.options.OperationTimeout, this.time);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token, deadline.Token);
            try
            {
                result = await this.write(record, timeout.Token).ConfigureAwait(false);
                if (result.IsSuccess && result.Value.State is not ("Persisted" or "AlreadyPresent")) result = Result<AuditAppendReceipt>.Failure(new AuditError("AuditDeliveryUnconfirmed"));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
            catch (Exception) { result = Result<AuditAppendReceipt>.Failure(new AuditError("AuditStoreUnavailable")); }

            return result;
        }, stoppingToken).ConfigureAwait(false);
    }
    private void Resolve(Envelope envelope, Result<AuditAppendReceipt> result)
    {
        lock (this.gate)
        {
            if (envelope.Receipt.AcceptanceSequence != this.resolved + 1) throw new InvalidOperationException("Audit delivery order is invalid.");
            this.resolved++;
            if (result.IsSuccess) this.persisted++; else this.failed++;
            this.admitted.Remove(envelope.Record.AuditId);
            this.bytes -= envelope.Bytes;
            if (this.waiters.Remove(this.resolved, out var waiter)) waiter.TrySetResult(this.Snapshot(this.resolved));
        }

        if (result.IsFailure)
        {
            try { this.report?.Invoke(envelope.Record.AuditId, "AuditDeliveryFailed"); }
            catch (Exception) { /* Reporting cannot rewrite a terminal delivery outcome. */ }
        }
    }
    private void ReadyBatch()
    {
        lock (this.gate)
        {
            if (this.pending.Count > 0) { this.ready = true; this.Wake(); }
        }
    }
    private void Wake() { if (this.signal.CurrentCount == 0) this.signal.Release(); }
    private AuditFlushResult Snapshot(long target) => new() { ReceiptEpoch = this.epoch, ThroughSequence = target, Persisted = this.persisted, Failed = this.failed, Pending = Math.Max(0, target - this.resolved) };
    private static Result<AuditFlushResult> Outcome(AuditFlushResult outcome) => outcome.Failed == 0 && outcome.Pending == 0
        ? Result<AuditFlushResult>.Success(outcome) : Result<AuditFlushResult>.Failure(new AuditFlushIncompleteError(outcome));
    private static bool Permanent(Result<AuditAppendReceipt> result) => result.Errors.OfType<AuditError>().Any(error => error.Code is "AuditIdentityConflict" or "AuditIntegrityInvalid" or "AuditIntegrityMissingProtection" or "AuditRecordTooLarge" or "AuditEvidenceExpired" or "AuditDocumentInvalid");
    private sealed record Envelope(AuditRecord Record, AuditAppendReceipt Receipt, int Bytes);
}
