// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using Polly;
using Polly.CircuitBreaker;

/// <summary>Applies one process-local consecutive infrastructure-failure circuit to a complete named persistent provider.</summary>
/// <remarks>
/// The optional behavior uses the existing resilience pipeline's manual circuit control. A bounded counter supplies
/// consecutive-failure semantics; recovery admits exactly one nonmutating quota/readiness probe. It never retries
/// a mutation, consumes a source, or releases underlying I/O early. Registration omits disabled and in-memory chains.
/// </remarks>
/// <example><code>var guarded = new CircuitBreakingCacheStoreProviderBehavior(provider, options.Breaker, clock, diagnostics.ForProvider(provider.Name));</code></example>
public sealed class CircuitBreakingCacheStoreProviderBehavior : CacheStoreProviderBehavior, ICacheProviderCircuitState, ICacheProviderInfrastructureObserver
{
    private readonly Lock gate = new();
    private readonly SemaphoreSlim controlGate = new(1, 1);
    private readonly CircuitBreakerManualControl manual = new();
    private readonly CircuitBreakerStateProvider state = new();
    private readonly ResiliencePipeline pipeline;
    private readonly CacheProviderBreakerOptions options;
    private readonly TimeProvider time;
    private readonly CacheObservationSlot observations;
    private int failures;
    private long epoch;
    private long rejected;
    private long probes;
    private DateTimeOffset openedAt;
    private bool open;
    private bool probing;
    private bool initialized;
    private int disposed;

    /// <summary>Composes one optional provider circuit without native access or source execution.</summary>
    /// <param name="inner">The complete authoritative persistent provider.</param>
    /// <param name="options">The enabled finite consecutive-failure and cooldown policy.</param>
    /// <param name="timeProvider">The observation and cooldown clock.</param>
    /// <param name="observations">Optional bounded provider observations.</param>
    public CircuitBreakingCacheStoreProviderBehavior(ICacheStoreProvider inner, CacheProviderBreakerOptions options,
        TimeProvider timeProvider = null, CacheObservationSlot observations = null) : base(inner)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.time = timeProvider ?? TimeProvider.System;
        this.observations = observations;
        if (!options.Enabled || options.FailureThreshold <= 0 || options.Cooldown <= TimeSpan.Zero || !inner.Capabilities.Persistent)
        {
            throw new ArgumentException("Only enabled finite persistent-provider breaker policies create this behavior.", nameof(options));
        }

        this.pipeline = new ResiliencePipelineBuilder { TimeProvider = this.time }.AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            ManualControl = this.manual, StateProvider = this.state,
            // Rolling failure ratios do not express the specified consecutive threshold.
            // The finite latch below drives existing manual control, without retry/fallback strategies.
            ShouldHandle = static _ => ValueTask.FromResult(false)
        }).Build();
    }

    /// <inheritdoc />
    public long CircuitEpoch { get { lock (this.gate) { return this.epoch; } } }

    /// <inheritdoc />
    public override async ValueTask<Result> InitializeAsync(IReadOnlyList<CacheProviderCollection> collections, CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref this.disposed) != 0) { return Result.Failure(this.Unavailable()); }

        // Initial catalog readiness remains explicit and retryable. An uninitialized store cannot
        // recover through GetQuota, and initialization is never substituted for a recovery probe.
        var result = await this.Inner.InitializeAsync(collections, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess) { lock (this.gate) { this.initialized = true; } }

        return result;
    }

    /// <inheritdoc />
    public CacheProviderBreakerSnapshot Snapshot()
    {
        lock (this.gate)
        {
            var remaining = this.open ? this.options.Cooldown - (this.time.GetUtcNow() - this.openedAt) : TimeSpan.Zero;
            var actual = this.probing ? "HalfOpen" : this.open || this.state.CircuitState == CircuitState.Isolated ? "Open" : "Closed";
            return new(this.Name, actual, this.failures, remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero,
                this.rejected, this.probes, this.time.GetUtcNow());
        }
    }

    /// <inheritdoc />
    public ValueTask ReportInfrastructureTimeoutAsync(long epoch) => this.FailureAsync(epoch);

    /// <inheritdoc />
    public ValueTask ReportInfrastructureOutcomeAsync(long epoch, bool infrastructureFailure)
    {
        if (infrastructureFailure) { return this.FailureAsync(epoch); }

        lock (this.gate) { if (this.initialized && !this.open && this.epoch == epoch) { this.failures = 0; } }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) != 0) { return; }

        await this.Inner.DisposeAsync().ConfigureAwait(false);
    }

    private async ValueTask<Result<T>> ExecuteAsync<T>(Func<CancellationToken, ValueTask<Result<T>>> operation, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (Volatile.Read(ref this.disposed) != 0) { return Result<T>.Failure(this.Unavailable()); }

        var admission = await this.EnterAsync(token).ConfigureAwait(false);
        if (!admission.Allowed) { return Result<T>.Failure(this.Unavailable()); }

        try
        {
            token.ThrowIfCancellationRequested();
            var result = await this.pipeline.ExecuteAsync(operation, token).ConfigureAwait(false);
            if (!token.IsCancellationRequested)
            {
                if (CacheProviderFailureClassifier.IsInfrastructureFailure(result)) { await this.FailureAsync(admission.Epoch).ConfigureAwait(false); }
                else { lock (this.gate) { if (!this.open && this.epoch == admission.Epoch) { this.failures = 0; } } }
            }

            return result;
        }
        catch (BrokenCircuitException) { return Result<T>.Failure(this.Unavailable()); }
        catch (Exception exception) when (CacheProviderFailureClassifier.IsInfrastructureFailure(exception, token))
        {
            await this.FailureAsync(admission.Epoch).ConfigureAwait(false);
            return Result<T>.Failure(new CacheError(CacheErrorCode.ProviderUnavailable, "The selected cache provider infrastructure is unavailable."));
        }
    }

    private async ValueTask<Result> ExecuteResultAsync(Func<CancellationToken, ValueTask<Result>> operation, CancellationToken token)
    {
        var result = await this.ExecuteAsync(async cancellation =>
        {
            var actual = await operation(cancellation).ConfigureAwait(false);
            return actual.IsSuccess ? Result<bool>.Success(true) : Result<bool>.Failure((IResult)actual);
        }, token).ConfigureAwait(false);
        return result.IsSuccess ? Result.Success() : Result.Failure().WithErrors(result.Errors);
    }

    private async ValueTask<Admission> EnterAsync(CancellationToken token)
    {
        long probeEpoch;
        lock (this.gate)
        {
            if (!this.open) { return new(true, this.epoch); }

            if (this.probing || this.time.GetUtcNow() - this.openedAt < this.options.Cooldown)
            {
                this.rejected++;
                this.observations?.Record(CacheObservationKind.BreakerRejected);
                return new(false, this.epoch);
            }

            this.probing = true;
            this.probes++;
            probeEpoch = ++this.epoch;
        }

        this.observations?.Record(CacheObservationKind.BreakerProbe);
        await this.ControlAsync(false, probeEpoch).ConfigureAwait(false);
        try
        {
            var health = await this.Inner.GetQuotaAsync(token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (health.IsSuccess)
            {
                lock (this.gate)
                {
                    if (this.epoch != probeEpoch) { return new(false, this.epoch); }

                    this.open = false;
                    this.failures = 0;
                    this.observations?.Record(CacheObservationKind.BreakerRecovered);
                    return new(true, ++this.epoch);
                }
            }

            await this.ReopenProbeAsync(probeEpoch).ConfigureAwait(false);
            return new(false, this.CircuitEpoch);
        }
        catch (OperationCanceledException)
        {
            await this.ReopenProbeAsync(probeEpoch).ConfigureAwait(false);
            throw;
        }
        catch (Exception exception) when (CacheProviderFailureClassifier.IsInfrastructureFailure(exception, token))
        {
            await this.ReopenProbeAsync(probeEpoch).ConfigureAwait(false);
            return new(false, this.CircuitEpoch);
        }
        finally { lock (this.gate) { this.probing = false; } }
    }

    private async ValueTask FailureAsync(long expectedEpoch)
    {
        if (Volatile.Read(ref this.disposed) != 0) { return; }

        long openedEpoch;
        lock (this.gate)
        {
            if (!this.initialized || expectedEpoch != this.epoch || (this.open && !this.probing)) { return; }

            this.observations?.Record(CacheObservationKind.ProviderFailure);
            this.failures = (int)Math.Min(this.options.FailureThreshold, (long)this.failures + 1);
            if (this.failures < this.options.FailureThreshold && !this.probing) { return; }

            this.open = true;
            this.openedAt = this.time.GetUtcNow();
            openedEpoch = ++this.epoch;
        }

        this.observations?.Record(CacheObservationKind.BreakerOpened);
        await this.ControlAsync(true, openedEpoch).ConfigureAwait(false);
    }

    private async ValueTask ReopenProbeAsync(long expectedEpoch)
    {
        long openedEpoch;
        lock (this.gate)
        {
            if (this.epoch != expectedEpoch) { return; }

            this.open = true;
            this.openedAt = this.time.GetUtcNow();
            openedEpoch = ++this.epoch;
        }

        this.observations?.Record(CacheObservationKind.BreakerOpened);
        await this.ControlAsync(true, openedEpoch).ConfigureAwait(false);
    }

    private async ValueTask ControlAsync(bool isolate, long expectedEpoch)
    {
        await this.controlGate.WaitAsync().ConfigureAwait(false);
        try
        {
            lock (this.gate) { if (this.epoch != expectedEpoch) { return; } }

            if (isolate) { await this.manual.IsolateAsync().ConfigureAwait(false); }
            else { await this.manual.CloseAsync().ConfigureAwait(false); }
        }
        finally { this.controlGate.Release(); }
    }

    private CacheError Unavailable()
    {
        var snapshot = this.Snapshot();
        return new(CacheErrorCode.ProviderUnavailable, $"The named cache provider circuit is unavailable; cooldown remaining is {snapshot.RemainingCooldown.TotalMilliseconds:0} ms.");
    }

    private readonly record struct Admission(bool Allowed, long Epoch);

    /// <inheritdoc />
    public override ValueTask<Result<CacheReadValidation>> LookupAsync(CacheEntryIdentity identity, CacheReadRequest request, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(token => this.Inner.LookupAsync(identity, request, token), cancellationToken);

    /// <inheritdoc />
    public override ValueTask<Result<CacheReadValidation>> ValidateReadAsync(CacheEntryIdentity identity, CacheEntryVersion version, CacheReadRequest request,
        CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(token => this.Inner.ValidateReadAsync(identity, version, request, token), cancellationToken);

    /// <inheritdoc />
    public override ValueTask<Result<CachePublicationFence>> CaptureFenceAsync(CacheEntryIdentity identity, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(token => this.Inner.CaptureFenceAsync(identity, token), cancellationToken);

    /// <inheritdoc />
    public override ValueTask<Result> ReleaseFenceAsync(CacheEntryIdentity identity, CachePublicationFence fence, CancellationToken cancellationToken = default) =>
        this.ExecuteResultAsync(token => this.Inner.ReleaseFenceAsync(identity, fence, token), cancellationToken);

    /// <inheritdoc />
    public override ValueTask<Result<ICacheProviderWriteSession>> BeginWriteAsync(CacheProviderWriteRequest request, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(token => this.Inner.BeginWriteAsync(request, token), cancellationToken);

    /// <inheritdoc />
    public override ValueTask<Result<CacheEntryMetadata>> TouchAsync(CacheEntryIdentity identity, CacheEntryVersion? version = null, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(token => this.Inner.TouchAsync(identity, version, token), cancellationToken);

    /// <inheritdoc />
    public override ValueTask<Result<CacheInvalidationOutcome>> RemoveAsync(CacheEntryIdentity identity, CacheWriteCondition condition = default,
        CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(token => this.Inner.RemoveAsync(identity, condition, token), cancellationToken);

    /// <inheritdoc />
    public override ValueTask<Result<CacheInvalidationOutcome>> RemoveAsync(CacheProviderRemoval request, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(token => this.Inner.RemoveAsync(request, token), cancellationToken);

    /// <inheritdoc />
    public override ValueTask<Result<CacheMetadataPage>> QueryAsync(CacheMetadataQuery query, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(token => this.Inner.QueryAsync(query, token), cancellationToken);

    /// <inheritdoc />
    public override ValueTask<Result<CacheRefreshPage>> QueryRefreshAsync(CacheRefreshQuery query, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(token => this.Inner.QueryRefreshAsync(query, token), cancellationToken);

    /// <inheritdoc />
    public override ValueTask<Result> RecordRefreshAttemptAsync(CacheRefreshAttempt attempt, CancellationToken cancellationToken = default) =>
        this.ExecuteResultAsync(token => this.Inner.RecordRefreshAttemptAsync(attempt, token), cancellationToken);

    /// <inheritdoc />
    public override ValueTask<Result<CacheInvalidationOutcome>> InvalidateAsync(CacheProviderInvalidation request, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(token => this.Inner.InvalidateAsync(request, token), cancellationToken);

    /// <inheritdoc />
    public override ValueTask<Result<CacheOwnershipObservation>> AcquireOwnershipAsync(CacheOwnershipRequest request, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(token => this.Inner.AcquireOwnershipAsync(request, token), cancellationToken);

    /// <inheritdoc />
    public override ValueTask<Result<CacheOwnershipObservation>> RenewOwnershipAsync(CacheEntryIdentity identity, CacheOwnerToken owner, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(token => this.Inner.RenewOwnershipAsync(identity, owner, token), cancellationToken);

    /// <inheritdoc />
    public override ValueTask<Result<CacheOwnershipObservation>> ObserveOwnershipAsync(CacheOwnershipRequest request, string flightId, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(token => this.Inner.ObserveOwnershipAsync(request, flightId, token), cancellationToken);

    /// <inheritdoc />
    public override ValueTask<Result> CompleteOwnershipAsync(CacheEntryIdentity identity, CacheOwnerToken owner, CacheOwnershipCompletion completion,
        CancellationToken cancellationToken = default) =>
        this.ExecuteResultAsync(token => this.Inner.CompleteOwnershipAsync(identity, owner, completion, token), cancellationToken);

    /// <inheritdoc />
    public override ValueTask<Result> ReleaseInterestAsync(CacheEntryIdentity identity, string flightId, string interestId, CancellationToken cancellationToken = default) =>
        this.ExecuteResultAsync(token => this.Inner.ReleaseInterestAsync(identity, flightId, interestId, token), cancellationToken);

    /// <inheritdoc />
    public override ValueTask<Result<CacheMaintenanceOutcome>> MaintainAsync(CacheMaintenanceRequest request, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(token => this.Inner.MaintainAsync(request, token), cancellationToken);

    /// <inheritdoc />
    public override ValueTask<Result<CacheProviderQuota>> GetQuotaAsync(CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(token => this.Inner.GetQuotaAsync(token), cancellationToken);

    /// <inheritdoc />
    public override ValueTask<Result<CacheWriteOutcome>> ReconcileAsync(string operationId, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(token => this.Inner.ReconcileAsync(operationId, token), cancellationToken);

    /// <inheritdoc />
    public override ValueTask<Result<CacheProviderOperation>> SaveOperationAsync(CacheProviderOperation operation, string expectedRevision = null,
        CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(token => this.Inner.SaveOperationAsync(operation, expectedRevision, token), cancellationToken);

    /// <inheritdoc />
    public override ValueTask<Result<CacheProviderOperation>> GetOperationAsync(string operationId, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(token => this.Inner.GetOperationAsync(operationId, token), cancellationToken);

    /// <inheritdoc />
    public override ValueTask<Result<IReadOnlyList<CacheProviderOperation>>> GetOperationsAsync(int limit = 100, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(token => this.Inner.GetOperationsAsync(limit, token), cancellationToken);
}