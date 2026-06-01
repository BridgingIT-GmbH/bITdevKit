// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

/// <summary>
/// Provides an in-memory scheduler test harness with a controllable clock.
/// </summary>
/// <example>
/// <code>
/// using var harness = JobSchedulerTestHarness.Create()
///     .WithClock(new DateTimeOffset(2026, 05, 26, 09, 00, 00, TimeSpan.Zero))
///     .Build();
/// </code>
/// </example>
public sealed class JobSchedulerTestHarness : IDisposable
{
    private readonly ServiceProvider provider;
    private readonly IJobStoreProvider store;

    private JobSchedulerTestHarness(ServiceProvider provider)
    {
        this.provider = provider;
        this.store = provider.GetRequiredService<IJobStoreProvider>();
    }

    /// <summary>
    /// Gets the backing service provider.
    /// </summary>
    public IServiceProvider Services => this.provider;

    /// <summary>
    /// Gets the controllable scheduler clock.
    /// </summary>
    public FakeTimeProvider Clock => (FakeTimeProvider)this.provider.GetRequiredService<TimeProvider>();

    /// <summary>
    /// Gets the runtime control surface.
    /// </summary>
    public IJobSchedulerService Scheduler => this.provider.GetRequiredService<IJobSchedulerService>();

    /// <summary>
    /// Gets the concrete runtime service.
    /// </summary>
    public JobSchedulerService Runtime => this.provider.GetRequiredService<JobSchedulerService>();

    /// <summary>
    /// Gets the hosted background service.
    /// </summary>
    public JobSchedulerBackgroundService Background => this.provider.GetRequiredService<JobSchedulerBackgroundService>();

    /// <summary>
    /// Creates a fluent builder for scheduler tests.
    /// </summary>
    public static JobSchedulerTestHarnessBuilder Create() => new();

    /// <summary>
    /// Creates the scheduler harness directly.
    /// </summary>
    public static JobSchedulerTestHarness Create(
        Action<JobBuilderContext> configureJobs,
        Action<IServiceCollection> configureServices = null,
        Action<JobSchedulerHostedOptions> configureOptions = null,
        DateTimeOffset? nowUtc = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostApplicationLifetime, TestHostApplicationLifetime>();
        services.AddSingleton<TimeProvider>(new FakeTimeProvider(nowUtc ?? new DateTimeOffset(2026, 05, 26, 09, 00, 00, TimeSpan.Zero)));

        var context = services.AddJobScheduler().WithBackgroundExecution(options =>
        {
            options.EnableBackgroundExecution = false;
            configureOptions?.Invoke(options);
        });
        configureJobs?.Invoke(context);
        configureServices?.Invoke(services);

        return new JobSchedulerTestHarness(services.BuildServiceProvider());
    }

    /// <summary>
    /// Advances the clock by the supplied duration.
    /// </summary>
    public void Advance(TimeSpan value)
    {
        this.Clock.Advance(value);
    }

    /// <summary>
    /// Advances the clock to the supplied UTC instant.
    /// </summary>
    public Task AdvanceToAsync(DateTimeOffset targetUtc)
    {
        var delta = targetUtc.ToUniversalTime() - this.Clock.GetUtcNow();
        if (delta > TimeSpan.Zero)
        {
            this.Clock.Advance(delta);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Materializes due trigger occurrences.
    /// </summary>
    public Task<Result<IReadOnlyList<JobOccurrence>>> MaterializeAsync(int maxCatchUpOccurrences = 100, CancellationToken cancellationToken = default)
    {
        return this.Runtime.MaterializeScheduledOccurrencesAsync(this.Clock.GetUtcNow(), maxCatchUpOccurrences, cancellationToken);
    }

    /// <summary>
    /// Materializes due trigger occurrences.
    /// </summary>
    public Task<Result<IReadOnlyList<JobOccurrence>>> MaterializeDueTriggersAsync(int maxCatchUpOccurrences = 100, CancellationToken cancellationToken = default)
    {
        return this.MaterializeAsync(maxCatchUpOccurrences, cancellationToken);
    }

    /// <summary>
    /// Lists ready occurrences.
    /// </summary>
    public Task<IReadOnlyList<JobOccurrence>> ListReadyOccurrencesAsync(CancellationToken cancellationToken = default)
    {
        return this.Runtime.ListReadyOccurrencesAsync(cancellationToken);
    }

    /// <summary>
    /// Dispatches a job without waiting.
    /// </summary>
    public Task<Result<JobDispatchResult>> DispatchAsync<TJob>(object data = null, JobDispatchOptions options = null, CancellationToken cancellationToken = default)
        where TJob : class, IJob
    {
        return this.Scheduler.DispatchAsync<TJob>(data, options, cancellationToken);
    }

    /// <summary>
    /// Dispatches a named job without waiting.
    /// </summary>
    public Task<Result<JobDispatchResult>> DispatchAsync(string jobName, object data = null, JobDispatchOptions options = null, CancellationToken cancellationToken = default)
    {
        return this.Scheduler.DispatchAsync(jobName, data, options, cancellationToken);
    }

    /// <summary>
    /// Dispatches and waits for the terminal execution result.
    /// </summary>
    public Task<Result<JobExecutionResult>> DispatchAndWaitAsync<TJob>(object data = null, JobDispatchOptions options = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        where TJob : class, IJob
    {
        return this.Scheduler.DispatchAndWaitAsync<TJob>(data, options, timeout, cancellationToken);
    }

    /// <summary>
    /// Executes a stored occurrence.
    /// </summary>
    public Task<Result<JobExecutionResult>> ExecuteOccurrenceAsync(Guid occurrenceId, CancellationToken cancellationToken = default)
    {
        return this.Runtime.ExecuteStoredOccurrenceAsync(occurrenceId, cancellationToken);
    }

    /// <summary>
    /// Executes the first ready occurrence.
    /// </summary>
    public async Task<Result<JobExecutionResult>> RunDueAsync(CancellationToken cancellationToken = default)
    {
        var ready = await this.ListReadyOccurrencesAsync(cancellationToken).ConfigureAwait(false);
        var occurrence = ready.OrderBy(x => x.DueUtc).FirstOrDefault();
        return occurrence is null
            ? Result<JobExecutionResult>.Failure().WithError(new ValidationError("There is no due occurrence to execute."))
            : await this.ExecuteOccurrenceAsync(occurrence.OccurrenceId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes all ready occurrences.
    /// </summary>
    public async Task<Result<IReadOnlyList<JobExecutionResult>>> RunAllDueAsync(CancellationToken cancellationToken = default)
    {
        return await this.RunDueBatchAsync(int.MaxValue, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes a bounded batch of ready occurrences.
    /// </summary>
    public async Task<Result<IReadOnlyList<JobExecutionResult>>> RunDueBatchAsync(int maxCount, CancellationToken cancellationToken = default)
    {
        var ready = (await this.ListReadyOccurrencesAsync(cancellationToken).ConfigureAwait(false))
            .OrderBy(x => x.DueUtc)
            .Take(maxCount)
            .ToArray();

        var results = new List<JobExecutionResult>();
        foreach (var occurrence in ready)
        {
            var result = await this.ExecuteOccurrenceAsync(occurrence.OccurrenceId, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                return Result<IReadOnlyList<JobExecutionResult>>.Failure(results).WithErrors(result.Errors);
            }

            results.Add(result.Value);
        }

        return Result<IReadOnlyList<JobExecutionResult>>.Success(results);
    }

    /// <summary>
    /// Executes one scheduler sweep.
    /// </summary>
    public Task SweepAsync(CancellationToken cancellationToken = default)
    {
        return this.Background.SweepOnceAsync(cancellationToken);
    }

    /// <summary>
    /// Loads one occurrence.
    /// </summary>
    public Task<JobOccurrence> GetOccurrenceAsync(Guid occurrenceId, CancellationToken cancellationToken = default)
    {
        return this.store.Occurrences.GetAsync(occurrenceId, cancellationToken);
    }

    /// <summary>
    /// Loads occurrences filtered by job and optional trigger.
    /// </summary>
    public async Task<Result<IReadOnlyList<JobOccurrence>>> GetOccurrencesAsync(string jobName = null, string triggerName = null, CancellationToken cancellationToken = default)
    {
        var occurrences = await this.store.Queries.ListOccurrencesAsync(cancellationToken).ConfigureAwait(false);
        var filtered = occurrences
            .Where(x => string.IsNullOrWhiteSpace(jobName) || string.Equals(x.JobName, jobName, StringComparison.OrdinalIgnoreCase))
            .Where(x => string.IsNullOrWhiteSpace(triggerName) || string.Equals(x.TriggerName, triggerName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.DueUtc)
            .ToArray();

        return Result<IReadOnlyList<JobOccurrence>>.Success(filtered);
    }

    /// <summary>
    /// Finds one occurrence by job and optional trigger.
    /// </summary>
    public async Task<JobOccurrence> FindOccurrenceAsync(string jobName, string triggerName = null, CancellationToken cancellationToken = default)
    {
        var result = await this.GetOccurrencesAsync(jobName, triggerName, cancellationToken).ConfigureAwait(false);
        return result.Value.FirstOrDefault();
    }

    /// <summary>
    /// Gets registered job definitions.
    /// </summary>
    public IReadOnlyList<JobDefinition> GetJobs()
    {
        return this.provider.GetRequiredService<JobRegistrationStore>().GetDefinitions();
    }

    /// <summary>
    /// Gets registered triggers for a job.
    /// </summary>
    public IReadOnlyList<JobTriggerDefinition> GetTriggers(string jobName)
    {
        return this.GetJobs().FirstOrDefault(x => string.Equals(x.JobName, jobName, StringComparison.OrdinalIgnoreCase))?.Triggers ?? [];
    }

    /// <summary>
    /// Gets execution attempts for an occurrence.
    /// </summary>
    public async Task<IReadOnlyList<JobExecution>> GetExecutionsAsync(Guid occurrenceId, CancellationToken cancellationToken = default)
    {
        return await this.store.Executions.ListByOccurrenceAsync(occurrenceId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets retained execution history for an occurrence.
    /// </summary>
    public async Task<IReadOnlyList<JobExecutionHistoryEntry>> GetHistoryAsync(Guid occurrenceId, CancellationToken cancellationToken = default)
    {
        return await this.store.ExecutionHistory.ListAsync(occurrenceId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets active dependencies for an occurrence.
    /// </summary>
    public async Task<IReadOnlyList<JobOccurrenceDependency>> GetDependenciesAsync(Guid occurrenceId, CancellationToken cancellationToken = default)
    {
        return await this.store.Dependencies.ListByDependentAsync(occurrenceId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets leases currently known to the harness store.
    /// </summary>
    public async Task<IReadOnlyList<JobLeaseRecord>> GetLeasesAsync(CancellationToken cancellationToken = default)
    {
        return await this.store.Leases.ListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Loads a batch by external batch identifier.
    /// </summary>
    public async Task<JobBatch> GetBatchAsync(string batchId, CancellationToken cancellationToken = default)
    {
        var batches = await this.store.Batches.ListAsync(cancellationToken).ConfigureAwait(false);
        return batches.SingleOrDefault(x => string.Equals(x.ExternalBatchId, batchId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Loads batch child occurrences.
    /// </summary>
    public async Task<IReadOnlyList<JobBatchOccurrence>> GetBatchOccurrencesAsync(string batchId, CancellationToken cancellationToken = default)
    {
        var batch = await this.GetBatchAsync(batchId, cancellationToken).ConfigureAwait(false);
        return batch is null
            ? []
            : await this.store.Batches.ListOccurrencesAsync(batch.BatchId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Ensures that retained history contains a matching event.
    /// </summary>
    public async Task AssertHistoryContainsAsync(Guid occurrenceId, string eventName, string messageContains = null, CancellationToken cancellationToken = default)
    {
        var history = await this.GetHistoryAsync(occurrenceId, cancellationToken).ConfigureAwait(false);
        var match = history.FirstOrDefault(x =>
            string.Equals(x.EventName, eventName, StringComparison.Ordinal)
            && (messageContains is null || (x.Message?.Contains(messageContains, StringComparison.OrdinalIgnoreCase) ?? false)));

        if (match is null)
        {
            throw new InvalidOperationException($"The retained history for occurrence '{occurrenceId}' does not contain event '{eventName}'.");
        }
    }

    /// <summary>
    /// Ensures that retry attempts and occurrence status match expectations.
    /// </summary>
    public async Task AssertRetryAttemptsAsync(Guid occurrenceId, int expectedAttempts, JobOccurrenceStatus expectedOccurrenceStatus, CancellationToken cancellationToken = default)
    {
        var occurrence = await this.GetOccurrenceAsync(occurrenceId, cancellationToken).ConfigureAwait(false);
        var executions = await this.GetExecutionsAsync(occurrenceId, cancellationToken).ConfigureAwait(false);

        if (occurrence is null)
        {
            throw new InvalidOperationException($"Occurrence '{occurrenceId}' was not found.");
        }

        if (occurrence.Status != expectedOccurrenceStatus)
        {
            throw new InvalidOperationException($"Occurrence '{occurrenceId}' has status '{occurrence.Status}', expected '{expectedOccurrenceStatus}'.");
        }

        if (executions.Count != expectedAttempts)
        {
            throw new InvalidOperationException($"Occurrence '{occurrenceId}' has {executions.Count} execution attempt(s), expected {expectedAttempts}.");
        }
    }

    /// <summary>
    /// Ensures that an occurrence remains blocked by dependencies.
    /// </summary>
    public async Task AssertBlockedDependencyAsync(Guid occurrenceId, string blockedReasonContains = null, CancellationToken cancellationToken = default)
    {
        var occurrence = await this.GetOccurrenceAsync(occurrenceId, cancellationToken).ConfigureAwait(false);
        var dependencies = await this.GetDependenciesAsync(occurrenceId, cancellationToken).ConfigureAwait(false);

        if (occurrence is null)
        {
            throw new InvalidOperationException($"Occurrence '{occurrenceId}' was not found.");
        }

        if (occurrence.Status != JobOccurrenceStatus.Blocked)
        {
            throw new InvalidOperationException($"Occurrence '{occurrenceId}' has status '{occurrence.Status}', expected '{JobOccurrenceStatus.Blocked}'.");
        }

        if (dependencies.Count == 0)
        {
            throw new InvalidOperationException($"Occurrence '{occurrenceId}' is not blocked by any dependency rows.");
        }

        if (!string.IsNullOrWhiteSpace(blockedReasonContains)
            && !(occurrence.BlockedReason?.Contains(blockedReasonContains, StringComparison.OrdinalIgnoreCase) ?? false))
        {
            throw new InvalidOperationException($"Occurrence '{occurrenceId}' blocked reason does not contain '{blockedReasonContains}'.");
        }
    }

    /// <summary>
    /// Ensures that a batch has the expected roll-up status.
    /// </summary>
    public async Task AssertBatchStatusAsync(string batchId, JobBatchStatus expectedStatus, int? expectedAcceptedCount = null, CancellationToken cancellationToken = default)
    {
        var batch = await this.GetBatchAsync(batchId, cancellationToken).ConfigureAwait(false);
        if (batch is null)
        {
            throw new InvalidOperationException($"Batch '{batchId}' was not found.");
        }

        if (batch.Status != expectedStatus)
        {
            throw new InvalidOperationException($"Batch '{batchId}' has status '{batch.Status}', expected '{expectedStatus}'.");
        }

        if (expectedAcceptedCount.HasValue && batch.AcceptedCount != expectedAcceptedCount.Value)
        {
            throw new InvalidOperationException($"Batch '{batchId}' accepted count is {batch.AcceptedCount}, expected {expectedAcceptedCount.Value}.");
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.provider.Dispose();
    }

    private sealed class TestHostApplicationLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;

        public CancellationToken ApplicationStopping => CancellationToken.None;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication()
        {
        }
    }
}

/// <summary>
/// Builds a <see cref="JobSchedulerTestHarness"/> instance.
/// </summary>
public sealed class JobSchedulerTestHarnessBuilder
{
    private readonly List<Action<JobBuilderContext>> jobConfigurations = [];
    private readonly List<Action<IServiceCollection>> serviceConfigurations = [];
    private readonly List<Action<JobSchedulerHostedOptions>> optionConfigurations = [];
    private DateTimeOffset? nowUtc;

    /// <summary>
    /// Sets the initial harness clock.
    /// </summary>
    public JobSchedulerTestHarnessBuilder WithClock(DateTimeOffset value)
    {
        this.nowUtc = value.ToUniversalTime();
        return this;
    }

    /// <summary>
    /// Registers a job definition.
    /// </summary>
    public JobSchedulerTestHarnessBuilder WithJob<TJob>(string jobName, Action<JobDefinitionBuilder<TJob>> configure)
        where TJob : class, IJob
    {
        this.jobConfigurations.Add(context => context.WithJob(jobName, configure));
        return this;
    }

    /// <summary>
    /// Registers an inline delegate job definition.
    /// </summary>
    public JobSchedulerTestHarnessBuilder WithJob(string jobName, Action<InlineJobDefinitionBuilder> configure)
    {
        this.jobConfigurations.Add(context => context.WithJob(jobName, configure));
        return this;
    }

    /// <summary>
    /// Registers a test service instance.
    /// </summary>
    public JobSchedulerTestHarnessBuilder WithService<TService>(TService instance)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(instance);

        this.serviceConfigurations.Add(services => services.AddSingleton(instance));
        return this;
    }

    /// <summary>
    /// Applies additional service registration.
    /// </summary>
    public JobSchedulerTestHarnessBuilder WithServices(Action<IServiceCollection> configure)
    {
        if (configure is not null)
        {
            this.serviceConfigurations.Add(configure);
        }

        return this;
    }

    /// <summary>
    /// Applies additional hosted scheduler options.
    /// </summary>
    public JobSchedulerTestHarnessBuilder WithOptions(Action<JobSchedulerHostedOptions> configure)
    {
        if (configure is not null)
        {
            this.optionConfigurations.Add(configure);
        }

        return this;
    }

    /// <summary>
    /// Builds the scheduler harness.
    /// </summary>
    public JobSchedulerTestHarness Build()
    {
        return JobSchedulerTestHarness.Create(
            configureJobs: context =>
            {
                foreach (var configuration in this.jobConfigurations)
                {
                    configuration(context);
                }
            },
            configureServices: services =>
            {
                foreach (var configuration in this.serviceConfigurations)
                {
                    configuration(services);
                }
            },
            configureOptions: options =>
            {
                foreach (var configuration in this.optionConfigurations)
                {
                    configuration(options);
                }
            },
            nowUtc: this.nowUtc);
    }
}