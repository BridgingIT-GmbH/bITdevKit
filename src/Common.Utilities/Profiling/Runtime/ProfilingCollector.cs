// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Owns one node-local profiling collection loop and manual captures.</summary>
/// <remarks>
/// Scheduled opportunities use absolute monotonic deadlines. Capture is single-flight across
/// scheduled and manual operations, and no store polling is used for control.
/// </remarks>
/// <example><code>await collector.StartAsync(session, cancellationToken);</code></example>
public sealed class ProfilingCollector : IProfilingCollector
{
    private readonly SemaphoreSlim controlGate = new(1, 1);
    private readonly IProfilingStore store;
    private readonly IProfilingSnapshotProbe probe;
    private readonly IProfilingRuntimeContextFactory contextFactory;
    private readonly IProfilingNodeIdentityProvider nodeIdentityProvider;
    private readonly ProfilingSessionFinalizer finalizer;
    private readonly ProfilingOptions options;
    private readonly TimeProvider timeProvider;
    private readonly IBroadcastRegistryStore broadcastRegistry;
    private readonly IBroadcastNodeIdentityProvider broadcastIdentityProvider;
    private readonly ProfilingActiveSessionContext activeSessionContext;
    private CollectionState current;

    /// <summary>Creates the node-local profiling collector.</summary>
    /// <param name="store">The profiling session store.</param>
    /// <param name="probe">The one-shot runtime probe.</param>
    /// <param name="contextFactory">The immutable runtime-context factory.</param>
    /// <param name="nodeIdentityProvider">The profiling node identity provider.</param>
    /// <param name="finalizer">The logical session finalizer.</param>
    /// <param name="options">The shared profiling options.</param>
    /// <param name="timeProvider">The monotonic and UTC time provider.</param>
    /// <param name="broadcastRegistry">The optional existing Broadcast registry.</param>
    /// <param name="broadcastIdentityProvider">The optional local Broadcast identity provider.</param>
    /// <param name="activeSessionContext">The optional process-local metric association context.</param>
    /// <example><code>var collector = new ProfilingCollector(store, probe, contexts, nodes, finalizer, options, clock, registry, identities);</code></example>
    public ProfilingCollector(
        IProfilingStore store,
        IProfilingSnapshotProbe probe,
        IProfilingRuntimeContextFactory contextFactory,
        IProfilingNodeIdentityProvider nodeIdentityProvider,
        ProfilingSessionFinalizer finalizer,
        ProfilingOptions options,
        TimeProvider timeProvider,
        IBroadcastRegistryStore broadcastRegistry = null,
        IBroadcastNodeIdentityProvider broadcastIdentityProvider = null,
        ProfilingActiveSessionContext activeSessionContext = null
    )
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.probe = probe ?? throw new ArgumentNullException(nameof(probe));
        this.contextFactory =
            contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        this.nodeIdentityProvider =
            nodeIdentityProvider ?? throw new ArgumentNullException(nameof(nodeIdentityProvider));
        this.finalizer = finalizer ?? throw new ArgumentNullException(nameof(finalizer));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.broadcastRegistry = broadcastRegistry;
        this.broadcastIdentityProvider = broadcastIdentityProvider;
        this.activeSessionContext = activeSessionContext;
    }

    /// <inheritdoc />
    public async Task<Result> StartAsync(
        ProfilingSession session,
        CancellationToken cancellationToken = default
    )
    {
        var validation = this.ValidateCollectableSession(session);
        if (validation is not null)
        {
            return Result.Failure().WithError(validation);
        }

        await this.controlGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (this.current?.Session.Identity.Id == session.Identity.Id)
            {
                return Result.Success();
            }

            if (this.current is not null && session.StartedUtc <= this.current.Session.StartedUtc)
            {
                return Result
                    .Failure()
                    .WithError(
                        new ProfilingInvalidStateError(
                            "Only a newer valid profiling session can replace local collection."
                        )
                    );
            }

            var prepared = await this.PrepareStateAsync(
                    session,
                    ProfilingNodeRole.ExpectedParticipant,
                    cancellationToken
                )
                .ConfigureAwait(false);
            if (prepared.IsFailure)
            {
                return Result.Failure().WithErrors(prepared.Errors).WithMessages(prepared.Messages);
            }

            if (this.current is not null)
            {
                await this.StopStateAsync(
                        this.current,
                        ProfilingParticipationState.Stopped,
                        "Replaced by a newer profiling session.",
                        CancellationToken.None
                    )
                    .ConfigureAwait(false);
                this.current = null;
            }

            this.current = prepared.Value;
            this.activeSessionContext?.Set(this.current.Session, this.current.Node);
            this.current.Completion = this.RunScheduledAsync(this.current);
            return Result.Success();
        }
        finally
        {
            this.controlGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<Result> StopAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default
    )
    {
        if (sessionId == Guid.Empty)
        {
            return Result
                .Failure()
                .WithError(
                    new ProfilingValidationError(
                        "A valid profiling session identifier is required."
                    )
                );
        }

        await this.controlGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var state = this.current;
            if (state?.Session.Identity.Id != sessionId)
            {
                return Result.Success();
            }

            await this.StopStateAsync(
                    state,
                    ProfilingParticipationState.Stopped,
                    null,
                    cancellationToken
                )
                .ConfigureAwait(false);
            this.activeSessionContext?.Clear(state.Session.Identity.Id);
            Interlocked.CompareExchange(ref this.current, null, state);
            return Result.Success();
        }
        finally
        {
            this.controlGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<Result<ProfilingSnapshot>> CaptureAsync(
        ProfilingSession session,
        ProfilingNodeRole role,
        CancellationToken cancellationToken = default
    )
    {
        var validation = this.ValidateCaptureSession(session);
        if (validation is not null)
        {
            return Failure<ProfilingSnapshot>(validation);
        }

        var state = this.current;
        var ownsState = state?.Session.Identity.Id != session.Identity.Id;
        if (ownsState)
        {
            var prepared = await this.PrepareStateAsync(session, role, cancellationToken)
                .ConfigureAwait(false);
            if (prepared.IsFailure)
            {
                return CopyFailure<ProfilingSnapshot, CollectionState>(prepared);
            }

            state = prepared.Value;
        }

        await state.CaptureGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (ownsState)
            {
                var baselineResult = await this
                    .CaptureRateBaselineAsync(state, cancellationToken)
                    .ConfigureAwait(false);
                if (baselineResult.IsFailure)
                {
                    return baselineResult;
                }

                await Task.Delay(
                        ProfilingOptions.MinimumSamplingInterval,
                        this.timeProvider,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }

            return await this
                .CaptureOneAsync(state, state.GetElapsed(this.timeProvider), cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            state.CaptureGate.Release();
            if (ownsState)
            {
                await this.CompleteParticipationAsync(
                        state,
                        ProfilingParticipationState.Completed,
                        null
                    )
                    .ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Stops local collection when the host shuts down before the profiling session completes.
    /// </summary>
    /// <param name="cancellationToken">The token that cancels waiting for local shutdown.</param>
    /// <returns>A task that completes after local collection has stopped.</returns>
    /// <example><code>await collector.StopForHostAsync(cancellationToken);</code></example>
    public async Task StopForHostAsync(CancellationToken cancellationToken)
    {
        await this.controlGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var state = this.current;
            if (state is null)
            {
                return;
            }

            await this.StopStateAsync(
                    state,
                    ProfilingParticipationState.Failed,
                    "Host stopped before profiling collection completed.",
                    cancellationToken
                )
                .ConfigureAwait(false);
            this.activeSessionContext?.Clear(state.Session.Identity.Id);
            Interlocked.CompareExchange(ref this.current, null, state);
        }
        finally
        {
            this.controlGate.Release();
        }
    }

    private async Task RunScheduledAsync(CollectionState state)
    {
        var firstOpportunity = true;
        var scheduledElapsed = state.BaseElapsed;
        try
        {
            while (scheduledElapsed < state.Session.Duration)
            {
                var delay = scheduledElapsed - state.GetElapsed(this.timeProvider);
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, this.timeProvider, state.Cancellation.Token)
                        .ConfigureAwait(false);
                }

                state.Cancellation.Token.ThrowIfCancellationRequested();
                if (state.CaptureGate.Wait(0))
                {
                    try
                    {
                        await this.CaptureOneAsync(
                                state,
                                scheduledElapsed,
                                state.Cancellation.Token
                            )
                            .ConfigureAwait(false);
                    }
                    finally
                    {
                        state.CaptureGate.Release();
                    }
                }
                else
                {
                    state.SkippedCaptureCount++;
                }

                scheduledElapsed = firstOpportunity
                    ? NextAbsoluteOpportunity(state.BaseElapsed, state.Session.SamplingInterval)
                    : scheduledElapsed.Add(state.Session.SamplingInterval);
                firstOpportunity = false;

                var elapsedAfterCapture = state.GetElapsed(this.timeProvider);
                while (
                    scheduledElapsed < state.Session.Duration
                    && scheduledElapsed < elapsedAfterCapture
                )
                {
                    state.SkippedCaptureCount++;
                    scheduledElapsed = scheduledElapsed.Add(state.Session.SamplingInterval);
                }
            }

            var remaining = state.Session.Duration - state.GetElapsed(this.timeProvider);
            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining, this.timeProvider, state.Cancellation.Token)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (state.Cancellation.IsCancellationRequested)
        {
            // Explicit stop, replacement, and host shutdown are normal lifecycle paths.
        }
        finally
        {
            await state.CaptureGate.WaitAsync().ConfigureAwait(false);
            state.CaptureGate.Release();
            var terminalState =
                state.RequestedTerminalState ?? ProfilingParticipationState.Completed;
            await this.CompleteParticipationAsync(state, terminalState, state.Failure)
                .ConfigureAwait(false);
        }

        this.activeSessionContext?.Clear(state.Session.Identity.Id);

        try
        {
            if (!state.Cancellation.IsCancellationRequested)
            {
                var finalizationDelay =
                    state.Session.EndsUtc.Add(state.FinalizationGracePeriod)
                    - this.timeProvider.GetUtcNow();
                if (finalizationDelay > TimeSpan.Zero)
                {
                    await Task.Delay(finalizationDelay, this.timeProvider, state.Cancellation.Token)
                        .ConfigureAwait(false);
                }

                await this.finalizer.FinalizeAsync(state.Session).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (state.Cancellation.IsCancellationRequested)
        {
            // Shutdown or a local stop during the grace period cancels this node's finalizer.
        }

        Interlocked.CompareExchange(ref this.current, null, state);
    }

    private async Task<Result<ProfilingSnapshot>> CaptureOneAsync(
        CollectionState state,
        TimeSpan scheduledElapsed,
        CancellationToken cancellationToken
    )
    {
        var sequence = state.SuccessfulCaptureCount + 1;
        var request = new ProfilingCaptureRequest(
            state.Session,
            state.Node,
            sequence,
            scheduledElapsed,
            state.GetElapsed(this.timeProvider),
            state.SkippedCaptureCount,
            state.FailedCaptureCount
        );

        Result<ProfilingSnapshot> probeResult;
        try
        {
            probeResult = await this
                .probe.CaptureAsync(request, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            state.FailedCaptureCount++;
            return Failure<ProfilingSnapshot>(
                new ProfilingUnavailableError(
                    $"The profiling probe failed: {exception.GetType().Name}."
                )
            );
        }

        if (probeResult.IsFailure)
        {
            state.FailedCaptureCount++;
            return probeResult;
        }

        var storedResult = await this
            .store.AddSnapshotAsync(probeResult.Value, CancellationToken.None)
            .ConfigureAwait(false);
        if (storedResult.IsFailure)
        {
            state.FailedCaptureCount++;
            return storedResult;
        }

        state.SuccessfulCaptureCount++;
        return storedResult;
    }

    private async Task<Result<ProfilingSnapshot>> CaptureRateBaselineAsync(
        CollectionState state,
        CancellationToken cancellationToken
    )
    {
        var elapsed = state.GetElapsed(this.timeProvider);
        var request = new ProfilingCaptureRequest(
            state.Session,
            state.Node,
            state.SuccessfulCaptureCount + 1,
            elapsed,
            elapsed,
            state.SkippedCaptureCount,
            state.FailedCaptureCount
        );

        try
        {
            return await this
                .probe.CaptureAsync(request, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Failure<ProfilingSnapshot>(
                new ProfilingUnavailableError(
                    $"The profiling rate baseline failed: {exception.GetType().Name}."
                )
            );
        }
    }

    private async Task<Result<CollectionState>> PrepareStateAsync(
        ProfilingSession session,
        ProfilingNodeRole requestedRole,
        CancellationToken cancellationToken
    )
    {
        if (this.broadcastRegistry is null || this.broadcastIdentityProvider is null)
        {
            return Failure<CollectionState>(
                new ProfilingUnavailableError(
                    "The existing Broadcast node registration is unavailable."
                )
            );
        }

        var registration = await this
            .broadcastRegistry.FindAsync(
                this.broadcastIdentityProvider.GetNodeIdentity(),
                cancellationToken
            )
            .ConfigureAwait(false);
        if (registration is null || !registration.IsActive)
        {
            return Failure<CollectionState>(
                new ProfilingUnavailableError(
                    "The local Broadcast node registration is unavailable."
                )
            );
        }

        var nodeResult = await this
            .nodeIdentityProvider.GetAsync(registration, cancellationToken)
            .ConfigureAwait(false);
        if (nodeResult.IsFailure)
        {
            return CopyFailure<CollectionState, ProfilingNode>(nodeResult);
        }

        var dataResult = await this
            .store.GetSessionDataAsync(session.Identity.Key, cancellationToken)
            .ConfigureAwait(false);
        if (dataResult.IsFailure)
        {
            return CopyFailure<CollectionState, ProfilingSessionData>(dataResult);
        }

        var node = nodeResult.Value;
        var existingParticipation = dataResult.Value.Participations.FirstOrDefault(item =>
            item.NodeId == node.Identity.Id
        );
        var role = existingParticipation?.Role ?? requestedRole;
        var joinedUtc = existingParticipation?.JoinedUtc ?? this.timeProvider.GetUtcNow();
        var successfulCount = Math.Max(
            existingParticipation?.SuccessfulCaptureCount ?? 0,
            dataResult
                .Value.Snapshots.Where(snapshot => snapshot.NodeId == node.Identity.Id)
                .Select(snapshot => snapshot.Sequence)
                .DefaultIfEmpty()
                .Max()
        );
        var state = new CollectionState(
            session,
            node,
            role,
            this.timeProvider.GetTimestamp(),
            ClampElapsed(this.timeProvider.GetUtcNow() - session.StartedUtc, session.Duration),
            successfulCount,
            existingParticipation?.SkippedCaptureCount ?? 0,
            existingParticipation?.FailedCaptureCount ?? 0,
            this.options.FinalizationGracePeriod
        );

        var contextResult = await this
            .store.AddRuntimeContextAsync(
                this.contextFactory.Create(session, node),
                cancellationToken
            )
            .ConfigureAwait(false);
        if (contextResult.IsFailure)
        {
            return CopyFailure<CollectionState, ProfilingRuntimeContext>(contextResult);
        }

        var participationResult = await this
            .store.UpsertParticipationAsync(
                state.CreateParticipation(
                    ProfilingParticipationState.Collecting,
                    joinedUtc,
                    null,
                    null
                ),
                cancellationToken
            )
            .ConfigureAwait(false);
        if (participationResult.IsFailure)
        {
            return CopyFailure<CollectionState, ProfilingNodeParticipation>(participationResult);
        }

        state.JoinedUtc = participationResult.Value.JoinedUtc;
        return Result<CollectionState>.Success(state);
    }

    private async Task StopStateAsync(
        CollectionState state,
        ProfilingParticipationState terminalState,
        string failure,
        CancellationToken cancellationToken
    )
    {
        state.RequestedTerminalState = terminalState;
        state.Failure = failure;
        state.Cancellation.Cancel();
        await state.Completion.WaitAsync(cancellationToken).ConfigureAwait(false);
        await state.CaptureGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        state.CaptureGate.Release();
        await this.CompleteParticipationAsync(state, terminalState, failure).ConfigureAwait(false);
    }

    private async Task CompleteParticipationAsync(
        CollectionState state,
        ProfilingParticipationState terminalState,
        string failure
    )
    {
        var completedUtc = this.timeProvider.GetUtcNow();
        await this
            .store.UpsertParticipationAsync(
                state.CreateParticipation(terminalState, state.JoinedUtc, completedUtc, failure)
            )
            .ConfigureAwait(false);
    }

    private IResultError ValidateCollectableSession(ProfilingSession session)
    {
        var validation = this.ValidateCaptureSession(session);
        if (validation is not null)
        {
            return validation;
        }

        return session.State != ProfilingSessionState.Running
            ? new ProfilingInvalidStateError(
                "Only a running profiling session can start local collection."
            )
            : null;
    }

    private IResultError ValidateCaptureSession(ProfilingSession session)
    {
        if (
            session is null
            || session.Identity.Id == Guid.Empty
            || session.SamplingInterval < ProfilingOptions.MinimumSamplingInterval
            || session.Duration <= TimeSpan.Zero
        )
        {
            return new ProfilingValidationError(
                "A valid profiling session with supported timing is required."
            );
        }

        return this.timeProvider.GetUtcNow() > session.EndsUtc
            ? new ProfilingInvalidStateError("The profiling session collection window has elapsed.")
            : null;
    }

    private static TimeSpan ClampElapsed(TimeSpan elapsed, TimeSpan duration) =>
        elapsed < TimeSpan.Zero ? TimeSpan.Zero
        : elapsed > duration ? duration
        : elapsed;

    private static TimeSpan NextAbsoluteOpportunity(TimeSpan elapsed, TimeSpan interval)
    {
        var completedIntervals = elapsed.Ticks / interval.Ticks;
        var nextTicks = checked((completedIntervals + 1) * interval.Ticks);
        return TimeSpan.FromTicks(nextTicks);
    }

    private static Result<T> Failure<T>(IResultError error) => Result<T>.Failure().WithError(error);

    private static Result<TTarget> CopyFailure<TTarget, TSource>(Result<TSource> source) =>
        Result<TTarget>.Failure().WithErrors(source.Errors).WithMessages(source.Messages);

    private sealed class CollectionState(
        ProfilingSession session,
        ProfilingNode node,
        ProfilingNodeRole role,
        long startedTimestamp,
        TimeSpan baseElapsed,
        long successfulCaptureCount,
        long skippedCaptureCount,
        long failedCaptureCount,
        TimeSpan finalizationGracePeriod
    )
    {
        public ProfilingSession Session { get; } = session;

        public ProfilingNode Node { get; } = node;

        public ProfilingNodeRole Role { get; } = role;

        public long StartedTimestamp { get; } = startedTimestamp;

        public TimeSpan BaseElapsed { get; } = baseElapsed;

        public long SuccessfulCaptureCount { get; set; } = successfulCaptureCount;

        public long SkippedCaptureCount { get; set; } = skippedCaptureCount;

        public long FailedCaptureCount { get; set; } = failedCaptureCount;

        public TimeSpan FinalizationGracePeriod { get; } = finalizationGracePeriod;

        public DateTimeOffset JoinedUtc { get; set; }

        public ProfilingParticipationState? RequestedTerminalState { get; set; }

        public string Failure { get; set; }

        public CancellationTokenSource Cancellation { get; } = new();

        public SemaphoreSlim CaptureGate { get; } = new(1, 1);

        public Task Completion { get; set; } = Task.CompletedTask;

        public TimeSpan GetElapsed(TimeProvider timeProvider) =>
            this.BaseElapsed
            + timeProvider.GetElapsedTime(this.StartedTimestamp, timeProvider.GetTimestamp());

        public ProfilingNodeParticipation CreateParticipation(
            ProfilingParticipationState state,
            DateTimeOffset joinedUtc,
            DateTimeOffset? completedUtc,
            string failure
        ) =>
            new()
            {
                SessionId = this.Session.Identity.Id,
                SessionKey = this.Session.Identity.Key,
                NodeId = this.Node.Identity.Id,
                NodeKey = this.Node.Identity.Key,
                Role = this.Role,
                State = state,
                JoinedUtc = joinedUtc,
                CompletedUtc = completedUtc,
                SuccessfulCaptureCount = this.SuccessfulCaptureCount,
                SkippedCaptureCount = this.SkippedCaptureCount,
                FailedCaptureCount = this.FailedCaptureCount,
                Failure = failure,
            };
    }
}
