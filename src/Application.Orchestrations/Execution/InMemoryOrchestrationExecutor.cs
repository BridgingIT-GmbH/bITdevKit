// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

using System.Collections.Concurrent;
using System.Reflection;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Executes orchestrations using persisted in-memory runtime semantics.
/// </summary>
public class InMemoryOrchestrationExecutor : IOrchestrationExecutor, IOrchestrationService
{
    private const string ActivityIndexPropertyName = "__orchestration.activity.index";
    private const string CompensationStackPropertyName = "__orchestration.compensation.stack";
    private const string PauseStatusPropertyName = "__orchestration.pause.previous-status";
    private const string PauseReasonPropertyName = "__orchestration.pause.reason";
    private const string RetryStatePropertyPrefix = "__orchestration.retry.";
    private const string WaitReasonPropertyName = "__orchestration.wait.reason";
    private const string WaitSignalNamesPropertyName = "__orchestration.wait.signal-names";
    private const string WaitStartedPropertyName = "__orchestration.wait.started-utc";
    private const string DelayTimerTriggerKind = "WaitDelay";
    private const string StateTimerTriggerKind = "StateTimeout";
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(1);

    private readonly IServiceProvider serviceProvider;
    private readonly OrchestrationRegistrationStore registrations;
    private readonly IOrchestrationStorageProvider persistenceProvider;
    private readonly IOrchestrationClock clock;
    private readonly OrchestrationExecutionSettings executionSettings;
    private readonly ILogger<InMemoryOrchestrationExecutor> logger;
    private readonly ConcurrentDictionary<Guid, byte> scheduledTimerWatchers = new ConcurrentDictionary<Guid, byte>();
    private readonly AsyncLocal<ActivityCheckpointSession> currentCheckpointSession = new();

    private class LeaseHandle
    {
        public LeaseHandle(OrchestrationLease lease)
        {
            this.Lease = lease;
        }

        public OrchestrationLease Lease { get; set; }
    }

    private sealed class ActivitySnapshotHolder
    {
        public ActivitySnapshotHolder(OrchestrationInstanceSnapshot snapshot)
        {
            this.Snapshot = snapshot;
        }

        public OrchestrationInstanceSnapshot Snapshot { get; set; }
    }

    private sealed class ActivityCheckpointSession
    {
        public object Context { get; init; }

        public LeaseHandle Lease { get; init; }

        public ActivitySnapshotHolder SnapshotHolder { get; init; }
    }

    private class OrchestrationLeaseException : InvalidOperationException
    {
        public OrchestrationLeaseException(string message, Exception innerException = null)
            : base(message, innerException)
        {
        }
    }

    private class OrchestrationLeaseConflictException : OrchestrationLeaseException
    {
        public OrchestrationLeaseConflictException(string message, Exception innerException = null)
            : base(message, innerException)
        {
        }
    }

    private class OrchestrationLeaseLostException : OrchestrationLeaseException
    {
        public OrchestrationLeaseLostException(string message, Exception innerException = null)
            : base(message, innerException)
        {
        }
    }

    private sealed record ActivityExecutionResolution(ActivityExecutionResolutionKind Kind, OrchestrationOutcome Outcome = null, string Reason = null);

    private enum ActivityExecutionResolutionKind
    {
        Continue,
        RetryImmediate,
        Deferred,
        Outcome,
        Fail,
    }

    private sealed record ActivityRetryState
    {
        public int RetryCount { get; init; }

        public DateTimeOffset? NextAttemptUtc { get; init; }

        public string Reason { get; init; }
    }

    private sealed record CompensationRegistration
    {
        public string StateName { get; init; }

        public string ActivityName { get; init; }

        public string CompensationName { get; init; }
    }

    private sealed record CompensationExecutionResult(bool Failed, OrchestrationInstanceSnapshot Snapshot);

    private sealed record BehaviorExecutionRequest<TData>(
        OrchestrationContext<TData> Context,
        string StateName,
        string ActivityName,
        OrchestrationActivityExecutionKind Kind,
        int Attempt,
        Func<Task<OrchestrationOutcome>> Execute)
        where TData : class, IOrchestrationData;

    /// <summary>
    /// Describes the outcome of a recovery action executed by the orchestration runtime.
    /// </summary>
    public enum RecoveryActionResult
    {
        None,
        Continued,
        Repaired,
        SkippedLeaseConflict,
    }

    /// <summary>
    /// Represents the result of a recovery action together with the number of affected timers when applicable.
    /// </summary>
    /// <param name="Result">The recovery action outcome.</param>
    /// <param name="AffectedTimerCount">The number of timers repaired or affected by the action.</param>
    public sealed record RecoveryActionOutcome(RecoveryActionResult Result, int AffectedTimerCount = 0);

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryOrchestrationExecutor" /> class.
    /// </summary>
    /// <param name="serviceProvider">The root service provider.</param>
    public InMemoryOrchestrationExecutor(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        this.registrations = serviceProvider.GetRequiredService<OrchestrationRegistrationStore>();
        this.persistenceProvider = serviceProvider.GetRequiredService<IOrchestrationStorageProvider>();
        this.clock = serviceProvider.GetRequiredService<IOrchestrationClock>();
        this.executionSettings = serviceProvider.GetService<OrchestrationExecutionSettings>() ?? new OrchestrationExecutionSettings();
        this.logger = (serviceProvider.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance).CreateLogger<InMemoryOrchestrationExecutor>();
    }

    /// <inheritdoc />
    async Task<OrchestrationContext<TData>> IOrchestrationExecutor.ExecuteAsync<TOrchestration, TData>(
        TData data,
        CancellationToken cancellationToken)
    {
        return await this.ExecuteInlineContextAsync<TOrchestration, TData>(data, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Result<OrchestrationExecuteResult>> ExecuteAsync<TOrchestration, TData>(
        TData data,
        CancellationToken cancellationToken = default)
        where TOrchestration : class, IOrchestration<TData>
        where TData : class, IOrchestrationData
    {
        try
        {
            var context = await this.ExecuteInlineContextAsync<TOrchestration, TData>(data, cancellationToken).ConfigureAwait(false);

            if (context.Status is OrchestrationStatus.Waiting or OrchestrationStatus.Paused)
            {
                return Result<OrchestrationExecuteResult>.Failure()
                    .WithError(new Error("Inline execution cannot complete because the orchestration reached a waiting or paused state."));
            }

            var snapshot = await this.persistenceProvider.Instances.GetAsync(context.InstanceId, cancellationToken).ConfigureAwait(false);
            return Result<OrchestrationExecuteResult>.Success(this.BuildExecuteResult(context, snapshot?.SerializedContext));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result<OrchestrationExecuteResult>.Failure()
                .WithError(new Error("Inline orchestration execution was canceled."));
        }
        catch (Exception exception)
        {
            return Result<OrchestrationExecuteResult>.Failure()
                .WithError(new Error(exception.Message));
        }
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> DispatchAsync<TOrchestration, TData>(
        TData data,
        CancellationToken cancellationToken = default)
        where TOrchestration : class, IOrchestration<TData>
        where TData : class, IOrchestrationData
    {
        ArgumentNullException.ThrowIfNull(data);

        try
        {
            var instanceId = await this.CreateInstanceAsync<TOrchestration, TData>(data, cancellationToken).ConfigureAwait(false);
            this.ScheduleBackgroundAdvance<TOrchestration, TData>(instanceId);

            return Result<Guid>.Success(instanceId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result<Guid>.Failure().WithError(new Error("Orchestration dispatch was canceled."));
        }
        catch (Exception exception)
        {
            return Result<Guid>.Failure().WithError(new Error(exception.Message));
        }
    }

    /// <inheritdoc />
    public async Task<Result<OrchestrationWaitResult>> DispatchAndWaitAsync<TOrchestration, TData>(
        TData data,
        OrchestrationWaitFor waitFor = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
        where TOrchestration : class, IOrchestration<TData>
        where TData : class, IOrchestrationData
    {
        try
        {
            var dispatch = await this.DispatchAsync<TOrchestration, TData>(data, cancellationToken).ConfigureAwait(false);
            if (dispatch.IsFailure)
            {
                return Result<OrchestrationWaitResult>.Failure().WithErrors(dispatch.Errors).WithMessages(dispatch.Messages);
            }

            var effectiveWaitFor = waitFor ?? WaitFor.Completion();
            var deadline = timeout.HasValue ? this.clock.UtcNow.Add(timeout.Value) : (DateTimeOffset?)null;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!this.executionSettings.EnableBackgroundExecution)
                {
                    await this.ContinueInstanceAsync(dispatch.Value, cancellationToken).ConfigureAwait(false);
                }

                var snapshot = await this.persistenceProvider.Instances.GetAsync(dispatch.Value, cancellationToken).ConfigureAwait(false);
                if (snapshot is null)
                {
                    return Result<OrchestrationWaitResult>.Failure()
                        .WithError(new Error($"Orchestration instance '{dispatch.Value}' was not found."));
                }

                var outcome = await this.GetOutcomeAsync(dispatch.Value, snapshot.OrchestrationName, cancellationToken).ConfigureAwait(false);
                if (this.MatchesWaitCondition(snapshot, outcome, effectiveWaitFor))
                {
                    return Result<OrchestrationWaitResult>.Success(this.BuildWaitResult(snapshot, outcome, timedOut: false));
                }

                if (deadline.HasValue && this.clock.UtcNow >= deadline.Value)
                {
                    await this.ContinueInstanceAsync(dispatch.Value, cancellationToken).ConfigureAwait(false);

                    snapshot = await this.persistenceProvider.Instances.GetAsync(dispatch.Value, cancellationToken).ConfigureAwait(false) ?? snapshot;
                    outcome = await this.GetOutcomeAsync(dispatch.Value, snapshot.OrchestrationName, cancellationToken).ConfigureAwait(false);
                    if (this.MatchesWaitCondition(snapshot, outcome, effectiveWaitFor))
                    {
                        return Result<OrchestrationWaitResult>.Success(this.BuildWaitResult(snapshot, outcome, timedOut: false));
                    }

                    return Result<OrchestrationWaitResult>.Success(this.BuildWaitResult(snapshot, outcome, timedOut: true));
                }

                await this.clock.DelayAsync(TimeSpan.FromMilliseconds(20), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result<OrchestrationWaitResult>.Failure()
                .WithError(new Error("DispatchAndWait was canceled."));
        }
        catch (Exception exception)
        {
            return Result<OrchestrationWaitResult>.Failure()
                .WithError(new Error(exception.Message));
        }
    }

    /// <inheritdoc />
    public async Task<Result> SignalAsync(
        Guid instanceId,
        string signalName,
        object payload = null,
        string idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signalName);

        try
        {
            var registration = await this.ResolveRegistrationAsync(instanceId, cancellationToken).ConfigureAwait(false);
            return await this.InvokeResultOperationAsync(
                    nameof(this.SignalInstanceAsync),
                    registration.OrchestrationType,
                    registration.DataType,
                    instanceId,
                    signalName,
                    payload,
                    idempotencyKey,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure().WithError(new Error("Signal dispatch was canceled."));
        }
        catch (Exception exception)
        {
            return Result.Failure().WithError(new Error(exception.Message));
        }
    }

    /// <inheritdoc />
    public async Task<Result> PauseAsync(
        Guid instanceId,
        string reason = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var registration = await this.ResolveRegistrationAsync(instanceId, cancellationToken).ConfigureAwait(false);
            return await this.InvokeResultOperationAsync(
                    nameof(this.PauseInstanceAsync),
                    registration.OrchestrationType,
                    registration.DataType,
                    instanceId,
                    reason,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure().WithError(new Error("Pause was canceled."));
        }
        catch (Exception exception)
        {
            return Result.Failure().WithError(new Error(exception.Message));
        }
    }

    /// <inheritdoc />
    public async Task<Result> ResumeAsync(
        Guid instanceId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var registration = await this.ResolveRegistrationAsync(instanceId, cancellationToken).ConfigureAwait(false);
            return await this.InvokeResultOperationAsync(
                    nameof(this.ResumeInstanceAsync),
                    registration.OrchestrationType,
                    registration.DataType,
                    instanceId,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure().WithError(new Error("Resume was canceled."));
        }
        catch (Exception exception)
        {
            return Result.Failure().WithError(new Error(exception.Message));
        }
    }

    /// <inheritdoc />
    public async Task<Result> CancelAsync(
        Guid instanceId,
        string reason = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var registration = await this.ResolveRegistrationAsync(instanceId, cancellationToken).ConfigureAwait(false);
            return await this.InvokeResultOperationAsync(
                    nameof(this.CancelInstanceAsync),
                    registration.OrchestrationType,
                    registration.DataType,
                    instanceId,
                    reason,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure().WithError(new Error("Cancel was canceled."));
        }
        catch (Exception exception)
        {
            return Result.Failure().WithError(new Error(exception.Message));
        }
    }

    /// <inheritdoc />
    public async Task<Result> TerminateAsync(
        Guid instanceId,
        string reason = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var registration = await this.ResolveRegistrationAsync(instanceId, cancellationToken).ConfigureAwait(false);
            return await this.InvokeResultOperationAsync(
                    nameof(this.TerminateInstanceAsync),
                    registration.OrchestrationType,
                    registration.DataType,
                    instanceId,
                    reason,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure().WithError(new Error("Terminate was canceled."));
        }
        catch (Exception exception)
        {
            return Result.Failure().WithError(new Error(exception.Message));
        }
    }

    private async Task<OrchestrationContext<TData>> ExecuteInlineContextAsync<TOrchestration, TData>(
        TData data,
        CancellationToken cancellationToken)
        where TOrchestration : class, IOrchestration<TData>
        where TData : class, IOrchestrationData
    {
        ArgumentNullException.ThrowIfNull(data);

        if (!this.registrations.Contains(typeof(TOrchestration)))
        {
            throw new InvalidOperationException($"Orchestration '{typeof(TOrchestration).Name}' is not registered.");
        }

        await using var scope = this.serviceProvider.CreateAsyncScope();
        var orchestration = scope.ServiceProvider.GetRequiredService<TOrchestration>();
        this.registrations.RegisterName(orchestration.Name, typeof(TOrchestration));
        var orchestrationDefinition = this.RequireCodeFirstOrchestration(orchestration);

        var context = new OrchestrationContext<TData>(
            orchestration.Name,
            data,
            scope.ServiceProvider,
            startedUtc: this.clock.UtcNow);
        using var logScope = this.BeginOrchestrationScope(context.InstanceId, context.OrchestrationName, context.CurrentState);

        this.logger.LogDebug(
            "{LogKey} executing orchestration inline (instanceId={InstanceId}, orchestration={Orchestration})",
            Constants.LogKey,
            context.InstanceId,
            orchestration.Name);

        try
        {
            var definition = orchestrationDefinition.GetDefinition();
            var snapshot = await this.persistenceProvider.Instances.CreateAsync(context, cancellationToken: cancellationToken).ConfigureAwait(false);
            await this.AppendHistoryAsync(context.InstanceId, "Created", null, null, context.OrchestrationName, cancellationToken).ConfigureAwait(false);

            await this.WithLeaseAsync(
                    context.InstanceId,
                    lease => this.ProcessContextAsync(definition, context, snapshot, lease, cancellationToken),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OrchestrationLeaseException exception)
        {
            this.logger.LogWarning(
                exception,
                "{LogKey} inline orchestration execution lost lease access (instanceId={InstanceId}, orchestration={Orchestration})",
                Constants.LogKey,
                context.InstanceId,
                context.OrchestrationName);
            context.Status = OrchestrationStatus.Failed;
            context.FailureReason = exception.Message;
            context.CompletedUtc ??= this.clock.UtcNow;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            this.logger.LogError(
                exception,
                "{LogKey} inline orchestration execution failed (instanceId={InstanceId}, orchestration={Orchestration}, state={State}, activity={Activity})",
                Constants.LogKey,
                context.InstanceId,
                context.OrchestrationName,
                context.CurrentState,
                context.CurrentActivity);
            context.Status = OrchestrationStatus.Failed;
            context.FailureReason = exception.Message;
            context.CompletedUtc ??= this.clock.UtcNow;

            var snapshot = await this.persistenceProvider.Instances.GetAsync(context.InstanceId, cancellationToken).ConfigureAwait(false);
            if (snapshot is not null)
            {
                await this.persistenceProvider.Instances.SaveAsync(snapshot, context, cancellationToken).ConfigureAwait(false);
                await this.AppendHistoryAsync(context.InstanceId, "Failed", context.CurrentState, context.CurrentActivity, context.FailureReason, cancellationToken).ConfigureAwait(false);
            }
        }

        return context;
    }

    private async Task<Guid> CreateInstanceAsync<TOrchestration, TData>(
        TData data,
        CancellationToken cancellationToken)
        where TOrchestration : class, IOrchestration<TData>
        where TData : class, IOrchestrationData
    {
        ArgumentNullException.ThrowIfNull(data);

        if (!this.registrations.Contains(typeof(TOrchestration)))
        {
            throw new InvalidOperationException($"Orchestration '{typeof(TOrchestration).Name}' is not registered.");
        }

        await using var scope = this.serviceProvider.CreateAsyncScope();
        var orchestration = scope.ServiceProvider.GetRequiredService<TOrchestration>();
        this.registrations.RegisterName(orchestration.Name, typeof(TOrchestration));

        _ = this.RequireCodeFirstOrchestration(orchestration).GetDefinition();

        var context = new OrchestrationContext<TData>(
            orchestration.Name,
            data,
            scope.ServiceProvider,
            startedUtc: this.clock.UtcNow);
        using var logScope = this.BeginOrchestrationScope(context.InstanceId, context.OrchestrationName, context.CurrentState);

        this.logger.LogDebug(
            "{LogKey} created orchestration instance (instanceId={InstanceId}, orchestration={Orchestration})",
            Constants.LogKey,
            context.InstanceId,
            orchestration.Name);

        await this.persistenceProvider.Instances.CreateAsync(context, cancellationToken: cancellationToken).ConfigureAwait(false);
        await this.AppendHistoryAsync(context.InstanceId, "Created", null, null, context.OrchestrationName, cancellationToken).ConfigureAwait(false);

        return context.InstanceId;
    }

    internal Task<Guid> CreateInstanceForTestingAsync<TOrchestration, TData>(
        TData data,
        CancellationToken cancellationToken = default)
        where TOrchestration : class, IOrchestration<TData>
        where TData : class, IOrchestrationData
    {
        return this.CreateInstanceAsync<TOrchestration, TData>(data, cancellationToken);
    }

    private void ScheduleBackgroundAdvance<TOrchestration, TData>(Guid instanceId)
        where TOrchestration : class, IOrchestration<TData>
        where TData : class, IOrchestrationData
    {
        if (!this.executionSettings.EnableBackgroundExecution)
        {
            return;
        }

        this.logger.LogDebug(
            "{LogKey} scheduling background orchestration advance (instanceId={InstanceId}, orchestration={Orchestration})",
            Constants.LogKey,
            instanceId,
            typeof(TOrchestration).Name);

        _ = Task.Run(() => this.ContinueInstanceCoreAsync<TOrchestration, TData>(instanceId, CancellationToken.None));
    }

    internal async Task ContinueInstanceAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        _ = await this.ContinueInstanceForRecoveryAsync(instanceId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Continues an orchestration instance as part of timer or recovery processing and reports the recovery outcome.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The recovery action outcome.</returns>
    public async Task<RecoveryActionOutcome> ContinueInstanceForRecoveryAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        var registration = await this.ResolveRegistrationAsync(instanceId, cancellationToken).ConfigureAwait(false);
        return await this.InvokeRecoveryOperationAsync(
                nameof(this.ContinueInstanceCoreWithResultAsync),
                registration.OrchestrationType,
                registration.DataType,
                instanceId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task ContinueInstanceCoreAsync<TOrchestration, TData>(Guid instanceId, CancellationToken cancellationToken)
        where TOrchestration : class, IOrchestration<TData>
        where TData : class, IOrchestrationData
    {
        _ = await this.ContinueInstanceCoreWithResultAsync<TOrchestration, TData>(instanceId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<RecoveryActionOutcome> ContinueInstanceCoreWithResultAsync<TOrchestration, TData>(Guid instanceId, CancellationToken cancellationToken)
        where TOrchestration : class, IOrchestration<TData>
        where TData : class, IOrchestrationData
    {
        await using var scope = this.serviceProvider.CreateAsyncScope();
        var orchestration = scope.ServiceProvider.GetRequiredService<TOrchestration>();
        this.registrations.RegisterName(orchestration.Name, typeof(TOrchestration));
        var orchestrationDefinition = this.RequireCodeFirstOrchestration(orchestration);

        var context = await this.persistenceProvider.Queries.GetContextAsync<TData>(instanceId, scope.ServiceProvider, cancellationToken).ConfigureAwait(false);
        if (context is null)
        {
            return new RecoveryActionOutcome(RecoveryActionResult.None);
        }

        var snapshot = await this.persistenceProvider.Instances.GetAsync(instanceId, cancellationToken).ConfigureAwait(false);
        if (snapshot is null || this.IsTerminal(snapshot.Status))
        {
            return new RecoveryActionOutcome(RecoveryActionResult.None);
        }

        var definition = orchestrationDefinition.GetDefinition();
        var completed = false;
        using var logScope = this.BeginOrchestrationScope(instanceId, orchestration.Name, snapshot.CurrentState);

        try
        {
            this.logger.LogDebug(
                "{LogKey} continuing orchestration instance (instanceId={InstanceId}, orchestration={Orchestration}, status={Status}, state={State})",
                Constants.LogKey,
                instanceId,
                orchestration.Name,
                snapshot.Status,
                snapshot.CurrentState);

            await this.WithLeaseAsync(
                    instanceId,
                    async lease =>
                    {
                        try
                        {
                            await this.ProcessContextAsync(definition, context, snapshot, lease, cancellationToken).ConfigureAwait(false);
                            completed = true;
                        }
                        catch (OrchestrationLeaseLostException)
                        {
                            this.logger.LogDebug(
                                "{LogKey} orchestration continuation stopped because the lease was lost (instanceId={InstanceId}, orchestration={Orchestration})",
                                Constants.LogKey,
                                instanceId,
                                orchestration.Name);
                            return true;
                        }
                        catch (Exception exception) when (exception is not OperationCanceledException)
                        {
                            this.logger.LogError(
                                exception,
                                "{LogKey} orchestration continuation failed and will mark the instance as failed (instanceId={InstanceId}, orchestration={Orchestration}, state={State}, activity={Activity})",
                                Constants.LogKey,
                                instanceId,
                                orchestration.Name,
                                context.CurrentState,
                                context.CurrentActivity);
                            await this.FailContextAsync(definition, context, snapshot, lease, exception.Message, cancellationToken).ConfigureAwait(false);
                        }

                        return true;
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OrchestrationLeaseConflictException)
        {
            this.logger.LogDebug(
                "{LogKey} skipped orchestration continuation because another worker owns the lease (instanceId={InstanceId}, orchestration={Orchestration})",
                Constants.LogKey,
                instanceId,
                orchestration.Name);
            return new RecoveryActionOutcome(RecoveryActionResult.SkippedLeaseConflict);
        }

        if (completed)
        {
            this.logger.LogDebug(
                "{LogKey} orchestration continuation completed (instanceId={InstanceId}, orchestration={Orchestration})",
                Constants.LogKey,
                instanceId,
                orchestration.Name);
        }

        return new RecoveryActionOutcome(completed ? RecoveryActionResult.Continued : RecoveryActionResult.None);
    }

    /// <summary>
    /// Repairs a waiting orchestration instance by recreating any missing durable timers described by its wait plan.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The recovery action outcome.</returns>
    public async Task<RecoveryActionOutcome> RepairWaitingInstanceAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        var snapshot = await this.persistenceProvider.Instances.GetAsync(instanceId, cancellationToken).ConfigureAwait(false);
        if (snapshot is null || snapshot.Status != OrchestrationStatus.Waiting)
        {
            return new RecoveryActionOutcome(RecoveryActionResult.None);
        }

        using var logScope = this.BeginOrchestrationScope(instanceId, snapshot.OrchestrationName, snapshot.CurrentState);

        var waitPlan = this.TryGetWaitPlan(snapshot, out var plan) ? plan : null;
        if (waitPlan is null || waitPlan.ExpectedTimers.Count == 0)
        {
            return new RecoveryActionOutcome(RecoveryActionResult.None);
        }

        try
        {
            return await this.WithLeaseAsync(
                    instanceId,
                    async lease =>
                    {
                        var currentSnapshot = await this.persistenceProvider.Instances.GetAsync(instanceId, cancellationToken).ConfigureAwait(false);
                        if (currentSnapshot is null || currentSnapshot.Status != OrchestrationStatus.Waiting)
                        {
                            return new RecoveryActionOutcome(RecoveryActionResult.None);
                        }

                        var currentPlan = this.TryGetWaitPlan(currentSnapshot, out var latestPlan) ? latestPlan : null;
                        if (currentPlan is null || currentPlan.ExpectedTimers.Count == 0)
                        {
                            return new RecoveryActionOutcome(RecoveryActionResult.None);
                        }

                        var repaired = await this.EnsureWaitPlanTimersScheduledAsync(
                                instanceId,
                                snapshot.OrchestrationName,
                                currentPlan,
                                lease,
                                cancellationToken)
                            .ConfigureAwait(false);

                        if (repaired > 0)
                        {
                            this.logger.LogDebug(
                                "{LogKey} repaired waiting orchestration boundary (instanceId={InstanceId}, orchestration={Orchestration}, state={State}, repairedTimers={TimerCount})",
                                Constants.LogKey,
                                instanceId,
                                snapshot.OrchestrationName,
                                currentSnapshot.CurrentState,
                                repaired);
                        }

                        return repaired > 0
                            ? new RecoveryActionOutcome(RecoveryActionResult.Repaired, repaired)
                            : new RecoveryActionOutcome(RecoveryActionResult.None);
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OrchestrationLeaseConflictException)
        {
            this.logger.LogDebug(
                "{LogKey} skipped waiting-boundary repair because another worker owns the lease (instanceId={InstanceId}, orchestration={Orchestration})",
                Constants.LogKey,
                instanceId,
                snapshot.OrchestrationName);
            return new RecoveryActionOutcome(RecoveryActionResult.SkippedLeaseConflict);
        }
    }

    private async Task ProcessContextAsync<TData>(
        OrchestrationDefinition<TData> definition,
        OrchestrationContext<TData> context,
        OrchestrationInstanceSnapshot snapshot,
        LeaseHandle lease,
        CancellationToken cancellationToken)
        where TData : class, IOrchestrationData
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (this.IsTerminal(context.Status) || context.Status == OrchestrationStatus.Paused)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(context.CurrentState))
            {
                await this.BeginMutatingActionAsync(lease, $"Enter initial state '{definition.InitialState}'", cancellationToken).ConfigureAwait(false);
                snapshot = await this.EnterStateAsync(definition.InitialState, context, snapshot, lease, cancellationToken).ConfigureAwait(false);
            }

            var state = definition.States[context.CurrentState];

            if (context.Status == OrchestrationStatus.Waiting)
            {
                if (await this.TryProcessSignalAsync(definition, state, context, snapshot, lease, cancellationToken).ConfigureAwait(false))
                {
                    snapshot = await this.persistenceProvider.Instances.GetAsync(context.InstanceId, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (await this.TryProcessTimerAsync(state, context, snapshot, lease, cancellationToken).ConfigureAwait(false))
                {
                    snapshot = await this.persistenceProvider.Instances.GetAsync(context.InstanceId, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                return;
            }

            var activityHandled = await this.ExecuteStateActivitiesAsync(definition, state, context, snapshot, lease, cancellationToken).ConfigureAwait(false);
            snapshot = await this.persistenceProvider.Instances.GetAsync(context.InstanceId, cancellationToken).ConfigureAwait(false) ?? snapshot;

            if (activityHandled)
            {
                continue;
            }

            if (this.IsTerminal(context.Status) || context.Status is OrchestrationStatus.Waiting or OrchestrationStatus.Paused)
            {
                return;
            }

            if (await this.TryProcessSignalAsync(definition, state, context, snapshot, lease, cancellationToken).ConfigureAwait(false))
            {
                snapshot = await this.persistenceProvider.Instances.GetAsync(context.InstanceId, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (await this.TryProcessTimerAsync(state, context, snapshot, lease, cancellationToken).ConfigureAwait(false))
            {
                snapshot = await this.persistenceProvider.Instances.GetAsync(context.InstanceId, cancellationToken).ConfigureAwait(false);
                continue;
            }

            var transition = state.Transitions.FirstOrDefault(item => item.Condition(context));
            if (transition is not null)
            {
                await this.BeginMutatingActionAsync(lease, $"Transition orchestration instance '{context.InstanceId}' from '{state.Name}' to '{transition.TargetState}'", cancellationToken).ConfigureAwait(false);
                snapshot = await this.TransitionToStateAsync(state.Name, transition.TargetState, context, snapshot, lease, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (state.TerminalDirectiveKind != OrchestrationTerminalDirectiveKind.None)
            {
                await this.BeginMutatingActionAsync(lease, $"Apply terminal outcome '{state.TerminalDirectiveKind}' for state '{state.Name}'", cancellationToken).ConfigureAwait(false);
                await this.ApplyOutcomeAsync(definition, ToOutcome(state.TerminalDirectiveKind, state.TerminalDirectiveReason), state, context, snapshot, lease, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (state.WaitingSignals.Count > 0 || state.Timers.Count > 0)
            {
                await this.BeginMutatingActionAsync(lease, $"Enter waiting state '{state.Name}'", cancellationToken).ConfigureAwait(false);
                var waitPlan = this.BuildStateWaitPlan(state, context, reason: null);
                await this.EnterWaitingAsync(state, context, snapshot, waitPlan, lease, cancellationToken).ConfigureAwait(false);
                return;
            }

                await this.FailContextAsync(
                    definition,
                    context,
                    snapshot,
                    lease,
                    $"State '{state.Name}' has no matching transition or terminal behavior.",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }
    }

    private async Task<bool> ExecuteStateActivitiesAsync<TData>(
        OrchestrationDefinition<TData> definition,
        OrchestrationStateDefinition<TData> state,
        OrchestrationContext<TData> context,
        OrchestrationInstanceSnapshot snapshot,
        LeaseHandle lease,
        CancellationToken cancellationToken)
        where TData : class, IOrchestrationData
    {
        var activityIndex = this.GetActivityIndex(context);
        var snapshotHolder = new ActivitySnapshotHolder(snapshot);
        while (activityIndex < state.Activities.Count)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var activityDefinition = state.Activities[activityIndex];
            context.CurrentActivity = activityDefinition.Name;
            var activityAttempt = this.GetActivityAttempt(context, state.Name, activityDefinition.Name);
            this.logger.LogDebug(
                "{LogKey} executing orchestration activity (instanceId={InstanceId}, orchestration={Orchestration}, state={State}, activity={Activity}, index={ActivityIndex})",
                Constants.LogKey,
                context.InstanceId,
                context.OrchestrationName,
                state.Name,
                activityDefinition.Name,
                activityIndex);

            try
            {
                while (true)
                {
                    await this.BeginMutatingActionAsync(lease, $"Execute activity '{activityDefinition.Name}'", cancellationToken).ConfigureAwait(false);
                    var previousCheckpointSession = this.currentCheckpointSession.Value;
                    this.currentCheckpointSession.Value = new ActivityCheckpointSession
                    {
                        Context = context,
                        Lease = lease,
                        SnapshotHolder = snapshotHolder,
                    };

                    OrchestrationOutcome outcome;
                    try
                    {
                        outcome = await this.ExecuteActivityWithBehaviorsAsync(
                                new BehaviorExecutionRequest<TData>(
                                    context,
                                    state.Name,
                                    activityDefinition.Name,
                                    OrchestrationActivityExecutionKind.Activity,
                                    activityAttempt,
                                    () => activityDefinition.Factory(context.Services).ExecuteAsync(context, cancellationToken)),
                                cancellationToken)
                            .ConfigureAwait(false);
                    }
                    finally
                    {
                        this.currentCheckpointSession.Value = previousCheckpointSession;
                    }

                    var resolution = await this.ResolveActivityOutcomeAsync(state, activityDefinition, context, outcome, lease, cancellationToken).ConfigureAwait(false);
                    if (resolution.Kind == ActivityExecutionResolutionKind.Fail)
                    {
                        await this.FailContextAsync(definition, context, snapshotHolder.Snapshot, lease, resolution.Reason, cancellationToken).ConfigureAwait(false);
                        return true;
                    }

                    context.LastOutcome = resolution.Outcome ?? outcome;

                    if (resolution.Kind == ActivityExecutionResolutionKind.RetryImmediate)
                    {
                        this.logger.LogDebug(
                            "{LogKey} retrying orchestration activity immediately (instanceId={InstanceId}, orchestration={Orchestration}, state={State}, activity={Activity}, reason={Reason})",
                            Constants.LogKey,
                            context.InstanceId,
                            context.OrchestrationName,
                            context.CurrentState,
                            activityDefinition.Name,
                            context.LastOutcome.Reason);
                        await this.AppendHistoryAsync(
                                lease,
                                context.InstanceId,
                                "ActivityRetried",
                                context.CurrentState,
                                activityDefinition.Name,
                                context.LastOutcome.Reason,
                                cancellationToken)
                            .ConfigureAwait(false);
                        snapshotHolder.Snapshot = await this.SaveSnapshotAsync(snapshotHolder.Snapshot, context, lease, "Persist retry attempt", cancellationToken).ConfigureAwait(false);
                        activityAttempt++;
                        continue;
                    }

                    if (resolution.Kind == ActivityExecutionResolutionKind.Deferred)
                    {
                        this.logger.LogDebug(
                            "{LogKey} scheduled orchestration activity retry (instanceId={InstanceId}, orchestration={Orchestration}, state={State}, activity={Activity}, reason={Reason})",
                            Constants.LogKey,
                            context.InstanceId,
                            context.OrchestrationName,
                            context.CurrentState,
                            activityDefinition.Name,
                            context.LastOutcome.Reason);
                        await this.AppendHistoryAsync(
                                lease,
                                context.InstanceId,
                                "ActivityRetryScheduled",
                                context.CurrentState,
                                activityDefinition.Name,
                                context.LastOutcome.Reason,
                                cancellationToken)
                            .ConfigureAwait(false);
                        await this.ApplyOutcomeAsync(definition, context.LastOutcome, state, context, snapshotHolder.Snapshot, lease, cancellationToken).ConfigureAwait(false);
                        return true;
                    }

                    this.ClearRetryState(context, state.Name, activityDefinition.Name);

                    await this.AppendHistoryAsync(
                            lease,
                            context.InstanceId,
                            "ActivityCompleted",
                            context.CurrentState,
                            activityDefinition.Name,
                            context.LastOutcome.Kind.ToString(),
                            cancellationToken)
                        .ConfigureAwait(false);

                    this.RegisterCompensation(context, state.Name, activityDefinition);

                    activityIndex++;
                    this.SetActivityIndex(context, activityIndex);
                    context.CurrentActivity = null;
                    this.logger.LogDebug(
                        "{LogKey} completed orchestration activity (instanceId={InstanceId}, orchestration={Orchestration}, state={State}, activity={Activity}, outcome={Outcome})",
                        Constants.LogKey,
                        context.InstanceId,
                        context.OrchestrationName,
                        context.CurrentState,
                        activityDefinition.Name,
                        context.LastOutcome.Kind);
                    snapshotHolder.Snapshot = await this.SaveSnapshotAsync(snapshotHolder.Snapshot, context, lease, "Persist activity result", cancellationToken).ConfigureAwait(false);

                    if ((resolution.Outcome ?? outcome).Kind == OrchestrationOutcomeKind.Continue)
                    {
                        break;
                    }

                    await this.ApplyOutcomeAsync(definition, resolution.Outcome ?? outcome, state, context, snapshotHolder.Snapshot, lease, cancellationToken).ConfigureAwait(false);
                    return true;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                this.logger.LogWarning(
                    exception,
                    "{LogKey} orchestration activity failed (instanceId={InstanceId}, orchestration={Orchestration}, state={State}, activity={Activity})",
                    Constants.LogKey,
                    context.InstanceId,
                    context.OrchestrationName,
                    context.CurrentState,
                    activityDefinition.Name);
                await this.AppendHistoryAsync(
                        lease,
                        context.InstanceId,
                        "ActivityFailed",
                        context.CurrentState,
                        activityDefinition.Name,
                        exception.Message,
                        cancellationToken)
                    .ConfigureAwait(false);

                var resolution = await this.ResolveActivityExceptionAsync(state, activityDefinition, context, exception.Message, lease, cancellationToken).ConfigureAwait(false);
                if (resolution.Kind == ActivityExecutionResolutionKind.RetryImmediate)
                {
                    this.logger.LogDebug(
                        "{LogKey} retrying failed orchestration activity immediately (instanceId={InstanceId}, orchestration={Orchestration}, state={State}, activity={Activity}, reason={Reason})",
                        Constants.LogKey,
                        context.InstanceId,
                        context.OrchestrationName,
                        context.CurrentState,
                        activityDefinition.Name,
                        context.LastOutcome.Reason);
                    await this.AppendHistoryAsync(
                            lease,
                            context.InstanceId,
                            "ActivityRetried",
                            context.CurrentState,
                            activityDefinition.Name,
                            context.LastOutcome.Reason,
                            cancellationToken)
                        .ConfigureAwait(false);
                    snapshotHolder.Snapshot = await this.SaveSnapshotAsync(snapshotHolder.Snapshot, context, lease, "Persist retry attempt", cancellationToken).ConfigureAwait(false);
                    activityAttempt++;
                    continue;
                }

                if (resolution.Kind == ActivityExecutionResolutionKind.Deferred)
                {
                    this.logger.LogDebug(
                        "{LogKey} scheduled retry after orchestration activity failure (instanceId={InstanceId}, orchestration={Orchestration}, state={State}, activity={Activity}, reason={Reason})",
                        Constants.LogKey,
                        context.InstanceId,
                        context.OrchestrationName,
                        context.CurrentState,
                        activityDefinition.Name,
                        context.LastOutcome.Reason);
                    await this.AppendHistoryAsync(
                            lease,
                            context.InstanceId,
                            "ActivityRetryScheduled",
                            context.CurrentState,
                            activityDefinition.Name,
                            context.LastOutcome.Reason,
                            cancellationToken)
                        .ConfigureAwait(false);
                    await this.ApplyOutcomeAsync(definition, context.LastOutcome, state, context, snapshotHolder.Snapshot, lease, cancellationToken).ConfigureAwait(false);
                    return true;
                }

                await this.FailContextAsync(definition, context, snapshotHolder.Snapshot, lease, resolution.Reason ?? exception.Message, cancellationToken).ConfigureAwait(false);
                return true;
            }
        }

        context.CurrentActivity = null;
        return false;
    }

    private async Task<bool> TryProcessSignalAsync<TData>(
        OrchestrationDefinition<TData> orchestrationDefinition,
        OrchestrationStateDefinition<TData> state,
        OrchestrationContext<TData> context,
        OrchestrationInstanceSnapshot snapshot,
        LeaseHandle lease,
        CancellationToken cancellationToken)
        where TData : class, IOrchestrationData
    {
        var signals = await this.persistenceProvider.Signals
            .GetProcessableAsync(context.InstanceId, context.CurrentState, cancellationToken)
            .ConfigureAwait(false);

        foreach (var signal in signals)
        {
            var definition = state.SignalHandlers
                .Concat(state.WaitingSignals)
                .FirstOrDefault(item => string.Equals(item.SignalName, signal.SignalName, StringComparison.OrdinalIgnoreCase));

            if (definition is null)
            {
                continue;
            }

            try
            {
                await this.BeginMutatingActionAsync(lease, $"Process signal '{signal.SignalName}'", cancellationToken).ConfigureAwait(false);
                this.logger.LogDebug(
                    "{LogKey} processing orchestration signal (instanceId={InstanceId}, orchestration={Orchestration}, state={State}, signal={SignalName})",
                    Constants.LogKey,
                    context.InstanceId,
                    context.OrchestrationName,
                    context.CurrentState,
                    signal.SignalName);
                var payload = this.DeserializeSignalPayload(signal, definition.PayloadType);
                definition.MapToContextAction?.Invoke(context, payload);
                snapshot = await this.SaveSnapshotAsync(snapshot, context, lease, $"Persist signal '{signal.SignalName}' mapping", cancellationToken).ConfigureAwait(false);

                foreach (var activityDefinition in definition.Activities)
                {
                    await this.BeginMutatingActionAsync(lease, $"Execute signal activity '{activityDefinition.Name}'", cancellationToken).ConfigureAwait(false);
                    context.CurrentActivity = activityDefinition.Name;
                    var outcome = await this.ExecuteActivityWithBehaviorsAsync(
                            new BehaviorExecutionRequest<TData>(
                                context,
                                state.Name,
                                activityDefinition.Name,
                                OrchestrationActivityExecutionKind.SignalActivity,
                                1,
                                () => activityDefinition.Factory(context.Services).ExecuteAsync(context, cancellationToken)),
                            cancellationToken)
                        .ConfigureAwait(false);

                    context.LastOutcome = outcome;
                    context.CurrentActivity = null;
                    snapshot = await this.SaveSnapshotAsync(snapshot, context, lease, $"Persist signal activity '{activityDefinition.Name}' result", cancellationToken).ConfigureAwait(false);

                    await this.AppendHistoryAsync(
                            lease,
                            context.InstanceId,
                            "SignalActivityCompleted",
                            context.CurrentState,
                            activityDefinition.Name,
                            signal.SignalName,
                            cancellationToken)
                        .ConfigureAwait(false);

                    if (outcome.Kind != OrchestrationOutcomeKind.Continue)
                    {
                        await this.UpdateSignalStatusAsync(lease, signal.SignalId, OrchestrationSignalStatus.Processed, signal.SignalName, cancellationToken).ConfigureAwait(false);
                        await this.ApplyOutcomeAsync(orchestrationDefinition, outcome, state, context, snapshot, lease, cancellationToken).ConfigureAwait(false);
                        return true;
                    }
                }

                await this.UpdateSignalStatusAsync(lease, signal.SignalId, OrchestrationSignalStatus.Processed, signal.SignalName, cancellationToken).ConfigureAwait(false);
                await this.AppendHistoryAsync(lease, context.InstanceId, "SignalProcessed", context.CurrentState, null, signal.SignalName, cancellationToken).ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(definition.TargetState))
                {
                    await this.BeginMutatingActionAsync(lease, $"Transition instance '{context.InstanceId}' after signal '{signal.SignalName}'", cancellationToken).ConfigureAwait(false);
                    await this.TransitionToStateAsync(state.Name, definition.TargetState, context, snapshot, lease, cancellationToken).ConfigureAwait(false);
                    return true;
                }

                this.ClearWaitMetadata(context);
                context.Status = OrchestrationStatus.Running;
                snapshot = await this.SaveSnapshotAsync(snapshot, context, lease, $"Resume instance '{context.InstanceId}' after signal '{signal.SignalName}'", cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                this.logger.LogError(
                    exception,
                    "{LogKey} orchestration signal processing failed (instanceId={InstanceId}, orchestration={Orchestration}, state={State}, signal={SignalName})",
                    Constants.LogKey,
                    context.InstanceId,
                    context.OrchestrationName,
                    context.CurrentState,
                    signal.SignalName);
                await this.UpdateSignalStatusAsync(lease, signal.SignalId, OrchestrationSignalStatus.Failed, exception.Message, cancellationToken).ConfigureAwait(false);
                await this.FailContextAsync(orchestrationDefinition, context, snapshot, lease, exception.Message, cancellationToken).ConfigureAwait(false);
                return true;
            }
        }

        return false;
    }

    private async Task<bool> TryProcessTimerAsync<TData>(
        OrchestrationStateDefinition<TData> state,
        OrchestrationContext<TData> context,
        OrchestrationInstanceSnapshot snapshot,
        LeaseHandle lease,
        CancellationToken cancellationToken)
        where TData : class, IOrchestrationData
    {
        var timers = await this.persistenceProvider.Timers.GetDueAsync(this.clock.UtcNow, cancellationToken).ConfigureAwait(false);
        foreach (var timer in timers.Where(item => item.InstanceId == context.InstanceId))
        {
            if (timer.TriggerKind == DelayTimerTriggerKind && !this.IsRelevantDelayTimer(timer, context))
            {
                await this.BeginMutatingActionAsync(lease, $"Obsolete delay timer '{timer.TimerId}'", cancellationToken).ConfigureAwait(false);
                await this.UpdateTimerStatusAsync(lease, timer.TimerId, OrchestrationTimerStatus.Obsolete, "Current state changed.", cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (timer.TriggerKind == StateTimerTriggerKind && !this.IsRelevantStateTimer(timer, context.CurrentState))
            {
                await this.BeginMutatingActionAsync(lease, $"Obsolete state timer '{timer.TimerId}'", cancellationToken).ConfigureAwait(false);
                await this.UpdateTimerStatusAsync(lease, timer.TimerId, OrchestrationTimerStatus.Obsolete, "Current state changed.", cancellationToken).ConfigureAwait(false);
                continue;
            }

            await this.BeginMutatingActionAsync(lease, $"Process timer '{timer.TimerId}'", cancellationToken).ConfigureAwait(false);
            this.logger.LogDebug(
                "{LogKey} processing orchestration timer (instanceId={InstanceId}, orchestration={Orchestration}, state={State}, timerId={TimerId}, trigger={TriggerKind}, targetState={TargetState})",
                Constants.LogKey,
                context.InstanceId,
                context.OrchestrationName,
                context.CurrentState,
                timer.TimerId,
                timer.TriggerKind,
                timer.TargetState);
            await this.UpdateTimerStatusAsync(lease, timer.TimerId, OrchestrationTimerStatus.Consumed, timer.TriggerKind, cancellationToken).ConfigureAwait(false);
            await this.AppendHistoryAsync(lease, context.InstanceId, "TimerConsumed", context.CurrentState, null, timer.TriggerKind, cancellationToken).ConfigureAwait(false);

            if (timer.TriggerKind == DelayTimerTriggerKind)
            {
                this.ClearWaitMetadata(context);
                context.Status = OrchestrationStatus.Running;
                snapshot = await this.SaveSnapshotAsync(snapshot, context, lease, $"Resume instance '{context.InstanceId}' after delay timer", cancellationToken).ConfigureAwait(false);
                return true;
            }

            if (timer.TriggerKind == StateTimerTriggerKind && !string.IsNullOrWhiteSpace(timer.TargetState))
            {
                await this.BeginMutatingActionAsync(lease, $"Transition instance '{context.InstanceId}' after timer '{timer.TimerId}'", cancellationToken).ConfigureAwait(false);
                await this.TransitionToStateAsync(state.Name, timer.TargetState, context, snapshot, lease, cancellationToken).ConfigureAwait(false);
                return true;
            }
        }

        return false;
    }

    private async Task EnterWaitingAsync<TData>(
        OrchestrationStateDefinition<TData> state,
        OrchestrationContext<TData> context,
        OrchestrationInstanceSnapshot snapshot,
        OrchestrationWaitPlan waitPlan,
        LeaseHandle lease,
        CancellationToken cancellationToken)
        where TData : class, IOrchestrationData
    {
        ArgumentNullException.ThrowIfNull(waitPlan);

        context.Status = OrchestrationStatus.Waiting;
        context.CurrentActivity = null;
        context.FailureReason = waitPlan.Reason;
        context.Properties[WaitStartedPropertyName] = waitPlan.StartedUtc;
        OrchestrationRuntimeMetadata.SetWaitPlan(context, waitPlan);

        if (waitPlan.SignalNames.Length > 0)
        {
            context.Properties[WaitSignalNamesPropertyName] = waitPlan.SignalNames;
        }
        else
        {
            context.Properties.Remove(WaitSignalNamesPropertyName);
        }

        if (string.IsNullOrWhiteSpace(waitPlan.Reason))
        {
            context.Properties.Remove(WaitReasonPropertyName);
        }
        else
        {
            context.Properties[WaitReasonPropertyName] = waitPlan.Reason;
        }

        snapshot = await this.SaveSnapshotAsync(snapshot, context, lease, $"Persist waiting state '{state.Name}'", cancellationToken).ConfigureAwait(false);
        await this.AppendHistoryAsync(lease, context.InstanceId, "Waiting", context.CurrentState, null, waitPlan.Reason, cancellationToken).ConfigureAwait(false);
        this.logger.LogDebug(
            "{LogKey} orchestration entered waiting state (instanceId={InstanceId}, orchestration={Orchestration}, state={State}, waitKind={WaitKind}, signals={SignalCount}, timers={TimerCount}, reason={Reason})",
            Constants.LogKey,
            context.InstanceId,
            context.OrchestrationName,
            state.Name,
            waitPlan.Kind,
            waitPlan.SignalNames.Length,
            waitPlan.ExpectedTimers.Count,
            waitPlan.Reason);
        _ = await this.EnsureWaitPlanTimersScheduledAsync(context.InstanceId, context.OrchestrationName, waitPlan, lease, cancellationToken).ConfigureAwait(false);
    }

    private async Task ApplyOutcomeAsync<TData>(
        OrchestrationDefinition<TData> definition,
        OrchestrationOutcome outcome,
        OrchestrationStateDefinition<TData> state,
        OrchestrationContext<TData> context,
        OrchestrationInstanceSnapshot snapshot,
        LeaseHandle lease,
        CancellationToken cancellationToken)
        where TData : class, IOrchestrationData
    {
        context.LastOutcome = outcome;
        context.CurrentActivity = null;

        switch (outcome.Kind)
        {
            case OrchestrationOutcomeKind.Wait:
                this.logger.LogDebug(
                    "{LogKey} orchestration outcome requested wait (instanceId={InstanceId}, orchestration={Orchestration}, state={State}, reason={Reason}, delay={Delay})",
                    Constants.LogKey,
                    context.InstanceId,
                    context.OrchestrationName,
                    state.Name,
                    outcome.Reason,
                    outcome.Delay);
                await this.BeginMutatingActionAsync(lease, $"Enter waiting state '{state.Name}' from outcome '{outcome.Kind}'", cancellationToken).ConfigureAwait(false);
                var waitPlan = this.BuildOutcomeWaitPlan(state, context, outcome);
                await this.EnterWaitingAsync(state, context, snapshot, waitPlan, lease, cancellationToken).ConfigureAwait(false);
                break;

            case OrchestrationOutcomeKind.Complete:
                this.logger.LogInformation(
                    "{LogKey} orchestration completed (instanceId={InstanceId}, orchestration={Orchestration}, state={State}, reason={Reason})",
                    Constants.LogKey,
                    context.InstanceId,
                    context.OrchestrationName,
                    context.CurrentState,
                    outcome.Reason);
                await this.BeginMutatingActionAsync(lease, $"Complete instance '{context.InstanceId}'", cancellationToken).ConfigureAwait(false);
                context.Status = OrchestrationStatus.Completed;
                context.CompletedUtc = this.clock.UtcNow;
                this.ClearWaitMetadata(context);
                await this.ObsoletePendingStateArtifactsAsync(context.InstanceId, context.CurrentState, lease, cancellationToken).ConfigureAwait(false);
                snapshot = await this.SaveSnapshotAsync(snapshot, context, lease, "Persist completion result", cancellationToken).ConfigureAwait(false);
                await this.AppendHistoryAsync(lease, context.InstanceId, "Completed", context.CurrentState, null, outcome.Reason, cancellationToken).ConfigureAwait(false);
                break;

            case OrchestrationOutcomeKind.Cancel:
                this.logger.LogInformation(
                    "{LogKey} orchestration cancellation requested (instanceId={InstanceId}, orchestration={Orchestration}, state={State}, reason={Reason})",
                    Constants.LogKey,
                    context.InstanceId,
                    context.OrchestrationName,
                    context.CurrentState,
                    outcome.Reason);
                await this.BeginMutatingActionAsync(lease, $"Cancel instance '{context.InstanceId}' from outcome", cancellationToken).ConfigureAwait(false);
                var cancelCompensationResult = await this.ExecuteCompensationsAsync(definition, context, snapshot, lease, cancellationToken).ConfigureAwait(false);
                snapshot = cancelCompensationResult.Snapshot;
                context.CompletedUtc ??= this.clock.UtcNow;
                this.ClearWaitMetadata(context);
                await this.ObsoletePendingStateArtifactsAsync(context.InstanceId, context.CurrentState, lease, cancellationToken).ConfigureAwait(false);
                if (cancelCompensationResult.Failed)
                {
                    this.logger.LogError(
                        "{LogKey} orchestration cancellation compensation failed (instanceId={InstanceId}, orchestration={Orchestration}, state={State}, reason={Reason})",
                        Constants.LogKey,
                        context.InstanceId,
                        context.OrchestrationName,
                        context.CurrentState,
                        context.FailureReason);
                    context.Status = OrchestrationStatus.Failed;
                    context.FailureReason = this.BuildCompensationFailureReason("Cancellation", outcome.Reason);
                    snapshot = await this.SaveSnapshotAsync(snapshot, context, lease, "Persist compensation failure after cancellation", cancellationToken).ConfigureAwait(false);
                    await this.AppendHistoryAsync(lease, context.InstanceId, "Failed", context.CurrentState, null, context.FailureReason, cancellationToken).ConfigureAwait(false);
                    break;
                }

                context.Status = OrchestrationStatus.Cancelled;
                context.FailureReason = outcome.Reason;
                snapshot = await this.SaveSnapshotAsync(snapshot, context, lease, "Persist cancellation result", cancellationToken).ConfigureAwait(false);
                await this.AppendHistoryAsync(lease, context.InstanceId, "Cancelled", context.CurrentState, null, outcome.Reason, cancellationToken).ConfigureAwait(false);
                break;

            case OrchestrationOutcomeKind.Terminate:
                this.logger.LogInformation(
                    "{LogKey} orchestration termination requested (instanceId={InstanceId}, orchestration={Orchestration}, state={State}, reason={Reason})",
                    Constants.LogKey,
                    context.InstanceId,
                    context.OrchestrationName,
                    context.CurrentState,
                    outcome.Reason);
                await this.BeginMutatingActionAsync(lease, $"Terminate instance '{context.InstanceId}' from outcome", cancellationToken).ConfigureAwait(false);
                var terminationCompensationResult = await this.ExecuteCompensationsAsync(definition, context, snapshot, lease, cancellationToken).ConfigureAwait(false);
                snapshot = terminationCompensationResult.Snapshot;
                context.CompletedUtc ??= this.clock.UtcNow;
                this.ClearWaitMetadata(context);
                await this.ObsoletePendingStateArtifactsAsync(context.InstanceId, context.CurrentState, lease, cancellationToken).ConfigureAwait(false);
                if (terminationCompensationResult.Failed)
                {
                    this.logger.LogError(
                        "{LogKey} orchestration termination compensation failed (instanceId={InstanceId}, orchestration={Orchestration}, state={State}, reason={Reason})",
                        Constants.LogKey,
                        context.InstanceId,
                        context.OrchestrationName,
                        context.CurrentState,
                        context.FailureReason);
                    context.Status = OrchestrationStatus.Failed;
                    context.FailureReason = this.BuildCompensationFailureReason("Termination", outcome.Reason);
                    snapshot = await this.SaveSnapshotAsync(snapshot, context, lease, "Persist compensation failure after termination", cancellationToken).ConfigureAwait(false);
                    await this.AppendHistoryAsync(lease, context.InstanceId, "Failed", context.CurrentState, null, context.FailureReason, cancellationToken).ConfigureAwait(false);
                    break;
                }

                context.Status = OrchestrationStatus.Terminated;
                context.FailureReason = outcome.Reason;
                snapshot = await this.SaveSnapshotAsync(snapshot, context, lease, "Persist termination result", cancellationToken).ConfigureAwait(false);
                await this.AppendHistoryAsync(lease, context.InstanceId, "Terminated", context.CurrentState, null, outcome.Reason, cancellationToken).ConfigureAwait(false);
                break;

            default:
                await this.FailContextAsync(definition, context, snapshot, lease, $"Unsupported orchestration outcome '{outcome.Kind}'.", cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    private async Task<OrchestrationInstanceSnapshot> EnterStateAsync<TData>(
        string stateName,
        OrchestrationContext<TData> context,
        OrchestrationInstanceSnapshot snapshot,
        LeaseHandle lease,
        CancellationToken cancellationToken)
        where TData : class, IOrchestrationData
    {
        context.CurrentState = stateName;
        context.Status = OrchestrationStatus.Running;
        context.CurrentActivity = null;
        this.SetActivityIndex(context, 0);
        this.ClearWaitMetadata(context);
        var stateVisit = OrchestrationRuntimeMetadata.EnterStateVisit(context, stateName);
        OrchestrationRuntimeMetadata.CleanupStateScopedHelperKeys(context, stateName, stateVisit);

        this.logger.LogDebug(
            "{LogKey} orchestration entered state (instanceId={InstanceId}, orchestration={Orchestration}, state={State}, visit={Visit})",
            Constants.LogKey,
            context.InstanceId,
            context.OrchestrationName,
            stateName,
            stateVisit);

        snapshot = await this.SaveSnapshotAsync(snapshot, context, lease, $"Persist entered state '{stateName}'", cancellationToken).ConfigureAwait(false);
        await this.AppendHistoryAsync(lease, context.InstanceId, "StateEntered", stateName, null, null, cancellationToken).ConfigureAwait(false);
        return snapshot;
    }

    private async Task<OrchestrationInstanceSnapshot> TransitionToStateAsync<TData>(
        string sourceState,
        string targetState,
        OrchestrationContext<TData> context,
        OrchestrationInstanceSnapshot snapshot,
        LeaseHandle lease,
        CancellationToken cancellationToken)
        where TData : class, IOrchestrationData
    {
        this.logger.LogDebug(
            "{LogKey} orchestration transitioned state (instanceId={InstanceId}, orchestration={Orchestration}, fromState={SourceState}, toState={TargetState})",
            Constants.LogKey,
            context.InstanceId,
            context.OrchestrationName,
            sourceState,
            targetState);
        await this.ObsoletePendingStateArtifactsAsync(context.InstanceId, sourceState, lease, cancellationToken).ConfigureAwait(false);
        await this.AppendHistoryAsync(lease, context.InstanceId, "Transitioned", sourceState, null, targetState, cancellationToken).ConfigureAwait(false);
        return await this.EnterStateAsync(targetState, context, snapshot, lease, cancellationToken).ConfigureAwait(false);
    }

    private OrchestrationWaitPlan BuildStateWaitPlan<TData>(
        OrchestrationStateDefinition<TData> state,
        OrchestrationContext<TData> context,
        string reason)
        where TData : class, IOrchestrationData
    {
        var startedUtc = this.clock.UtcNow;
        var signalNames = state.WaitingSignals
            .Select(item => item.SignalName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var timers = state.Timers
            .Select((item, index) => new OrchestrationExpectedTimer
            {
                TriggerKind = StateTimerTriggerKind,
                DueTimeUtc = startedUtc.Add(item.Delay),
                TargetState = item.TargetState,
                Continuation = this.BuildStateTimerContinuation(state.Name, index),
            })
            .ToArray();

        return new OrchestrationWaitPlan
        {
            Kind = timers.Length > 0 ? StateTimerTriggerKind : "SignalWait",
            StateName = state.Name,
            ActivityIndex = this.GetActivityIndex(context),
            Reason = reason,
            StartedUtc = startedUtc,
            SignalNames = signalNames,
            ExpectedTimers = timers,
        };
    }

    private OrchestrationWaitPlan BuildOutcomeWaitPlan<TData>(
        OrchestrationStateDefinition<TData> state,
        OrchestrationContext<TData> context,
        OrchestrationOutcome outcome)
        where TData : class, IOrchestrationData
    {
        var startedUtc = this.clock.UtcNow;
        var expectedTimers = outcome.Delay.HasValue
            ? new[]
            {
                new OrchestrationExpectedTimer
                {
                    TriggerKind = DelayTimerTriggerKind,
                    DueTimeUtc = startedUtc.Add(outcome.Delay.Value),
                    TargetState = context.CurrentState,
                    Continuation = this.BuildDelayTimerContinuation(context.CurrentState, this.GetActivityIndex(context)),
                },
            }
            : Array.Empty<OrchestrationExpectedTimer>();

        return new OrchestrationWaitPlan
        {
            Kind = outcome.Delay.HasValue ? DelayTimerTriggerKind : "ManualWait",
            StateName = state.Name,
            ActivityIndex = this.GetActivityIndex(context),
            Reason = outcome.Reason,
            StartedUtc = startedUtc,
            SignalNames = state.WaitingSignals.Select(item => item.SignalName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            ExpectedTimers = expectedTimers,
        };
    }

    private async Task<int> EnsureWaitPlanTimersScheduledAsync(
        Guid instanceId,
        string orchestrationName,
        OrchestrationWaitPlan waitPlan,
        LeaseHandle lease,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(waitPlan);

        if (waitPlan.ExpectedTimers.Count == 0)
        {
            return 0;
        }

        var created = 0;
        var existing = await this.persistenceProvider.Timers.GetAsync(instanceId, cancellationToken).ConfigureAwait(false);
        foreach (var expectedTimer in waitPlan.ExpectedTimers)
        {
            var pending = existing.FirstOrDefault(item =>
                item.Status == OrchestrationTimerStatus.Pending &&
                string.Equals(item.TriggerKind, expectedTimer.TriggerKind, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.Continuation, expectedTimer.Continuation, StringComparison.Ordinal));

            if (pending is not null)
            {
                this.logger.LogDebug(
                    "{LogKey} reusing pending orchestration timer (instanceId={InstanceId}, orchestration={Orchestration}, state={State}, trigger={TriggerKind}, continuation={Continuation}, timerId={TimerId})",
                    Constants.LogKey,
                    instanceId,
                    orchestrationName,
                    waitPlan.StateName,
                    expectedTimer.TriggerKind,
                    expectedTimer.Continuation,
                    pending.TimerId);
                this.ScheduleTimerWatcher(pending, orchestrationName);
                continue;
            }

            var timer = await this.ScheduleTimerAsync(
                    lease,
                    instanceId,
                    expectedTimer.TriggerKind,
                    expectedTimer.DueTimeUtc,
                    expectedTimer.TargetState,
                    expectedTimer.Continuation,
                    cancellationToken)
                .ConfigureAwait(false);

            await this.AppendHistoryAsync(lease, instanceId, "TimerScheduled", waitPlan.StateName, null, timer.TriggerKind, cancellationToken).ConfigureAwait(false);
            this.logger.LogDebug(
                "{LogKey} scheduled orchestration timer (instanceId={InstanceId}, orchestration={Orchestration}, state={State}, timerId={TimerId}, trigger={TriggerKind}, dueTimeUtc={DueTimeUtc}, continuation={Continuation}, targetState={TargetState})",
                Constants.LogKey,
                instanceId,
                orchestrationName,
                waitPlan.StateName,
                timer.TimerId,
                timer.TriggerKind,
                timer.DueTimeUtc,
                timer.Continuation,
                timer.TargetState);
            this.ScheduleTimerWatcher(timer, orchestrationName);
            created++;
        }

        return created;
    }

    private void ScheduleTimerWatcher(OrchestrationTimerRecord timer, string orchestrationName)
    {
        if (!this.executionSettings.EnableBackgroundExecution)
        {
            return;
        }

        if (!this.scheduledTimerWatchers.TryAdd(timer.TimerId, 0))
        {
            return;
        }

        this.logger.LogDebug(
            "{LogKey} scheduled orchestration timer watcher (instanceId={InstanceId}, timerId={TimerId}, trigger={TriggerKind}, dueTimeUtc={DueTimeUtc}, continuation={Continuation})",
            Constants.LogKey,
            timer.InstanceId,
            timer.TimerId,
            timer.TriggerKind,
            timer.DueTimeUtc,
            timer.Continuation);

        _ = Task.Run(async () =>
        {
            try
            {
                var delay = timer.DueTimeUtc - this.clock.UtcNow;
                if (delay > TimeSpan.Zero)
                {
                    await this.clock.DelayAsync(delay).ConfigureAwait(false);
                }

                if (this.registrations.TryGetByName(orchestrationName, out var orchestrationType))
                {
                    var dataType = this.registrations.GetDataType(orchestrationType);
                    this.logger.LogDebug(
                        "{LogKey} timer watcher continuing orchestration instance (instanceId={InstanceId}, timerId={TimerId}, orchestration={Orchestration})",
                        Constants.LogKey,
                        timer.InstanceId,
                        timer.TimerId,
                        orchestrationName);
                    await this.InvokeVoidOperationAsync(nameof(this.ContinueInstanceCoreAsync), orchestrationType, dataType, timer.InstanceId, CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                this.logger.LogDebug(
                    exception,
                    "{LogKey} orchestration timer watcher stopped unexpectedly (instanceId={InstanceId}, timerId={TimerId}, orchestration={Orchestration})",
                    Constants.LogKey,
                    timer.InstanceId,
                    timer.TimerId,
                    orchestrationName);
            }
            finally
            {
                this.scheduledTimerWatchers.TryRemove(timer.TimerId, out _);
            }
        });
    }

    private async Task ObsoletePendingStateArtifactsAsync(Guid instanceId, string stateName, LeaseHandle lease, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(stateName))
        {
            return;
        }

        var signals = await this.persistenceProvider.Signals.GetAsync(instanceId, cancellationToken).ConfigureAwait(false);
        foreach (var signal in signals.Where(item => item.Status == OrchestrationSignalStatus.Pending && string.Equals(item.CurrentState, stateName, StringComparison.OrdinalIgnoreCase)))
        {
            await this.UpdateSignalStatusAsync(lease, signal.SignalId, OrchestrationSignalStatus.Ignored, "State changed.", cancellationToken).ConfigureAwait(false);
        }

        var timers = await this.persistenceProvider.Timers.GetAsync(instanceId, cancellationToken).ConfigureAwait(false);
        foreach (var timer in timers.Where(item => item.Status == OrchestrationTimerStatus.Pending && this.ReferencesState(item, stateName)))
        {
            await this.UpdateTimerStatusAsync(lease, timer.TimerId, OrchestrationTimerStatus.Obsolete, "State changed.", cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<Result> SignalInstanceAsync<TOrchestration, TData>(
        Guid instanceId,
        string signalName,
        object payload,
        string idempotencyKey,
        CancellationToken cancellationToken)
        where TOrchestration : class, IOrchestration<TData>
        where TData : class, IOrchestrationData
    {
        await using var scope = this.serviceProvider.CreateAsyncScope();
        var orchestration = scope.ServiceProvider.GetRequiredService<TOrchestration>();
        this.registrations.RegisterName(orchestration.Name, typeof(TOrchestration));
        var orchestrationDefinition = this.RequireCodeFirstOrchestration(orchestration);

        var snapshot = await this.persistenceProvider.Instances.GetAsync(instanceId, cancellationToken).ConfigureAwait(false);
        if (snapshot is null)
        {
            return Result.Failure().WithError(new Error($"Orchestration instance '{instanceId}' was not found."));
        }

        if (this.IsTerminal(snapshot.Status))
        {
            return Result.Failure().WithError(new Error($"Orchestration instance '{instanceId}' is already terminal."));
        }

        return await this.WithLeaseAsync(
                instanceId,
                async lease =>
                {
                    var context = await this.persistenceProvider.Queries.GetContextAsync<TData>(instanceId, scope.ServiceProvider, cancellationToken).ConfigureAwait(false);
                    using var logScope = this.BeginOrchestrationScope(instanceId, orchestration.Name, context.CurrentState);
                    var definition = orchestrationDefinition.GetDefinition();
                    var stateName = string.IsNullOrWhiteSpace(context.CurrentState) ? definition.InitialState : context.CurrentState;
                    var state = definition.States[stateName];
                    var matchesSignal = state.SignalHandlers.Concat(state.WaitingSignals)
                        .Any(item => string.Equals(item.SignalName, signalName, StringComparison.OrdinalIgnoreCase));

                    await this.BeginMutatingActionAsync(lease, $"accept signal '{signalName}'", cancellationToken).ConfigureAwait(false);
                    this.logger.LogDebug(
                        "{LogKey} accepted orchestration signal (instanceId={InstanceId}, orchestration={Orchestration}, state={State}, signal={SignalName}, idempotencyKey={IdempotencyKey})",
                        Constants.LogKey,
                        instanceId,
                        orchestration.Name,
                        context.CurrentState,
                        signalName,
                        idempotencyKey);
                    var signal = await this.PersistSignalAsync(
                            lease,
                            instanceId,
                            signalName,
                            payload,
                            context.CurrentState,
                            idempotencyKey,
                            cancellationToken)
                        .ConfigureAwait(false);

                    if (signal.Status != OrchestrationSignalStatus.Pending)
                    {
                        return Result.Success();
                    }

                    await this.AppendHistoryAsync(lease, instanceId, "SignalAccepted", context.CurrentState, null, signalName, cancellationToken).ConfigureAwait(false);

                    if (!matchesSignal)
                    {
                        this.logger.LogWarning(
                            "{LogKey} rejected orchestration signal because no handler matched the current state (instanceId={InstanceId}, orchestration={Orchestration}, state={State}, signal={SignalName})",
                            Constants.LogKey,
                            instanceId,
                            orchestration.Name,
                            context.CurrentState,
                            signalName);
                        await this.UpdateSignalStatusAsync(lease, signal.SignalId, OrchestrationSignalStatus.Rejected, "No matching signal handler for current state.", cancellationToken).ConfigureAwait(false);
                        await this.AppendHistoryAsync(lease, instanceId, "SignalRejected", context.CurrentState, null, signalName, cancellationToken).ConfigureAwait(false);
                        return Result.Success();
                    }

                    if (context.Status == OrchestrationStatus.Paused)
                    {
                        return Result.Success();
                    }

                    await this.ProcessContextAsync(definition, context, snapshot, lease, cancellationToken).ConfigureAwait(false);
                    return Result.Success();
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<Result> PauseInstanceAsync<TOrchestration, TData>(
        Guid instanceId,
        string reason,
        CancellationToken cancellationToken)
        where TOrchestration : class, IOrchestration<TData>
        where TData : class, IOrchestrationData
    {
        return await this.ChangeLifecycleAsync<TData>(
                instanceId,
                cancellationToken,
                context =>
                {
                    if (context.Status == OrchestrationStatus.Paused)
                    {
                        return Result.Failure().WithError(new Error($"Orchestration instance '{instanceId}' is already paused."));
                    }

                    if (this.IsTerminal(context.Status))
                    {
                        return Result.Failure().WithError(new Error($"Orchestration instance '{instanceId}' is already terminal."));
                    }

                    context.Properties[PauseStatusPropertyName] = context.Status.ToString();
                    if (!string.IsNullOrWhiteSpace(reason))
                    {
                        context.Properties[PauseReasonPropertyName] = reason;
                    }

                    context.Status = OrchestrationStatus.Paused;
                    context.FailureReason = reason;
                    return Result.Success();
                },
                "Paused",
                reason,
                obsoletePendingArtifacts: false)
            .ConfigureAwait(false);
    }

    private async Task<Result> ResumeInstanceAsync<TOrchestration, TData>(
        Guid instanceId,
        CancellationToken cancellationToken)
        where TOrchestration : class, IOrchestration<TData>
        where TData : class, IOrchestrationData
    {
        await using var scope = this.serviceProvider.CreateAsyncScope();
        var orchestration = scope.ServiceProvider.GetRequiredService<TOrchestration>();
        var definition = this.RequireCodeFirstOrchestration(orchestration).GetDefinition();

        return await this.WithLeaseAsync(
                instanceId,
                async lease =>
                {
                    var snapshot = await this.persistenceProvider.Instances.GetAsync(instanceId, cancellationToken).ConfigureAwait(false);
                    if (snapshot is null)
                    {
                        return Result.Failure().WithError(new Error($"Orchestration instance '{instanceId}' was not found."));
                    }

                    var context = await this.persistenceProvider.Queries.GetContextAsync<TData>(instanceId, scope.ServiceProvider, cancellationToken).ConfigureAwait(false);
                    if (context.Status != OrchestrationStatus.Paused)
                    {
                        return Result.Failure().WithError(new Error($"Orchestration instance '{instanceId}' is not paused."));
                    }

                    context.Properties.Remove(PauseReasonPropertyName);
                    if (context.Properties.TryGet<string>(PauseStatusPropertyName, out var previousStatusText) &&
                        Enum.TryParse<OrchestrationStatus>(previousStatusText, out var previousStatus))
                    {
                        context.Status = previousStatus;
                    }
                    else
                    {
                        context.Status = OrchestrationStatus.Running;
                    }

                    context.Properties.Remove(PauseStatusPropertyName);
                    await this.BeginMutatingActionAsync(lease, $"resume instance '{instanceId}'", cancellationToken).ConfigureAwait(false);
                    snapshot = await this.SaveSnapshotAsync(snapshot, context, lease, "persist resumed orchestration state", cancellationToken).ConfigureAwait(false);
                    await this.AppendHistoryAsync(lease, instanceId, "Resumed", context.CurrentState, null, null, cancellationToken).ConfigureAwait(false);

                    await this.ProcessContextAsync(definition, context, snapshot, lease, cancellationToken).ConfigureAwait(false);
                    return Result.Success();
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<Result> CancelInstanceAsync<TOrchestration, TData>(
        Guid instanceId,
        string reason,
        CancellationToken cancellationToken)
        where TOrchestration : class, IOrchestration<TData>
        where TData : class, IOrchestrationData
    {
        return await this.ChangeLifecycleAsync<TData>(
                instanceId,
                cancellationToken,
                context =>
                {
                    if (this.IsTerminal(context.Status))
                    {
                        return Result.Failure().WithError(new Error($"Orchestration instance '{instanceId}' is already terminal."));
                    }

                    context.Status = OrchestrationStatus.Cancelled;
                    context.CompletedUtc = this.clock.UtcNow;
                    context.FailureReason = reason;
                    return Result.Success();
                },
                "Cancelled",
                reason)
            .ConfigureAwait(false);
    }

    private async Task<Result> TerminateInstanceAsync<TOrchestration, TData>(
        Guid instanceId,
        string reason,
        CancellationToken cancellationToken)
        where TOrchestration : class, IOrchestration<TData>
        where TData : class, IOrchestrationData
    {
        return await this.ChangeLifecycleAsync<TData>(
                instanceId,
                cancellationToken,
                context =>
                {
                    if (this.IsTerminal(context.Status))
                    {
                        return Result.Failure().WithError(new Error($"Orchestration instance '{instanceId}' is already terminal."));
                    }

                    context.Status = OrchestrationStatus.Terminated;
                    context.CompletedUtc = this.clock.UtcNow;
                    context.FailureReason = reason;
                    return Result.Success();
                },
                "Terminated",
                reason)
            .ConfigureAwait(false);
    }

    private async Task<Result> ChangeLifecycleAsync<TData>(
        Guid instanceId,
        CancellationToken cancellationToken,
        Func<OrchestrationContext<TData>, Result> apply,
        string historyEvent,
        string details,
        bool obsoletePendingArtifacts = true)
        where TData : class, IOrchestrationData
    {
        return await this.WithLeaseAsync(
                instanceId,
                async lease =>
                {
                    var snapshot = await this.persistenceProvider.Instances.GetAsync(instanceId, cancellationToken).ConfigureAwait(false);
                    if (snapshot is null)
                    {
                        return Result.Failure().WithError(new Error($"Orchestration instance '{instanceId}' was not found."));
                    }

                    var context = await this.persistenceProvider.Queries.GetContextAsync<TData>(instanceId, this.serviceProvider, cancellationToken).ConfigureAwait(false);
                    var result = apply(context);
                    if (result.IsFailure)
                    {
                        return result;
                    }

                    await this.BeginMutatingActionAsync(lease, $"apply lifecycle change '{historyEvent}'", cancellationToken).ConfigureAwait(false);
                    if (obsoletePendingArtifacts)
                    {
                        await this.ObsoletePendingStateArtifactsAsync(instanceId, context.CurrentState, lease, cancellationToken).ConfigureAwait(false);
                    }

                    this.ClearWaitMetadata(context);
                    snapshot = await this.SaveSnapshotAsync(snapshot, context, lease, $"persist lifecycle change '{historyEvent}'", cancellationToken).ConfigureAwait(false);
                    await this.AppendHistoryAsync(lease, instanceId, historyEvent, context.CurrentState, null, details, cancellationToken).ConfigureAwait(false);
                    return Result.Success();
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task FailContextAsync<TData>(
        OrchestrationDefinition<TData> definition,
        OrchestrationContext<TData> context,
        OrchestrationInstanceSnapshot snapshot,
        LeaseHandle lease,
        string reason,
        CancellationToken cancellationToken)
        where TData : class, IOrchestrationData
    {
        var compensationResult = await this.ExecuteCompensationsAsync(definition, context, snapshot, lease, cancellationToken).ConfigureAwait(false);
        snapshot = compensationResult.Snapshot;
        context.Status = OrchestrationStatus.Failed;
        context.CompletedUtc ??= this.clock.UtcNow;
        context.FailureReason = compensationResult.Failed ? $"Compensation failure after: {reason}" : reason;
        this.ClearWaitMetadata(context);

        await this.BeginMutatingActionAsync(lease, $"fail instance '{context.InstanceId}'", cancellationToken).ConfigureAwait(false);
        await this.ObsoletePendingStateArtifactsAsync(context.InstanceId, context.CurrentState, lease, cancellationToken).ConfigureAwait(false);
        await this.SaveSnapshotAsync(snapshot, context, lease, "persist failure state", cancellationToken).ConfigureAwait(false);
        await this.AppendHistoryAsync(lease, context.InstanceId, "Failed", context.CurrentState, context.CurrentActivity, reason, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ActivityExecutionResolution> ResolveActivityOutcomeAsync<TData>(
        OrchestrationStateDefinition<TData> state,
        OrchestrationActivityDefinition<TData> activityDefinition,
        OrchestrationContext<TData> context,
        OrchestrationOutcome outcome,
        LeaseHandle lease,
        CancellationToken cancellationToken)
        where TData : class, IOrchestrationData
    {
        if (outcome.Kind != OrchestrationOutcomeKind.Retry)
        {
            return new ActivityExecutionResolution(ActivityExecutionResolutionKind.Outcome, outcome);
        }

        var policy = activityDefinition.RetryPolicy;
        if (policy is null)
        {
            return new ActivityExecutionResolution(ActivityExecutionResolutionKind.RetryImmediate, outcome);
        }

        var retryState = this.GetRetryState(context, state.Name, activityDefinition.Name);
        var retryCount = retryState?.RetryCount + 1 ?? 1;
        var totalAttempts = retryCount + 1;
        if (totalAttempts > policy.MaxAttempts)
        {
            this.ClearRetryState(context, state.Name, activityDefinition.Name);
            return new ActivityExecutionResolution(ActivityExecutionResolutionKind.Fail, Reason: outcome.Reason ?? $"Activity '{activityDefinition.Name}' exhausted its retry policy.");
        }

        var delay = policy.GetDelay(retryCount);
        context.Properties[this.BuildRetryStatePropertyName(state.Name, activityDefinition.Name)] = new ActivityRetryState
        {
            RetryCount = retryCount,
            NextAttemptUtc = this.clock.UtcNow.Add(delay),
            Reason = outcome.Reason,
        };

        if (delay <= TimeSpan.Zero)
        {
            return new ActivityExecutionResolution(ActivityExecutionResolutionKind.RetryImmediate, OrchestrationOutcome.Retry(outcome.Reason));
        }

        return new ActivityExecutionResolution(
            ActivityExecutionResolutionKind.Deferred,
            OrchestrationOutcome.Wait(delay, outcome.Reason ?? $"Retry activity '{activityDefinition.Name}' after durable delay."));
    }

    private async Task<ActivityExecutionResolution> ResolveActivityExceptionAsync<TData>(
        OrchestrationStateDefinition<TData> state,
        OrchestrationActivityDefinition<TData> activityDefinition,
        OrchestrationContext<TData> context,
        string reason,
        LeaseHandle lease,
        CancellationToken cancellationToken)
        where TData : class, IOrchestrationData
    {
        var policy = activityDefinition.RetryPolicy;
        if (policy is null)
        {
            return new ActivityExecutionResolution(ActivityExecutionResolutionKind.Fail, Reason: reason);
        }

        var retryState = this.GetRetryState(context, state.Name, activityDefinition.Name);
        var retryCount = retryState?.RetryCount + 1 ?? 1;
        var totalAttempts = retryCount + 1;
        if (totalAttempts > policy.MaxAttempts)
        {
            this.ClearRetryState(context, state.Name, activityDefinition.Name);
            return new ActivityExecutionResolution(ActivityExecutionResolutionKind.Fail, Reason: reason);
        }

        var delay = policy.GetDelay(retryCount);
        context.Properties[this.BuildRetryStatePropertyName(state.Name, activityDefinition.Name)] = new ActivityRetryState
        {
            RetryCount = retryCount,
            NextAttemptUtc = this.clock.UtcNow.Add(delay),
            Reason = reason,
        };
        context.LastOutcome = delay <= TimeSpan.Zero
            ? OrchestrationOutcome.Retry(reason)
            : OrchestrationOutcome.Wait(delay, reason);

        return delay <= TimeSpan.Zero
            ? new ActivityExecutionResolution(ActivityExecutionResolutionKind.RetryImmediate, context.LastOutcome)
            : new ActivityExecutionResolution(ActivityExecutionResolutionKind.Deferred, context.LastOutcome);
    }

    private ActivityRetryState GetRetryState<TData>(OrchestrationContext<TData> context, string stateName, string activityName)
        where TData : class, IOrchestrationData
    {
        return context.Properties.TryGet<ActivityRetryState>(this.BuildRetryStatePropertyName(stateName, activityName), out var retryState)
            ? retryState
            : null;
    }

    private void ClearRetryState<TData>(OrchestrationContext<TData> context, string stateName, string activityName)
        where TData : class, IOrchestrationData
    {
        context.Properties.Remove(this.BuildRetryStatePropertyName(stateName, activityName));
    }

    private string BuildRetryStatePropertyName(string stateName, string activityName)
    {
        return $"{RetryStatePropertyPrefix}{stateName}:{activityName}";
    }

    private void RegisterCompensation<TData>(OrchestrationContext<TData> context, string stateName, OrchestrationActivityDefinition<TData> activityDefinition)
        where TData : class, IOrchestrationData
    {
        if (activityDefinition.Compensation is null)
        {
            return;
        }

        var stack = this.GetCompensationStack(context);
        stack.Add(new CompensationRegistration
        {
            StateName = stateName,
            ActivityName = activityDefinition.Name,
            CompensationName = activityDefinition.Compensation.Name,
        });
        context.Properties[CompensationStackPropertyName] = stack;
    }

    private List<CompensationRegistration> GetCompensationStack<TData>(OrchestrationContext<TData> context)
        where TData : class, IOrchestrationData
    {
        if (context.Properties.TryGet<List<CompensationRegistration>>(CompensationStackPropertyName, out var stack))
        {
            return stack;
        }

        var created = new List<CompensationRegistration>();
        context.Properties[CompensationStackPropertyName] = created;
        return created;
    }

    private async Task<CompensationExecutionResult> ExecuteCompensationsAsync<TData>(
        OrchestrationDefinition<TData> definition,
        OrchestrationContext<TData> context,
        OrchestrationInstanceSnapshot snapshot,
        LeaseHandle lease,
        CancellationToken cancellationToken)
        where TData : class, IOrchestrationData
    {
        using var logScope = this.BeginOrchestrationScope(context.InstanceId, context.OrchestrationName, context.CurrentState);
        var stack = this.GetCompensationStack(context);
        if (stack.Count == 0)
        {
            return new CompensationExecutionResult(false, snapshot);
        }

        this.logger.LogDebug(
            "{LogKey} executing orchestration compensation stack (instanceId={InstanceId}, orchestration={Orchestration}, state={State}, count={CompensationCount})",
            Constants.LogKey,
            context.InstanceId,
            context.OrchestrationName,
            context.CurrentState,
            stack.Count);

        var failed = false;
        while (stack.Count > 0)
        {
            var registration = stack[^1];
            stack.RemoveAt(stack.Count - 1);
            context.Properties[CompensationStackPropertyName] = stack;
            snapshot = await this.SaveSnapshotAsync(snapshot, context, lease, "persist compensation stack change", cancellationToken).ConfigureAwait(false);

            var state = definition.States[registration.StateName];
            var activity = state.Activities.FirstOrDefault(item => string.Equals(item.Name, registration.ActivityName, StringComparison.Ordinal));
            var compensation = activity?.Compensation;
            if (compensation is null)
            {
                failed = true;
                await this.AppendHistoryAsync(lease, context.InstanceId, "CompensationFailed", registration.StateName, registration.CompensationName, "Compensation definition was not found.", cancellationToken).ConfigureAwait(false);
                continue;
            }

            try
            {
                this.logger.LogDebug(
                    "{LogKey} executing orchestration compensation activity (instanceId={InstanceId}, orchestration={Orchestration}, state={State}, activity={Activity}, compensation={Compensation})",
                    Constants.LogKey,
                    context.InstanceId,
                    context.OrchestrationName,
                    registration.StateName,
                    registration.ActivityName,
                    compensation.Name);
                await this.AppendHistoryAsync(lease, context.InstanceId, "CompensationStarted", registration.StateName, compensation.Name, registration.ActivityName, cancellationToken).ConfigureAwait(false);
                context.CurrentActivity = compensation.Name;
                var compensationAttempt = 1;

                while (true)
                {
                    var outcome = await this.ExecuteActivityWithBehaviorsAsync(
                            new BehaviorExecutionRequest<TData>(
                                context,
                                registration.StateName,
                                compensation.Name,
                                OrchestrationActivityExecutionKind.Compensation,
                                compensationAttempt,
                                () => compensation.Factory(context.Services).ExecuteAsync(context, cancellationToken)),
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (outcome.Kind == OrchestrationOutcomeKind.Retry)
                    {
                        compensationAttempt++;
                        continue;
                    }

                    break;
                }

                context.CurrentActivity = null;
                snapshot = await this.SaveSnapshotAsync(snapshot, context, lease, "persist compensation result", cancellationToken).ConfigureAwait(false);
                await this.AppendHistoryAsync(lease, context.InstanceId, "CompensationCompleted", registration.StateName, compensation.Name, registration.ActivityName, cancellationToken).ConfigureAwait(false);
                this.logger.LogDebug(
                    "{LogKey} completed orchestration compensation activity (instanceId={InstanceId}, orchestration={Orchestration}, state={State}, activity={Activity}, compensation={Compensation})",
                    Constants.LogKey,
                    context.InstanceId,
                    context.OrchestrationName,
                    registration.StateName,
                    registration.ActivityName,
                    compensation.Name);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failed = true;
                this.logger.LogError(
                    exception,
                    "{LogKey} orchestration compensation activity failed (instanceId={InstanceId}, orchestration={Orchestration}, state={State}, activity={Activity}, compensation={Compensation})",
                    Constants.LogKey,
                    context.InstanceId,
                    context.OrchestrationName,
                    registration.StateName,
                    registration.ActivityName,
                    compensation.Name);
                context.CurrentActivity = null;
                snapshot = await this.SaveSnapshotAsync(snapshot, context, lease, "persist compensation failure", cancellationToken).ConfigureAwait(false);
                await this.AppendHistoryAsync(lease, context.InstanceId, "CompensationFailed", registration.StateName, compensation.Name, exception.Message, cancellationToken).ConfigureAwait(false);
            }
        }

        return new CompensationExecutionResult(failed, snapshot);
    }

    private async Task<OrchestrationOutcome> ExecuteActivityWithBehaviorsAsync<TData>(
        BehaviorExecutionRequest<TData> request,
        CancellationToken cancellationToken)
        where TData : class, IOrchestrationData
    {
        var executionContext = new OrchestrationActivityExecutionContext(
            request.Context.InstanceId,
            request.Context.OrchestrationName,
            request.Context.CorrelationId,
            request.StateName,
            request.ActivityName,
            request.Kind,
            request.Attempt,
            request.Context.Services,
            request.Context);

        var behaviors = request.Context.Services.GetServices<IOrchestrationBehavior>().ToArray();
        OrchestrationDelegate next = async () =>
            await request.Execute().ConfigureAwait(false) ?? OrchestrationOutcome.Continue();

        for (var i = behaviors.Length - 1; i >= 0; i--)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var behavior = behaviors[i];
            var currentNext = next;
            next = () => behavior.ExecuteAsync(executionContext, cancellationToken, currentNext);
        }

        return await next().ConfigureAwait(false) ?? OrchestrationOutcome.Continue();
    }

    private int GetActivityAttempt<TData>(OrchestrationContext<TData> context, string stateName, string activityName)
        where TData : class, IOrchestrationData
    {
        return this.GetRetryState(context, stateName, activityName)?.RetryCount + 1 ?? 1;
    }


    private async Task<RegistrationResolution> ResolveRegistrationAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        var snapshot = await this.persistenceProvider.Instances.GetAsync(instanceId, cancellationToken).ConfigureAwait(false);
        if (snapshot is null)
        {
            throw new InvalidOperationException($"Orchestration instance '{instanceId}' was not found.");
        }

        if (!this.registrations.TryGetByName(snapshot.OrchestrationName, out var orchestrationType))
        {
            throw new InvalidOperationException($"Orchestration '{snapshot.OrchestrationName}' is not registered.");
        }

        return new RegistrationResolution(orchestrationType, this.registrations.GetDataType(orchestrationType));
    }

    private async Task<TResult> WithLeaseAsync<TResult>(
        Guid instanceId,
        Func<LeaseHandle, Task<TResult>> action,
        CancellationToken cancellationToken)
    {
        var owner = this.CreateLeaseOwner();
        var lease = new LeaseHandle(await this.AcquireLeaseAsync(instanceId, owner, cancellationToken).ConfigureAwait(false));
        try
        {
            return await action(lease).ConfigureAwait(false);
        }
        finally
        {
            await this.ReleaseLeaseAsync(instanceId, owner, lease, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task WithLeaseAsync(
        Guid instanceId,
        Func<LeaseHandle, Task> action,
        CancellationToken cancellationToken)
    {
        var owner = this.CreateLeaseOwner();
        var lease = new LeaseHandle(await this.AcquireLeaseAsync(instanceId, owner, cancellationToken).ConfigureAwait(false));
        try
        {
            await action(lease).ConfigureAwait(false);
        }
        finally
        {
            await this.ReleaseLeaseAsync(instanceId, owner, lease, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<Result> InvokeResultOperationAsync(
        string methodName,
        Type orchestrationType,
        Type dataType,
        params object[] arguments)
    {
        var method = this.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Method '{methodName}' was not found.");

        var task = (Task<Result>)method.MakeGenericMethod(orchestrationType, dataType)
            .Invoke(this, arguments)!;

        return await task.ConfigureAwait(false);
    }

    private async Task InvokeVoidOperationAsync(
        string methodName,
        Type orchestrationType,
        Type dataType,
        params object[] arguments)
    {
        var method = this.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Method '{methodName}' was not found.");

        var task = (Task)method.MakeGenericMethod(orchestrationType, dataType)
            .Invoke(this, arguments)!;

        await task.ConfigureAwait(false);
    }

    private async Task<RecoveryActionOutcome> InvokeRecoveryOperationAsync(
        string methodName,
        Type orchestrationType,
        Type dataType,
        params object[] arguments)
    {
        var method = this.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Method '{methodName}' was not found.");

        var task = (Task<RecoveryActionOutcome>)method.MakeGenericMethod(orchestrationType, dataType)
            .Invoke(this, arguments)!;

        return await task.ConfigureAwait(false);
    }

    private IDisposable BeginOrchestrationScope(Guid instanceId, string orchestrationName = null, string stateName = null)
    {
        var state = new Dictionary<string, object>
        {
            ["OrchestrationInstanceId"] = instanceId,
        };

        if (!string.IsNullOrWhiteSpace(orchestrationName))
        {
            state["OrchestrationName"] = orchestrationName;
        }

        if (!string.IsNullOrWhiteSpace(stateName))
        {
            state["OrchestrationState"] = stateName;
        }

        return this.logger.BeginScope(state);
    }

    private async Task<string> GetOutcomeAsync(Guid instanceId, string orchestrationName, CancellationToken cancellationToken)
    {
        var snapshot = await this.persistenceProvider.Instances.GetAsync(instanceId, cancellationToken).ConfigureAwait(false);
        if (snapshot is null)
        {
            return null;
        }

        if (!this.registrations.TryGetByName(orchestrationName, out var orchestrationType))
        {
            return OrchestrationRuntimeMetadata.GetEffectiveOutcome(snapshot.Status, null);
        }

        var dataType = this.registrations.GetDataType(orchestrationType);
        var queryMethod = this.persistenceProvider.Queries.GetType().GetMethod(nameof(IOrchestrationQueryStore.GetContextAsync))
            ?? throw new InvalidOperationException("The context query method could not be resolved.");

        await using var scope = this.serviceProvider.CreateAsyncScope();
        var task = (Task)queryMethod.MakeGenericMethod(dataType)
            .Invoke(this.persistenceProvider.Queries, [instanceId, scope.ServiceProvider, cancellationToken])!;

        await task.ConfigureAwait(false);
        var context = task.GetType().GetProperty("Result")?.GetValue(task);
        var lastOutcome = context?.GetType().GetProperty("LastOutcome")?.GetValue(context) as OrchestrationOutcome;
        return OrchestrationRuntimeMetadata.GetEffectiveOutcome(snapshot.Status, lastOutcome);
    }

    private Orchestration<TData> RequireCodeFirstOrchestration<TData>(IOrchestration<TData> orchestration)
        where TData : class, IOrchestrationData
    {
        return orchestration as Orchestration<TData>
            ?? throw new InvalidOperationException($"Orchestration '{orchestration.GetType().Name}' must derive from {typeof(Orchestration<TData>).FullName}.");
    }

    private OrchestrationExecuteResult BuildExecuteResult<TData>(OrchestrationContext<TData> context, string contextJson)
        where TData : class, IOrchestrationData
    {
        return new OrchestrationExecuteResult
        {
            InstanceId = context.InstanceId,
            Status = context.Status.ToString(),
            CurrentState = context.CurrentState,
            Outcome = OrchestrationRuntimeMetadata.GetEffectiveOutcome(context.Status, context.LastOutcome),
            CorrelationId = context.CorrelationId,
            ContextJson = contextJson,
        };
    }

    private OrchestrationWaitResult BuildWaitResult(OrchestrationInstanceSnapshot snapshot, string outcome, bool timedOut)
    {
        return new OrchestrationWaitResult
        {
            InstanceId = snapshot.InstanceId,
            Status = snapshot.Status.ToString(),
            CurrentState = snapshot.CurrentState,
            Outcome = outcome,
            TimedOut = timedOut,
            CompletedUtc = snapshot.CompletedUtc ?? default,
        };
    }

    private bool MatchesWaitCondition(OrchestrationInstanceSnapshot snapshot, string outcome, OrchestrationWaitFor waitFor)
    {
        if (waitFor.Completion && this.IsTerminal(snapshot.Status))
        {
            return true;
        }

        if (waitFor.States?.Any(item => string.Equals(item, snapshot.CurrentState, StringComparison.OrdinalIgnoreCase)) == true)
        {
            return true;
        }

        if (waitFor.Outcomes?.Any(item => string.Equals(item, outcome, StringComparison.OrdinalIgnoreCase)) == true)
        {
            return true;
        }

        return false;
    }

    private object DeserializeSignalPayload(OrchestrationSignalRecord signal, Type payloadType)
    {
        if (payloadType is null || string.IsNullOrWhiteSpace(signal.Payload))
        {
            return null;
        }

        return this.persistenceProvider.Serializer.Deserialize(signal.Payload, payloadType);
    }

    private async Task AppendHistoryAsync(
        Guid instanceId,
        string eventType,
        string stateName,
        string activityName,
        string details,
        CancellationToken cancellationToken)
    {
        await this.persistenceProvider.History
            .AppendAsync(
                new OrchestrationHistoryEntry
                {
                    InstanceId = instanceId,
                    EventType = eventType,
                    StateName = stateName,
                    ActivityName = activityName,
                    Details = details,
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task AppendHistoryAsync(
        LeaseHandle lease,
        Guid instanceId,
        string eventType,
        string stateName,
        string activityName,
        string details,
        CancellationToken cancellationToken)
    {
        await this.EnsureLeaseOwnedAsync(lease, $"append history event '{eventType}'", cancellationToken).ConfigureAwait(false);
        await this.AppendHistoryAsync(instanceId, eventType, stateName, activityName, details, cancellationToken).ConfigureAwait(false);
    }

    private async Task BeginMutatingActionAsync(LeaseHandle lease, string actionName, CancellationToken cancellationToken)
    {
        await this.EnsureLeaseOwnedAsync(lease, actionName, cancellationToken).ConfigureAwait(false);

        try
        {
            lease.Lease = await this.persistenceProvider.Leases
                .RenewAsync(lease.Lease.InstanceId, lease.Lease.LeaseId, lease.Lease.Owner, LeaseDuration, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new OrchestrationLeaseLostException(
                $"The lease for orchestration instance '{lease.Lease.InstanceId}' was lost while attempting to {actionName}.",
                exception);
        }
    }

    private async Task EnsureLeaseOwnedAsync(LeaseHandle lease, string actionName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lease);

        var isOwned = await this.persistenceProvider.Leases
            .VerifyAsync(lease.Lease.InstanceId, lease.Lease.LeaseId, lease.Lease.Owner, cancellationToken)
            .ConfigureAwait(false);

        if (!isOwned)
        {
            throw new OrchestrationLeaseLostException(
                $"The lease for orchestration instance '{lease.Lease.InstanceId}' was lost while attempting to {actionName}.");
        }
    }

    private async Task<OrchestrationLease> AcquireLeaseAsync(Guid instanceId, string owner, CancellationToken cancellationToken)
    {
        this.logger.LogDebug(
            "{LogKey} acquiring orchestration lease (instanceId={InstanceId}, owner={Owner})",
            Constants.LogKey,
            instanceId,
            owner);
        try
        {
            var lease = await this.persistenceProvider.Leases.AcquireAsync(instanceId, owner, LeaseDuration, cancellationToken).ConfigureAwait(false);
            this.logger.LogDebug(
                "{LogKey} acquired orchestration lease (instanceId={InstanceId}, owner={Owner}, leaseId={LeaseId})",
                Constants.LogKey,
                instanceId,
                owner,
                lease.LeaseId);
            return lease;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            this.logger.LogDebug(
                exception,
                "{LogKey} failed to acquire orchestration lease because another worker is active (instanceId={InstanceId}, owner={Owner})",
                Constants.LogKey,
                instanceId,
                owner);
            throw new OrchestrationLeaseConflictException(
                $"An active worker already holds the lease for orchestration instance '{instanceId}'.",
                exception);
        }
    }

    private async Task ReleaseLeaseAsync(Guid instanceId, string owner, LeaseHandle lease, CancellationToken cancellationToken)
    {
        try
        {
            await this.persistenceProvider.Leases
                .ReleaseAsync(instanceId, lease.Lease.LeaseId, owner, cancellationToken)
                .ConfigureAwait(false);
            this.logger.LogDebug(
                "{LogKey} released orchestration lease (instanceId={InstanceId}, owner={Owner}, leaseId={LeaseId})",
                Constants.LogKey,
                instanceId,
                owner,
                lease.Lease.LeaseId);
        }
        catch (Exception exception)
        {
            this.logger.LogDebug(
                exception,
                "{LogKey} failed to release orchestration lease cleanly (instanceId={InstanceId}, owner={Owner}, leaseId={LeaseId})",
                Constants.LogKey,
                instanceId,
                owner,
                lease?.Lease?.LeaseId);
        }
    }

    private async Task<OrchestrationInstanceSnapshot> SaveSnapshotAsync<TData>(
        OrchestrationInstanceSnapshot snapshot,
        OrchestrationContext<TData> context,
        LeaseHandle lease,
        string actionName,
        CancellationToken cancellationToken)
        where TData : class, IOrchestrationData
    {
        await this.EnsureLeaseOwnedAsync(lease, actionName, cancellationToken).ConfigureAwait(false);

        try
        {
            return await this.persistenceProvider.Instances.SaveAsync(snapshot, context, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException exception)
        {
            throw new OrchestrationLeaseLostException(
                $"The orchestration instance '{context.InstanceId}' could not persist '{actionName}' because its lease is no longer authoritative.",
                exception);
        }
    }

    private async Task<OrchestrationSignalRecord> PersistSignalAsync<TPayload>(
        LeaseHandle lease,
        Guid instanceId,
        string signalName,
        TPayload payload,
        string currentState,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await this.EnsureLeaseOwnedAsync(lease, $"persist signal '{signalName}'", cancellationToken).ConfigureAwait(false);
        return await this.persistenceProvider.Signals
            .PersistAsync(instanceId, signalName, payload, currentState, idempotencyKey, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<OrchestrationSignalRecord> UpdateSignalStatusAsync(
        LeaseHandle lease,
        Guid signalId,
        OrchestrationSignalStatus status,
        string reason,
        CancellationToken cancellationToken)
    {
        await this.EnsureLeaseOwnedAsync(lease, $"update signal '{signalId}' to '{status}'", cancellationToken).ConfigureAwait(false);
        return await this.persistenceProvider.Signals.UpdateStatusAsync(signalId, status, reason, cancellationToken).ConfigureAwait(false);
    }

    private async Task<OrchestrationTimerRecord> UpdateTimerStatusAsync(
        LeaseHandle lease,
        Guid timerId,
        OrchestrationTimerStatus status,
        string reason,
        CancellationToken cancellationToken)
    {
        await this.EnsureLeaseOwnedAsync(lease, $"update timer '{timerId}' to '{status}'", cancellationToken).ConfigureAwait(false);
        return await this.persistenceProvider.Timers.UpdateStatusAsync(timerId, status, reason, cancellationToken).ConfigureAwait(false);
    }

    private async Task<OrchestrationTimerRecord> ScheduleTimerAsync(
        LeaseHandle lease,
        Guid instanceId,
        string triggerKind,
        DateTimeOffset dueTimeUtc,
        string targetState,
        string continuation,
        CancellationToken cancellationToken)
    {
        await this.EnsureLeaseOwnedAsync(lease, $"schedule timer '{triggerKind}'", cancellationToken).ConfigureAwait(false);
        return await this.persistenceProvider.Timers
            .ScheduleAsync(instanceId, triggerKind, dueTimeUtc, targetState, continuation, cancellationToken)
            .ConfigureAwait(false);
    }

    private int GetActivityIndex<TData>(OrchestrationContext<TData> context)
        where TData : class, IOrchestrationData
    {
        var value = context.Properties[ActivityIndexPropertyName];
        if (value is not null)
        {
            return value switch
            {
                int intValue => intValue,
                long longValue => checked((int)longValue),
                _ when int.TryParse(value?.ToString(), out var parsed) => parsed,
                _ => 0,
            };
        }

        return 0;
    }

    private void SetActivityIndex<TData>(OrchestrationContext<TData> context, int index)
        where TData : class, IOrchestrationData
    {
        context.Properties[ActivityIndexPropertyName] = index;
    }

    private void ClearWaitMetadata<TData>(OrchestrationContext<TData> context)
        where TData : class, IOrchestrationData
    {
        context.Properties.Remove(WaitReasonPropertyName);
        context.Properties.Remove(WaitSignalNamesPropertyName);
        context.Properties.Remove(WaitStartedPropertyName);
        context.Properties.Remove(OrchestrationRuntimeMetadata.WaitPlanPropertyName);
    }

    private bool TryGetWaitPlan(OrchestrationInstanceSnapshot snapshot, out OrchestrationWaitPlan waitPlan)
    {
        waitPlan = null;
        if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.SerializedContext))
        {
            return false;
        }

        var stored = this.persistenceProvider.Serializer.Deserialize<OrchestrationStoredContextSnapshot>(snapshot.SerializedContext);
        if (stored?.Properties is null ||
            !stored.Properties.TryGetValue(OrchestrationRuntimeMetadata.WaitPlanPropertyName, out var property) ||
            string.IsNullOrWhiteSpace(property.SerializedValue))
        {
            return false;
        }

        var deserialized = this.persistenceProvider.Serializer.Deserialize(property.SerializedValue, typeof(OrchestrationWaitPlan)) as OrchestrationWaitPlan;
        if (deserialized is null)
        {
            return false;
        }

        waitPlan = deserialized;
        return true;
    }

    private string BuildDelayTimerContinuation(string stateName, int activityIndex)
    {
        return $"{DelayTimerTriggerKind}:{stateName}:{activityIndex}";
    }

    private string BuildStateTimerContinuation(string stateName, int index)
    {
        return $"{StateTimerTriggerKind}:{stateName}:{index}";
    }

    private bool IsRelevantDelayTimer<TData>(OrchestrationTimerRecord timer, OrchestrationContext<TData> context)
        where TData : class, IOrchestrationData
    {
        return string.Equals(timer.Continuation, this.BuildDelayTimerContinuation(context.CurrentState, this.GetActivityIndex(context)), StringComparison.Ordinal);
    }

    private bool IsRelevantStateTimer(OrchestrationTimerRecord timer, string currentState)
    {
        return timer.Continuation?.StartsWith($"{StateTimerTriggerKind}:{currentState}:", StringComparison.Ordinal) == true;
    }

    private bool ReferencesState(OrchestrationTimerRecord timer, string stateName)
    {
        return timer.Continuation?.Contains($":{stateName}:", StringComparison.Ordinal) == true;
    }

    private bool IsTerminal(OrchestrationStatus status)
    {
        return status is OrchestrationStatus.Completed or OrchestrationStatus.Failed or OrchestrationStatus.Cancelled or OrchestrationStatus.Terminated;
    }

    internal async Task CheckpointActivityAsync<TData>(
        OrchestrationContext<TData> context,
        string actionName,
        IReadOnlyCollection<OrchestrationHistoryEntry> historyEntries,
        CancellationToken cancellationToken = default)
        where TData : class, IOrchestrationData
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionName);

        var session = this.currentCheckpointSession.Value;
        if (session is null || !ReferenceEquals(session.Context, context))
        {
            throw new InvalidOperationException("Activity checkpoints are only available during active orchestration activity execution.");
        }

        await this.BeginMutatingActionAsync(session.Lease, actionName, cancellationToken).ConfigureAwait(false);
        session.SnapshotHolder.Snapshot = await this.SaveSnapshotAsync(session.SnapshotHolder.Snapshot, context, session.Lease, actionName, cancellationToken).ConfigureAwait(false);

        if (historyEntries is null || historyEntries.Count == 0)
        {
            return;
        }

        foreach (var entry in historyEntries)
        {
            await this.AppendHistoryAsync(
                    context.InstanceId,
                    entry.EventType,
                    entry.StateName,
                    entry.ActivityName,
                    entry.Details,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    internal Task BeginActivityMutationAsync<TData>(
        OrchestrationContext<TData> context,
        string actionName,
        CancellationToken cancellationToken = default)
        where TData : class, IOrchestrationData
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionName);

        var session = this.currentCheckpointSession.Value;
        if (session is null || !ReferenceEquals(session.Context, context))
        {
            throw new InvalidOperationException("Activity mutations are only available during active orchestration activity execution.");
        }

        return this.BeginMutatingActionAsync(session.Lease, actionName, cancellationToken);
    }

    private string CreateLeaseOwner()
    {
        return $"runtime-{Environment.ProcessId}-{Guid.NewGuid():N}";
    }

    private string BuildCompensationFailureReason(string operation, string reason)
    {
        return string.IsNullOrWhiteSpace(reason)
            ? $"{operation} compensation failed."
            : $"{operation} compensation failed: {reason}";
    }

    private static OrchestrationOutcome ToOutcome(OrchestrationTerminalDirectiveKind kind, string reason)
    {
        return kind switch
        {
            OrchestrationTerminalDirectiveKind.Complete => OrchestrationOutcome.Complete(reason),
            OrchestrationTerminalDirectiveKind.Cancel => OrchestrationOutcome.Cancel(reason),
            OrchestrationTerminalDirectiveKind.Terminate => OrchestrationOutcome.Terminate(reason),
            OrchestrationTerminalDirectiveKind.Wait => OrchestrationOutcome.Wait(reason),
            _ => OrchestrationOutcome.Continue(),
        };
    }

    private sealed record RegistrationResolution(Type OrchestrationType, Type DataType);
}
