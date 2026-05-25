// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

using System.Reflection;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides a deterministic orchestration test harness built on the in-memory orchestration runtime.
/// </summary>
public class OrchestrationTestHarness : IAsyncDisposable
{
    private readonly IServiceProvider serviceProvider;
    private readonly InMemoryOrchestrationExecutor executor;
    private readonly IOrchestrationService runtime;
    private readonly IOrchestrationQueryStore queries;

    internal OrchestrationTestHarness(IServiceProvider serviceProvider, FakeOrchestrationClock clock)
    {
        this.serviceProvider = serviceProvider;
        this.executor = serviceProvider.GetRequiredService<InMemoryOrchestrationExecutor>();
        this.runtime = serviceProvider.GetRequiredService<IOrchestrationService>();
        this.queries = serviceProvider.GetRequiredService<IOrchestrationQueryStore>();
        this.Clock = clock;
    }

    /// <summary>
    /// Gets the fake clock used by the harness.
    /// </summary>
    public FakeOrchestrationClock Clock { get; }

    /// <summary>
    /// Creates a new orchestration test harness builder.
    /// </summary>
    /// <returns>The harness builder.</returns>
    public static OrchestrationTestHarnessBuilder CreateBuilder()
    {
        return new OrchestrationTestHarnessBuilder();
    }

    /// <summary>
    /// Executes an orchestration inline.
    /// </summary>
    /// <typeparam name="TOrchestration">The orchestration type.</typeparam>
    /// <typeparam name="TData">The orchestration data type.</typeparam>
    /// <param name="data">The orchestration data.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The execution result.</returns>
    public Task<Result<OrchestrationExecuteResult>> ExecuteAsync<TOrchestration, TData>(
        TData data,
        CancellationToken cancellationToken = default)
        where TOrchestration : class, IOrchestration<TData>
        where TData : class, IOrchestrationData
    {
        return this.runtime.ExecuteAsync<TOrchestration, TData>(data, cancellationToken);
    }

    /// <summary>
    /// Dispatches an orchestration and advances it synchronously until it reaches a stable waiting or terminal boundary.
    /// </summary>
    /// <typeparam name="TOrchestration">The orchestration type.</typeparam>
    /// <typeparam name="TData">The orchestration data type.</typeparam>
    /// <param name="data">The orchestration data.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The orchestration instance identifier.</returns>
    public async Task<Result<Guid>> DispatchAsync<TOrchestration, TData>(
        TData data,
        CancellationToken cancellationToken = default)
        where TOrchestration : class, IOrchestration<TData>
        where TData : class, IOrchestrationData
    {
        try
        {
            var instanceId = await this.executor.CreateInstanceForTestingAsync<TOrchestration, TData>(data, cancellationToken).ConfigureAwait(false);
            await this.executor.ContinueInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false);
            return Result<Guid>.Success(instanceId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result<Guid>.Failure().WithError(new Error("Harness dispatch was canceled."));
        }
        catch (Exception exception)
        {
            return Result<Guid>.Failure().WithError(new Error(exception.Message));
        }
    }

    /// <summary>
    /// Delivers a signal to an orchestration instance.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="signalName">The signal name.</param>
    /// <param name="payload">The optional payload.</param>
    /// <param name="idempotencyKey">The optional idempotency key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The signal result.</returns>
    public Task<Result> SignalAsync(
        Guid instanceId,
        string signalName,
        object payload = null,
        string idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        return this.runtime.SignalAsync(instanceId, signalName, payload, idempotencyKey, cancellationToken);
    }

    /// <summary>
    /// Pauses an orchestration instance.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="reason">The optional pause reason.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result.</returns>
    public Task<Result> PauseAsync(Guid instanceId, string reason = null, CancellationToken cancellationToken = default)
    {
        return this.runtime.PauseAsync(instanceId, reason, cancellationToken);
    }

    /// <summary>
    /// Resumes an orchestration instance.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result.</returns>
    public Task<Result> ResumeAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        return this.runtime.ResumeAsync(instanceId, cancellationToken);
    }

    /// <summary>
    /// Cancels an orchestration instance.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="reason">The optional cancellation reason.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result.</returns>
    public Task<Result> CancelAsync(Guid instanceId, string reason = null, CancellationToken cancellationToken = default)
    {
        return this.runtime.CancelAsync(instanceId, reason, cancellationToken);
    }

    /// <summary>
    /// Terminates an orchestration instance.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="reason">The optional termination reason.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result.</returns>
    public Task<Result> TerminateAsync(Guid instanceId, string reason = null, CancellationToken cancellationToken = default)
    {
        return this.runtime.TerminateAsync(instanceId, reason, cancellationToken);
    }

    /// <summary>
    /// Advances the fake clock and processes any instances affected by the new time.
    /// </summary>
    /// <param name="duration">The duration to advance.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes after affected instances have been advanced.</returns>
    public async Task AdvanceTimeAsync(TimeSpan duration, CancellationToken cancellationToken = default)
    {
        this.Clock.Advance(duration);
        await this.ContinueAllAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Advances the fake clock to the specified UTC time and processes any affected instances.
    /// </summary>
    /// <param name="utcNow">The target UTC timestamp.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes after affected instances have been advanced.</returns>
    public async Task AdvanceTimeToAsync(DateTimeOffset utcNow, CancellationToken cancellationToken = default)
    {
        this.Clock.AdvanceTo(utcNow);
        await this.ContinueAllAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Continues a persisted orchestration instance deterministically.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when continuation finishes.</returns>
    public Task ContinueAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        return this.executor.ContinueInstanceAsync(instanceId, cancellationToken);
    }

    /// <summary>
    /// Continues all non-terminal orchestration instances.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes after all continuations have run.</returns>
    public async Task ContinueAllAsync(CancellationToken cancellationToken = default)
    {
        for (var pass = 0; pass < 10; pass++)
        {
            var result = await this.queries.QueryAsync(new OrchestrationInstanceQuery { Take = int.MaxValue }, cancellationToken).ConfigureAwait(false);
            var active = result.Items.Where(item => !IsTerminal(item.Status)).ToArray();

            if (active.Length == 0)
            {
                return;
            }

            foreach (var snapshot in active)
            {
                await this.executor.ContinueInstanceAsync(snapshot.InstanceId, cancellationToken).ConfigureAwait(false);
            }

            var updated = await this.queries.QueryAsync(new OrchestrationInstanceQuery { Take = int.MaxValue }, cancellationToken).ConfigureAwait(false);
            if (updated.Items.Where(item => !IsTerminal(item.Status)).All(item => item.Status is not OrchestrationStatus.Running))
            {
                return;
            }
        }

        throw new InvalidOperationException("The orchestration test harness could not reach a stable boundary within the allotted continuation passes.");
    }

    /// <summary>
    /// Loads the persisted orchestration instance snapshot.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The snapshot when found; otherwise <c>null</c>.</returns>
    public Task<OrchestrationInstanceSnapshot> GetInstanceAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        return this.queries.GetInstanceAsync(instanceId, cancellationToken);
    }

    /// <summary>
    /// Loads the rehydrated orchestration context.
    /// </summary>
    /// <typeparam name="TData">The orchestration data type.</typeparam>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rehydrated context when found; otherwise <c>null</c>.</returns>
    public Task<OrchestrationContext<TData>> GetContextAsync<TData>(Guid instanceId, CancellationToken cancellationToken = default)
        where TData : class, IOrchestrationData
    {
        return this.queries.GetContextAsync<TData>(instanceId, this.serviceProvider, cancellationToken);
    }

    /// <summary>
    /// Loads the persisted execution history for an orchestration instance.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The persisted history entries.</returns>
    public Task<IReadOnlyCollection<OrchestrationHistoryEntry>> GetHistoryAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        return this.queries.GetHistoryAsync(instanceId, cancellationToken);
    }

    /// <summary>
    /// Loads the persisted signal records for an orchestration instance.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The persisted signal records.</returns>
    public Task<IReadOnlyCollection<OrchestrationSignalRecord>> GetSignalsAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        return this.queries.GetSignalsAsync(instanceId, cancellationToken);
    }

    /// <summary>
    /// Loads the persisted timer records for an orchestration instance.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The persisted timer records.</returns>
    public Task<IReadOnlyCollection<OrchestrationTimerRecord>> GetTimersAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        return this.queries.GetTimersAsync(instanceId, cancellationToken);
    }

    /// <summary>
    /// Creates a fluent assertion helper for an orchestration instance.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <returns>The assertion helper.</returns>
    public OrchestrationTestHarnessAssertions Assert(Guid instanceId)
    {
        return new OrchestrationTestHarnessAssertions(this, instanceId);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (this.serviceProvider is IAsyncDisposable asyncDisposable)
        {
            return asyncDisposable.DisposeAsync();
        }

        if (this.serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    private static bool IsTerminal(OrchestrationStatus status)
    {
        return status is OrchestrationStatus.Completed or OrchestrationStatus.Cancelled or OrchestrationStatus.Terminated or OrchestrationStatus.Failed;
    }
}

/// <summary>
/// Builds an <see cref="OrchestrationTestHarness"/> instance.
/// </summary>
public class OrchestrationTestHarnessBuilder
{
    private readonly List<Action<OrchestrationBuilderContext>> orchestrationRegistrations = [];
    private readonly List<Action<OrchestrationBuilderContext>> behaviorRegistrations = [];
    private readonly List<Action<IServiceCollection>> serviceConfigurations = [];
    private FakeOrchestrationClock clock = new();

    /// <summary>
    /// Registers a code-first orchestration with the harness.
    /// </summary>
    /// <typeparam name="TOrchestration">The orchestration type.</typeparam>
    /// <returns>The builder instance.</returns>
    public OrchestrationTestHarnessBuilder WithOrchestration<TOrchestration>()
        where TOrchestration : class
    {
        this.orchestrationRegistrations.Add(context => context.WithOrchestration<TOrchestration>());
        return this;
    }

    /// <summary>
    /// Registers an orchestration behavior with the harness using a behavior type.
    /// </summary>
    /// <typeparam name="TBehavior">The behavior type.</typeparam>
    /// <param name="behavior">An optional pre-instantiated behavior.</param>
    /// <returns>The builder instance.</returns>
    public OrchestrationTestHarnessBuilder WithBehavior<TBehavior>(IOrchestrationBehavior behavior = null)
        where TBehavior : class, IOrchestrationBehavior
    {
        this.behaviorRegistrations.Add(context => context.WithBehavior<TBehavior>(behavior));
        return this;
    }

    /// <summary>
    /// Registers an orchestration behavior with the harness using a factory method.
    /// </summary>
    /// <param name="implementationFactory">The behavior factory.</param>
    /// <returns>The builder instance.</returns>
    public OrchestrationTestHarnessBuilder WithBehavior(Func<IServiceProvider, IOrchestrationBehavior> implementationFactory)
    {
        ArgumentNullException.ThrowIfNull(implementationFactory);
        this.behaviorRegistrations.Add(context => context.WithBehavior(implementationFactory));
        return this;
    }

    /// <summary>
    /// Registers a pre-instantiated orchestration behavior with the harness.
    /// </summary>
    /// <param name="behavior">The behavior instance.</param>
    /// <returns>The builder instance.</returns>
    public OrchestrationTestHarnessBuilder WithBehavior(IOrchestrationBehavior behavior)
    {
        ArgumentNullException.ThrowIfNull(behavior);
        this.behaviorRegistrations.Add(context => context.WithBehavior(behavior));
        return this;
    }

    /// <summary>
    /// Configures additional services required by the orchestration under test.
    /// </summary>
    /// <param name="configure">The service configuration delegate.</param>
    /// <returns>The builder instance.</returns>
    public OrchestrationTestHarnessBuilder ConfigureServices(Action<IServiceCollection> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        this.serviceConfigurations.Add(configure);
        return this;
    }

    /// <summary>
    /// Uses the specified fake clock for the harness.
    /// </summary>
    /// <param name="clock">The fake clock.</param>
    /// <returns>The builder instance.</returns>
    public OrchestrationTestHarnessBuilder UseClock(FakeOrchestrationClock clock)
    {
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        return this;
    }

    /// <summary>
    /// Builds the configured orchestration test harness.
    /// </summary>
    /// <returns>The built harness.</returns>
    public OrchestrationTestHarness Build()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new OrchestrationExecutionSettings { EnableBackgroundExecution = false });
        services.AddSingleton<IOrchestrationClock>(this.clock);

        foreach (var configure in this.serviceConfigurations)
        {
            configure(services);
        }

        var context = services.AddOrchestrations();
        foreach (var register in this.behaviorRegistrations)
        {
            register(context);
        }

        foreach (var register in this.orchestrationRegistrations)
        {
            register(context);
        }

        return new OrchestrationTestHarness(BuildServiceProvider(services), this.clock);
    }

    private static IServiceProvider BuildServiceProvider(IServiceCollection services)
    {
        var extensionsType = Type.GetType(
            "Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions, Microsoft.Extensions.DependencyInjection",
            throwOnError: false);
        var buildMethod = extensionsType?
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(method =>
                string.Equals(method.Name, "BuildServiceProvider", StringComparison.Ordinal) &&
                method.GetParameters() is [{ ParameterType: var parameterType }] &&
                parameterType.IsAssignableFrom(typeof(IServiceCollection)));

        if (buildMethod is null)
        {
            throw new InvalidOperationException("The DI service provider implementation assembly is not available.");
        }

        return (IServiceProvider)buildMethod.Invoke(null, [services])!;
    }
}

/// <summary>
/// Provides fluent assertions over a persisted orchestration instance.
/// </summary>
public class OrchestrationTestHarnessAssertions
{
    private readonly OrchestrationTestHarness harness;
    private readonly Guid instanceId;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationTestHarnessAssertions"/> class.
    /// </summary>
    /// <param name="harness">The owning harness.</param>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    public OrchestrationTestHarnessAssertions(OrchestrationTestHarness harness, Guid instanceId)
    {
        this.harness = harness ?? throw new ArgumentNullException(nameof(harness));
        this.instanceId = instanceId;
    }

    /// <summary>
    /// Asserts the persisted orchestration status.
    /// </summary>
    /// <param name="expected">The expected status.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The current assertion helper.</returns>
    public async Task<OrchestrationTestHarnessAssertions> HaveStatusAsync(
        OrchestrationStatus expected,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await this.RequireSnapshotAsync(cancellationToken).ConfigureAwait(false);
        if (snapshot.Status != expected)
        {
            throw new InvalidOperationException($"Expected orchestration instance '{this.instanceId}' to have status '{expected}', but found '{snapshot.Status}'.");
        }

        return this;
    }

    /// <summary>
    /// Asserts the persisted current state.
    /// </summary>
    /// <param name="expected">The expected state name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The current assertion helper.</returns>
    public async Task<OrchestrationTestHarnessAssertions> HaveCurrentStateAsync(
        string expected,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await this.RequireSnapshotAsync(cancellationToken).ConfigureAwait(false);
        if (!string.Equals(snapshot.CurrentState, expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Expected orchestration instance '{this.instanceId}' to be in state '{expected}', but found '{snapshot.CurrentState}'.");
        }

        return this;
    }

    /// <summary>
    /// Asserts that the orchestration is waiting and optionally in the specified state.
    /// </summary>
    /// <param name="currentState">The optional expected waiting state.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The current assertion helper.</returns>
    public async Task<OrchestrationTestHarnessAssertions> BeWaitingAsync(
        string currentState = null,
        CancellationToken cancellationToken = default)
    {
        await this.HaveStatusAsync(OrchestrationStatus.Waiting, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(currentState))
        {
            await this.HaveCurrentStateAsync(currentState, cancellationToken).ConfigureAwait(false);
        }

        return this;
    }

    /// <summary>
    /// Asserts that the orchestration has completed and optionally in the specified final state.
    /// </summary>
    /// <param name="currentState">The optional expected state.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The current assertion helper.</returns>
    public async Task<OrchestrationTestHarnessAssertions> BeCompletedAsync(
        string currentState = null,
        CancellationToken cancellationToken = default)
    {
        await this.HaveStatusAsync(OrchestrationStatus.Completed, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(currentState))
        {
            await this.HaveCurrentStateAsync(currentState, cancellationToken).ConfigureAwait(false);
        }

        return this;
    }

    /// <summary>
    /// Asserts that the orchestration history contains the specified event type.
    /// </summary>
    /// <param name="eventType">The expected event type.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The current assertion helper.</returns>
    public async Task<OrchestrationTestHarnessAssertions> HaveHistoryEventAsync(
        string eventType,
        CancellationToken cancellationToken = default)
    {
        var history = await this.harness.GetHistoryAsync(this.instanceId, cancellationToken).ConfigureAwait(false);
        if (!history.Any(item => string.Equals(item.EventType, eventType, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Expected orchestration instance '{this.instanceId}' to contain history event '{eventType}'.");
        }

        return this;
    }

    /// <summary>
    /// Asserts the number of retry history events.
    /// </summary>
    /// <param name="expectedCount">The expected retry event count.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The current assertion helper.</returns>
    public async Task<OrchestrationTestHarnessAssertions> HaveRetryCountAsync(
        int expectedCount,
        CancellationToken cancellationToken = default)
    {
        var history = await this.harness.GetHistoryAsync(this.instanceId, cancellationToken).ConfigureAwait(false);
        var actual = history.Count(item => string.Equals(item.EventType, "ActivityRetried", StringComparison.OrdinalIgnoreCase));
        if (actual != expectedCount)
        {
            throw new InvalidOperationException($"Expected orchestration instance '{this.instanceId}' to have '{expectedCount}' retry events, but found '{actual}'.");
        }

        return this;
    }

    /// <summary>
    /// Asserts the number of compensation-related history events.
    /// </summary>
    /// <param name="expectedCount">The expected compensation event count.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The current assertion helper.</returns>
    public async Task<OrchestrationTestHarnessAssertions> HaveCompensationCountAsync(
        int expectedCount,
        CancellationToken cancellationToken = default)
    {
        var history = await this.harness.GetHistoryAsync(this.instanceId, cancellationToken).ConfigureAwait(false);
        var actual = history.Count(item => item.EventType?.Contains("Compensat", StringComparison.OrdinalIgnoreCase) == true);
        if (actual != expectedCount)
        {
            throw new InvalidOperationException($"Expected orchestration instance '{this.instanceId}' to have '{expectedCount}' compensation events, but found '{actual}'.");
        }

        return this;
    }

    /// <summary>
    /// Executes additional caller-provided assertions against the rehydrated orchestration context.
    /// </summary>
    /// <typeparam name="TData">The orchestration data type.</typeparam>
    /// <param name="assert">The assertion callback.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The current assertion helper.</returns>
    public async Task<OrchestrationTestHarnessAssertions> MatchContextAsync<TData>(
        Action<OrchestrationContext<TData>> assert,
        CancellationToken cancellationToken = default)
        where TData : class, IOrchestrationData
    {
        ArgumentNullException.ThrowIfNull(assert);

        var context = await this.harness.GetContextAsync<TData>(this.instanceId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Orchestration instance '{this.instanceId}' was not found.");

        assert(context);
        return this;
    }

    private async Task<OrchestrationInstanceSnapshot> RequireSnapshotAsync(CancellationToken cancellationToken)
    {
        return await this.harness.GetInstanceAsync(this.instanceId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Orchestration instance '{this.instanceId}' was not found.");
    }
}
