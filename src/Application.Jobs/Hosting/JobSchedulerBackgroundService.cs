// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Runs trigger materialization and due-occurrence execution in the background over the in-memory provider.
/// </summary>
public partial class JobSchedulerBackgroundService : BackgroundService
{
    private readonly TimeProvider timeProvider;
    private readonly JobSchedulerService scheduler;
    private readonly IHostApplicationLifetime applicationLifetime;
    private readonly JobSchedulerHostedOptions options;
    private readonly IJobSchedulerExceptionHandler[] exceptionHandlers;
    private readonly ILogger<JobSchedulerBackgroundService> logger;
    private readonly SemaphoreSlim concurrencyGate;
    private IDisposable startupRegistration;
    private CancellationTokenSource linkedCts;
    private Task startupTask;
    private DateTimeOffset schedulerStartedUtc;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobSchedulerBackgroundService"/> class.
    /// </summary>
    public JobSchedulerBackgroundService(
        TimeProvider timeProvider,
        JobSchedulerService scheduler,
        IHostApplicationLifetime applicationLifetime,
        JobSchedulerHostedOptions options = null,
        IEnumerable<IJobSchedulerExceptionHandler> exceptionHandlers = null,
        ILoggerFactory loggerFactory = null)
    {
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        this.applicationLifetime = applicationLifetime ?? throw new ArgumentNullException(nameof(applicationLifetime));
        this.exceptionHandlers = exceptionHandlers?.ToArray() ?? [];
        this.options = options ?? new JobSchedulerHostedOptions();
        this.logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<JobSchedulerBackgroundService>();
        this.concurrencyGate = new SemaphoreSlim(Math.Max(1, this.options.MaxConcurrency), Math.Max(1, this.options.MaxConcurrency));
    }

    /// <summary>
    /// Performs one scheduler sweep.
    /// </summary>
    public async Task SweepOnceAsync(CancellationToken cancellationToken = default)
    {
        this.schedulerStartedUtc = this.schedulerStartedUtc == default ? this.timeProvider.GetUtcNow() : this.schedulerStartedUtc;

        using var activity = JobSchedulerInstrumentation.StartSweepActivity(this.scheduler.SchedulerInstanceId);
        var recovered = await this.scheduler.RecoverExpiredLeasesAsync(cancellationToken).ConfigureAwait(false);
        if (recovered > 0)
        {
            TypedLogger.LogRecoveredExpiredLeaseOccurrences(this.logger, Constants.LogKey, recovered);
        }

        var materialized = await this.scheduler.MaterializeScheduledOccurrencesAsync(this.schedulerStartedUtc, this.options.MaxCatchUpOccurrences, cancellationToken).ConfigureAwait(false);
        if (materialized.IsFailure)
        {
            TypedLogger.LogBackgroundTriggerMaterializationFailed(this.logger, Constants.LogKey, materialized.Messages.FirstOrDefault() ?? materialized.Errors.FirstOrDefault()?.Message);
        }
        else if (materialized.Value.Count > 0)
        {
            TypedLogger.LogMaterializedSchedulerOccurrences(this.logger, Constants.LogKey, materialized.Value.Count);
        }

        var dueOccurrences = await this.scheduler.ListReadyOccurrencesAsync(cancellationToken).ConfigureAwait(false);
        activity?.SetTag("jobs.sweep.recovered_count", recovered);
        activity?.SetTag("jobs.sweep.materialized_count", materialized.IsSuccess ? materialized.Value.Count : 0);
        activity?.SetTag("jobs.sweep.ready_count", dueOccurrences.Count);
        if (dueOccurrences.Count == 0)
        {
            JobSchedulerInstrumentation.RecordSweepCycle(this.scheduler.SchedulerInstanceId, recovered, materialized.IsSuccess ? materialized.Value.Count : 0, 0, this.scheduler.ActiveExecutionCount, this.options.MaxConcurrency);
            return;
        }

        var availableSlots = Math.Max(0, this.options.MaxConcurrency - this.scheduler.ActiveExecutionCount);
        if (availableSlots == 0)
        {
            JobSchedulerInstrumentation.RecordSweepCycle(this.scheduler.SchedulerInstanceId, recovered, materialized.IsSuccess ? materialized.Value.Count : 0, dueOccurrences.Count, this.scheduler.ActiveExecutionCount, this.options.MaxConcurrency);
            TypedLogger.LogSweepDeferredNoWorkerSlots(
                this.logger,
                Constants.LogKey,
                this.scheduler.SchedulerInstanceId,
                dueOccurrences.Count,
                this.scheduler.ActiveExecutionCount,
                this.options.MaxConcurrency);
            return;
        }

        JobSchedulerInstrumentation.RecordSweepCycle(this.scheduler.SchedulerInstanceId, recovered, materialized.IsSuccess ? materialized.Value.Count : 0, dueOccurrences.Count, this.scheduler.ActiveExecutionCount, this.options.MaxConcurrency);

        var selected = dueOccurrences.Take(Math.Min(this.options.BatchSize, availableSlots)).ToArray();
        TypedLogger.LogSweepDispatchingReadyOccurrences(
            this.logger,
            Constants.LogKey,
            this.scheduler.SchedulerInstanceId,
            dueOccurrences.Count,
            selected.Length,
            availableSlots,
            this.options.BatchSize);
        var tasks = selected.Select(occurrence => this.RunWorkerAsync(occurrence.OccurrenceId, cancellationToken)).ToArray();
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!this.options.EnableBackgroundExecution)
        {
            return Task.CompletedTask;
        }

        this.linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        this.startupRegistration = this.applicationLifetime.ApplicationStarted.Register(() =>
        {
            this.schedulerStartedUtc = this.timeProvider.GetUtcNow();
            this.startupTask = Task.Run(() => this.StartInternalAsync(this.linkedCts.Token), this.linkedCts.Token);
        });

        stoppingToken.Register(this.OnStopping);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        TypedLogger.LogBackgroundServiceStopping(this.logger, Constants.LogKey, this.scheduler.SchedulerInstanceId);

        this.linkedCts?.Cancel();
        if (this.startupTask != null)
        {
            try
            {
                await Task.WhenAny(this.startupTask, Task.Delay(TimeSpan.FromSeconds(10), cancellationToken)).ConfigureAwait(false);
            }
            catch
            {
                // Ignore shutdown-time failures
            }
        }

        TypedLogger.LogBackgroundServiceStopped(this.logger, Constants.LogKey, this.scheduler.SchedulerInstanceId);
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        this.startupRegistration?.Dispose();
        this.linkedCts?.Dispose();
        this.concurrencyGate.Dispose();
        base.Dispose();
    }

    private async Task StartInternalAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (this.options.StartupDelay > TimeSpan.Zero)
            {
                TypedLogger.LogStartupDelayed(this.logger, Constants.LogKey, this.options.StartupDelay.TotalMilliseconds, this.scheduler.SchedulerInstanceId);
                await Task.Delay(this.options.StartupDelay, this.timeProvider, cancellationToken).ConfigureAwait(false);
            }

            TypedLogger.LogBackgroundServiceStarting(
                this.logger,
                Constants.LogKey,
                this.scheduler.SchedulerInstanceId,
                this.options.SweepInterval.TotalMilliseconds,
                this.options.BatchSize,
                this.options.MaxConcurrency);

            await this.RunSweepSafelyAsync(cancellationToken).ConfigureAwait(false);

            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(this.options.SweepInterval, this.timeProvider, cancellationToken).ConfigureAwait(false);
                await this.RunSweepSafelyAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown.
        }
        catch (Exception exception)
        {
            TypedLogger.LogBackgroundServiceFailedUnexpectedly(this.logger, exception, Constants.LogKey, exception.Message);
            throw;
        }
    }

    private async Task RunSweepSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await this.SweepOnceAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            await this.HandleUnhandledExceptionAsync(exception, cancellationToken).ConfigureAwait(false);
            TypedLogger.LogBackgroundSweepFailed(this.logger, exception, Constants.LogKey, exception.Message);
        }
    }

    private async Task HandleUnhandledExceptionAsync(Exception exception, CancellationToken cancellationToken)
    {
        var context = new JobSchedulerExceptionContext
        {
            SchedulerInstanceId = this.scheduler.SchedulerInstanceId,
            Source = JobSchedulerExceptionSource.BackgroundService,
            Exception = exception,
        };

        foreach (var handler in this.exceptionHandlers)
        {
            try
            {
                await handler.HandleAsync(context, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Do not let exception handlers hide the scheduler's original failure semantics.
            }
        }
    }

    private void OnStopping()
    {
        try
        {
            this.startupRegistration?.Dispose();
            this.linkedCts?.Cancel();
        }
        catch
        {
            // Ignore shutdown-time exceptions.
        }
    }

    private async Task RunWorkerAsync(Guid occurrenceId, CancellationToken cancellationToken)
    {
        await this.concurrencyGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await this.scheduler.ExecuteStoredOccurrenceAsync(occurrenceId, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure)
            {
                TypedLogger.LogBackgroundOccurrenceExecutionSkippedOrFailed(this.logger, Constants.LogKey, this.scheduler.SchedulerInstanceId, occurrenceId, result.Messages.FirstOrDefault() ?? result.Errors.FirstOrDefault()?.Message);
            }
        }
        finally
        {
            this.concurrencyGate.Release();
        }
    }

    private static partial class TypedLogger
    {
        [LoggerMessage(0, LogLevel.Debug, "[{LogKey}] recovered {Count} expired lease occurrence(s)")]
        public static partial void LogRecoveredExpiredLeaseOccurrences(ILogger logger, string logKey, int count);

        [LoggerMessage(1, LogLevel.Warning, "[{LogKey}] background trigger materialization failed ({Message})")]
        public static partial void LogBackgroundTriggerMaterializationFailed(ILogger logger, string logKey, string message);

        [LoggerMessage(2, LogLevel.Debug, "[{LogKey}] materialized {Count} scheduler occurrence(s)")]
        public static partial void LogMaterializedSchedulerOccurrences(ILogger logger, string logKey, int count);

        [LoggerMessage(3, LogLevel.Debug, "[{LogKey}] job scheduler sweep deferred ready occurrences, no worker slots available (instanceId={SchedulerInstanceId}, ready={ReadyCount}, activeExecutions={ActiveExecutionCount}, maxConcurrency={MaxConcurrency})")]
        public static partial void LogSweepDeferredNoWorkerSlots(ILogger logger, string logKey, string schedulerInstanceId, int readyCount, int activeExecutionCount, int maxConcurrency);

        [LoggerMessage(4, LogLevel.Debug, "[{LogKey}] job scheduler sweep dispatching ready occurrences (instanceId={SchedulerInstanceId}, ready={ReadyCount}, selected={SelectedCount}, availableSlots={AvailableSlots}, batchSize={BatchSize})")]
        public static partial void LogSweepDispatchingReadyOccurrences(ILogger logger, string logKey, string schedulerInstanceId, int readyCount, int selectedCount, int availableSlots, int batchSize);

        [LoggerMessage(5, LogLevel.Information, "[{LogKey}] job scheduler background service stopping (instanceId={SchedulerInstanceId})")]
        public static partial void LogBackgroundServiceStopping(ILogger logger, string logKey, string schedulerInstanceId);

        [LoggerMessage(6, LogLevel.Information, "[{LogKey}] job scheduler background service stopped (instanceId={SchedulerInstanceId})")]
        public static partial void LogBackgroundServiceStopped(ILogger logger, string logKey, string schedulerInstanceId);

        [LoggerMessage(7, LogLevel.Debug, "[{LogKey}] job scheduler startup delayed by {Delay}ms (instanceId={SchedulerInstanceId})")]
        public static partial void LogStartupDelayed(ILogger logger, string logKey, double delay, string schedulerInstanceId);

        [LoggerMessage(8, LogLevel.Information, "[{LogKey}] job scheduler background service starting (instanceId={SchedulerInstanceId}, sweepInterval={SweepInterval}ms, batchSize={BatchSize}, maxConcurrency={MaxConcurrency})")]
        public static partial void LogBackgroundServiceStarting(ILogger logger, string logKey, string schedulerInstanceId, double sweepInterval, int batchSize, int maxConcurrency);

        [LoggerMessage(9, LogLevel.Error, "[{LogKey}] job scheduler background service failed unexpectedly: {ErrorMessage}")]
        public static partial void LogBackgroundServiceFailedUnexpectedly(ILogger logger, Exception exception, string logKey, string errorMessage);

        [LoggerMessage(10, LogLevel.Warning, "[{LogKey}] job scheduler background sweep failed; the service will retry on the next interval: {ErrorMessage}")]
        public static partial void LogBackgroundSweepFailed(ILogger logger, Exception exception, string logKey, string errorMessage);

        [LoggerMessage(11, LogLevel.Debug, "[{LogKey}] background occurrence execution skipped or failed (instanceId={SchedulerInstanceId}, occurrenceId={OccurrenceId}, message={Message})")]
        public static partial void LogBackgroundOccurrenceExecutionSkippedOrFailed(ILogger logger, string logKey, string schedulerInstanceId, Guid occurrenceId, string message);
    }
}
