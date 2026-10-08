// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>Persists completed operations periodically, isolating faults from application execution.</summary>
/// <example>Used by the shared profiling writer and its node-local health observations.</example>
public sealed class OperationProfilingWriterService : BackgroundService
{
    private readonly IOperationProfilingStore store;
    private readonly OperationProfilingCompletionQueue queue;
    private readonly OperationProfilingHealthState health;
    private readonly IProfilingNodeIdentityProvider nodes;
    private readonly IOperationProfiler profiler;
    private readonly OperationProfilingOptions options;
    private readonly TimeProvider clock;
    private readonly ILogger<OperationProfilingWriterService> logger;
    private readonly SemaphoreSlim ticks = new(1, 1);
    private readonly Dictionary<Guid, ProfilingClearAcknowledgement> acknowledgements = [];
    private readonly HashSet<Guid> confirmedAcknowledgements = [];
    private Guid openAttempt = Guid.NewGuid();
    private long? lastWarning;
    private volatile bool draining;
    private volatile bool stopped;

    /// <summary>Persists completed operations periodically, isolating faults from application execution.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public OperationProfilingWriterService(IOperationProfilingStore store, OperationProfilingCompletionQueue queue,
        OperationProfilingHealthState health, IProfilingNodeIdentityProvider nodes, IOperationProfiler profiler,
        ProfilingOptions options, TimeProvider clock = null, ILogger<OperationProfilingWriterService> logger = null)
    {
        this.store = store;
        this.queue = queue;
        this.health = health;
        this.nodes = nodes;
        this.profiler = profiler;
        this.options = options.Operations.Snapshot();
        this.clock = clock ?? TimeProvider.System;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var boundary = this.profiler.BeginExecutionBoundary();
        using var suppression = this.profiler.Suppress();
        await this.OpenAsync(stoppingToken).ConfigureAwait(false);
        using var timer = new PeriodicTimer(this.options.FlushInterval, this.clock);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await this.TickAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown owns the bounded final drain, not application cancellation.
        }
    }

    /// <summary>Executes one coalesced periodic writer tick with fixed publication and scheduling bounds.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public async Task TickAsync(CancellationToken cancellationToken = default)
    {
        if (this.stopped || !await this.ticks.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        using var boundary = this.profiler.BeginExecutionBoundary();
        using var suppression = this.profiler.Suppress();
        try
        {
            var started = this.clock.GetTimestamp();
            if (this.queue.Watermark().Lease is null && !await this.OpenAsync(cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            if (!await this.SynchronizeAsync(cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            var watermark = this.queue.Watermark();
            for (var attempt = 0; attempt < this.options.MaxBatchesPerFlush; attempt++)
            {
                if (attempt > 0 && this.clock.GetElapsedTime(started) >= this.options.FlushTimeBudget)
                {
                    break;
                }

                var entries = this.queue.Select(watermark);
                if (entries.Count == 0)
                {
                    break;
                }

                this.queue.ObserveInFlight(entries, true);
                IResult<ProfilingBatchWriteResult> result;
                try
                {
                    result = await this.AttemptAsync(token => this.store.AppendAsync(entries.Select(e => e.Envelope).ToArray(), token), cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    this.queue.ObserveInFlight(entries, false);
                }

                if (this.stopped)
                {
                    return;
                }

                this.health.LastAttemptTicks = this.clock.GetUtcNow().UtcTicks;
                var invalidResponse = result?.IsSuccess == true && (result.Value?.Records is null || result.Value.Records.Count != entries.Count
                    || result.Value.Records.Select(r => (r.OperationId, r.CompletionSequence)).Distinct().Count() != entries.Count
                    || result.Value.Records.Any(r => !entries.Any(e => e.Envelope.Record.Id == r.OperationId && e.Envelope.CompletionSequence == r.CompletionSequence) || !Enum.IsDefined(r.Outcome)));
                foreach (var entry in entries)
                {
                    var outcome = result?.IsSuccess == true && !invalidResponse
                        ? result.Value.Records.Single(r => r.OperationId == entry.Envelope.Record.Id && r.CompletionSequence == entry.Envelope.CompletionSequence).Outcome
                        : invalidResponse || result is null || result.Errors.OfType<ProfilingPersistenceError>().Any(e => e.MayHaveCommitted)
                            ? ProfilingWriteOutcome.UnknownCommit
                            : result.Errors.OfType<ProfilingPersistenceError>().Any(e => !e.Transient) ? ProfilingWriteOutcome.PermanentFailure : ProfilingWriteOutcome.TransientFailure;
                    this.Apply(entry, outcome);
                }

                if (!await this.SynchronizeAsync(cancellationToken).ConfigureAwait(false))
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            this.Warn();
        }
        finally
        {
            this.ticks.Release();
        }
    }

    private async Task<bool> OpenAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await this.AttemptAsync(token => this.store.OpenWriterAsync(new()
            {
                AttemptId = this.openAttempt, Node = this.nodes.GetNode(), LeaseDuration = this.options.WriterLeaseDuration,
            }, token), cancellationToken).ConfigureAwait(false);
            if (result?.IsSuccess != true)
            {
                this.Warn();
                return false;
            }

            var lease = result.Value;
            if (lease is null || lease.WriterId == Guid.Empty || lease.Token == Guid.Empty || lease.StoreEpoch == Guid.Empty
                || lease.NodeId != this.nodes.GetNode().Identity.Id || lease.SettledThrough < 0)
            {
                this.Warn();
                return false;
            }

            this.queue.Activate(lease);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            this.Warn();
            return false;
        }
    }

    private async Task<bool> SynchronizeAsync(CancellationToken cancellationToken)
    {
        var lease = this.queue.Watermark().Lease;
        if (lease is null)
        {
            return false;
        }

        var result = await this.AttemptAsync(token => this.store.SynchronizeWriterAsync(new()
        {
            Lease = lease, SettledThrough = this.queue.Settlement, LeaseDuration = this.options.WriterLeaseDuration,
        }, token), cancellationToken).ConfigureAwait(false);
        if (result?.IsSuccess != true)
        {
            this.Warn();
            return false;
        }

        if (!result.Value.Active)
        {
            this.queue.Retire();
            this.acknowledgements.Clear();
            this.confirmedAcknowledgements.Clear();
            this.openAttempt = Guid.NewGuid();
            return false;
        }

        if (result.Value.Lease?.Token != lease.Token || result.Value.Lease.WriterId != lease.WriterId
            || result.Value.PendingClears.Count > this.health.MaximumClears)
        {
            throw new InvalidOperationException("Provider returned inconsistent writer coordination.");
        }

        this.queue.Activate(result.Value.Lease);
        this.health.PreparingClears = result.Value.PendingClears.Count;
        var pendingIds = result.Value.PendingClears.Select(c => c.Id).ToHashSet();
        foreach (var id in this.acknowledgements.Keys.Where(id => !pendingIds.Contains(id) && this.confirmedAcknowledgements.Contains(id)).ToArray())
        {
            this.acknowledgements.Remove(id);
            this.confirmedAcknowledgements.Remove(id);
        }

        foreach (var clear in result.Value.PendingClears)
        {
            if (!this.acknowledgements.TryGetValue(clear.Id, out var acknowledgement))
            {
                if (this.acknowledgements.Count >= this.health.MaximumClears)
                {
                    throw new InvalidOperationException("Unconfirmed clear acknowledgements reached their bound.");
                }

                var cutoff = this.queue.Watermark();
                acknowledgement = new() { ClearId = clear.Id, Lease = cutoff.Lease, CompletionCutoff = cutoff.Sequence };
                this.acknowledgements.Add(clear.Id, acknowledgement);
            }

        }

        foreach (var acknowledgement in this.acknowledgements.Values.Where(a => !this.confirmedAcknowledgements.Contains(a.ClearId)))
        {
            var acknowledged = await this.AttemptAsync(token => this.store.AcknowledgeClearAsync(acknowledgement, token), cancellationToken).ConfigureAwait(false);
            if (acknowledged?.IsSuccess != true)
            {
                if (!pendingIds.Contains(acknowledgement.ClearId) && acknowledged?.Errors.Any(e => e is ProfilingInvalidStateError) == true)
                {
                    this.confirmedAcknowledgements.Add(acknowledgement.ClearId);
                    continue;
                }

                this.Warn();
                return false;
            }

            if (acknowledged.Value.CompletionCutoff != acknowledgement.CompletionCutoff || acknowledged.Value.Lease?.Token != lease.Token)
            {
                throw new InvalidOperationException("Provider changed the original clear acknowledgement.");
            }

            this.confirmedAcknowledgements.Add(acknowledgement.ClearId);
        }

        return true;
    }

    private void Apply(OperationProfilingCompletionQueue.Entry entry, ProfilingWriteOutcome outcome)
    {
        if (outcome is ProfilingWriteOutcome.TransientFailure or ProfilingWriteOutcome.UnknownCommit or ProfilingWriteOutcome.RetentionRejected or ProfilingWriteOutcome.CapacityRejected)
        {
            entry.Uncertain |= outcome == ProfilingWriteOutcome.UnknownCommit;
            if (entry.Retries < this.options.MaximumRetries)
            {
                entry.Retries++;
                entry.RetryAt = checked(this.clock.GetTimestamp() + this.clock.TimestampFrequency * entry.Retries);
                Interlocked.Increment(ref this.health.Retries);
                return;
            }
        }

        if (outcome is ProfilingWriteOutcome.Accepted or ProfilingWriteOutcome.AlreadyStored)
        {
            Interlocked.Increment(ref this.health.Persisted);
            Interlocked.Exchange(ref this.health.LastFlushTicks, this.clock.GetUtcNow().UtcTicks);
        }
        else if (outcome == ProfilingWriteOutcome.Cleared)
        {
            Interlocked.Increment(ref this.health.Administrative);
        }
        else if (entry.Uncertain || outcome == ProfilingWriteOutcome.UnknownCommit)
        {
            Interlocked.Increment(ref this.health.Unknown);
        }
        else
        {
            Interlocked.Increment(ref this.health.Lost);
        }

        this.queue.Remove(entry);
        if (outcome == ProfilingWriteOutcome.StaleWriter)
        {
            this.queue.Retire();
            this.acknowledgements.Clear();
            this.confirmedAcknowledgements.Clear();
            this.openAttempt = Guid.NewGuid();
        }
    }

    private async Task<IResult<T>> AttemptAsync<T>(Func<CancellationToken, Task<IResult<T>>> action, CancellationToken cancellationToken)
    {
        using var deadline = new CancellationTokenSource(this.options.AttemptTimeout, this.clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, cancellationToken);
        Task<IResult<T>> call;
        try
        {
            call = action(linked.Token);
        }
        catch (Exception)
        {
            return Result<T>.Failure(new ProfilingPersistenceError(false, true));
        }

        try
        {
            // Timeout is signaled independently, but a cancellation-ignoring call must unwind
            // before any replacement attempt or settled watermark is sent to the provider.
            try
            {
                return await call.WaitAsync(linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!call.IsCompleted)
            {
                this.health.Stalled = true;
                try { return await call.ConfigureAwait(false); }
                finally { this.health.Stalled = false; }
            }
        }
        catch (OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested && !this.draining)
            {
                throw;
            }

            return Result<T>.Failure(new ProfilingPersistenceError(true, true));
        }
        catch (Exception)
        {
            return Result<T>.Failure(new ProfilingPersistenceError(true, true));
        }
    }

    private void Warn()
    {
        Interlocked.Increment(ref this.health.Faults);
        var now = this.clock.GetTimestamp();
        if (this.lastWarning is null || this.clock.GetElapsedTime(this.lastWarning.Value, now) >= TimeSpan.FromMinutes(1))
        {
            this.lastWarning = now;
            try { this.logger?.LogWarning("[Profiling] background persistence unavailable; inspect profiling health"); }
            catch (Exception) { Interlocked.Increment(ref this.health.Faults); }
        }
    }

    /// <summary>Closes capture then attempts the configured bounded shutdown drain.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        (this.profiler as OperationProfiler)?.CloseForHost();
        this.draining = true;
        using var deadline = new CancellationTokenSource(this.options.ShutdownDrainTimeout, this.clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, cancellationToken);
        try
        {
            await base.StopAsync(linked.Token).ConfigureAwait(false);
            while (this.queue.Snapshot().Count > 0 && !linked.IsCancellationRequested)
            {
                await this.TickAsync(linked.Token).WaitAsync(linked.Token).ConfigureAwait(false);
                if (this.queue.Snapshot().Count > 0)
                {
                    await Task.Delay(this.options.FlushInterval, this.clock, linked.Token).ConfigureAwait(false);
                }
            }

            var lease = this.queue.Watermark().Lease;
            if (lease is not null)
            {
                await this.store.CloseWriterAsync(lease, linked.Token).WaitAsync(linked.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
            // A still-running provider retains uncertainty; do not settle or start a replacement.
            var pending = this.queue.Snapshot();
            Interlocked.Add(ref this.health.Unknown, pending.InFlightCount);
            Interlocked.Add(ref this.health.Lost, pending.Count - pending.InFlightCount);
        }
        finally
        {
            this.stopped = true;
            this.queue.Retire(expired: false);
        }
    }
}
