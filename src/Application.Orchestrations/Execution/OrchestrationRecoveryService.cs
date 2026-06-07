// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Performs periodic orchestration recovery for due timers and incomplete waiting boundaries.
/// </summary>
public class OrchestrationRecoveryService : BackgroundService
{
    private readonly InMemoryOrchestrationExecutor executor;
    private readonly IOrchestrationStorageProvider persistenceProvider;
    private readonly IOrchestrationClock clock;
    private readonly IHostApplicationLifetime applicationLifetime;
    private readonly OrchestrationExecutionSettings settings;
    private readonly ILogger<OrchestrationRecoveryService> logger;
    private IDisposable startupRegistration;
    private CancellationTokenSource linkedCts;
    private Task startupTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationRecoveryService"/> class.
    /// </summary>
    /// <param name="executor">The orchestration executor used to repair and continue instances.</param>
    /// <param name="persistenceProvider">The orchestration persistence provider.</param>
    /// <param name="clock">The orchestration clock.</param>
    /// <param name="applicationLifetime">The host application lifetime used to defer startup until the application is ready.</param>
    /// <param name="settings">The orchestration execution settings.</param>
    /// <param name="loggerFactory">The logger factory used to create the service logger.</param>
    public OrchestrationRecoveryService(
        InMemoryOrchestrationExecutor executor,
        IOrchestrationStorageProvider persistenceProvider,
        IOrchestrationClock clock,
        IHostApplicationLifetime applicationLifetime,
        OrchestrationExecutionSettings settings,
        ILoggerFactory loggerFactory = null)
    {
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
        this.persistenceProvider = persistenceProvider ?? throw new ArgumentNullException(nameof(persistenceProvider));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.applicationLifetime = applicationLifetime ?? throw new ArgumentNullException(nameof(applicationLifetime));
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<OrchestrationRecoveryService>();
    }

    /// <summary>
    /// Performs a single recovery sweep that repairs incomplete waiting boundaries and continues due timer instances.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token used to stop the sweep.</param>
    /// <returns>A task that completes when the recovery sweep has finished.</returns>
    public async Task SweepOnceAsync(CancellationToken cancellationToken = default)
    {
        await this.RepairWaitingInstancesAsync(cancellationToken).ConfigureAwait(false);
        await this.ContinueDueTimersAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Registers the recovery loop once the host has fully started.
    /// </summary>
    /// <param name="stoppingToken">A token that is triggered when the host begins shutting down.</param>
    /// <returns>A completed task after startup registration has been configured.</returns>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!this.settings.EnableBackgroundExecution)
        {
            return Task.CompletedTask;
        }

        this.linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

        this.startupRegistration = this.applicationLifetime.ApplicationStarted.Register(() =>
        {
            this.startupTask = Task.Run(() => this.StartInternalAsync(this.linkedCts.Token), this.linkedCts.Token);
        });

        stoppingToken.Register(this.OnStopping);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the recovery service and waits briefly for any startup or sweep work to wind down.
    /// </summary>
    /// <param name="cancellationToken">A token that limits how long shutdown coordination may wait.</param>
    /// <returns>A task that completes once shutdown coordination has finished.</returns>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        this.logger.LogInformation("{LogKey} orchestration recovery service stopping", Constants.LogKey);

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

        this.logger.LogInformation("{LogKey} orchestration recovery service stopped", Constants.LogKey);

        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Releases resources used by the recovery service.
    /// </summary>
    public override void Dispose()
    {
        this.startupRegistration?.Dispose();
        this.linkedCts?.Dispose();
        base.Dispose();
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
            // Ignore shutdown-time exceptions
        }
    }

    private async Task StartInternalAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (this.settings.StartupDelay > TimeSpan.Zero)
            {
                this.logger.LogDebug("{LogKey} orchestration recovery service startup delayed by {Delay}ms", Constants.LogKey, this.settings.StartupDelay.TotalMilliseconds);
                await this.clock.DelayAsync(this.settings.StartupDelay, cancellationToken).ConfigureAwait(false);
            }

            this.logger.LogInformation(
                "{LogKey} orchestration recovery service starting (sweepInterval={SweepInterval}ms, batchSize={BatchSize})",
                Constants.LogKey,
                this.settings.BackgroundSweepInterval.TotalMilliseconds,
                this.settings.BackgroundSweepBatchSize);

            await this.RunSweepSafelyAsync(cancellationToken).ConfigureAwait(false);

            while (!cancellationToken.IsCancellationRequested)
            {
                await this.clock.DelayAsync(this.settings.BackgroundSweepInterval, cancellationToken).ConfigureAwait(false);
                await this.RunSweepSafelyAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception exception)
        {
            this.logger.LogError(exception, "{LogKey} orchestration recovery service failed unexpectedly: {ErrorMessage}", Constants.LogKey, exception.Message);
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
            this.logger.LogWarning(exception, "{LogKey} orchestration recovery sweep failed; the service will retry on the next interval: {ErrorMessage}", Constants.LogKey, exception.Message);
        }
    }

    private async Task RepairWaitingInstancesAsync(CancellationToken cancellationToken)
    {
        var skip = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batch = await this.persistenceProvider.Queries.QueryAsync(new OrchestrationInstanceQuery
            {
                Statuses = [OrchestrationStatus.Waiting],
                Skip = skip,
                Take = this.settings.BackgroundSweepBatchSize,
            }, cancellationToken).ConfigureAwait(false);

            if (batch.Items.Count == 0)
            {
                return;
            }

            foreach (var snapshot in batch.Items)
            {
                using var logScope = this.BeginOrchestrationScope(snapshot.InstanceId, snapshot.OrchestrationName, snapshot.CurrentState);
                try
                {
                    var result = await this.executor.RepairWaitingInstanceAsync(snapshot.InstanceId, cancellationToken).ConfigureAwait(false);
                    if (result.Result == InMemoryOrchestrationExecutor.RecoveryActionResult.Repaired)
                    {
                        this.logger.LogInformation("{LogKey} recovered missing orchestration timer(s) (instanceId={InstanceId}, count={Count})", Constants.LogKey, snapshot.InstanceId, result.AffectedTimerCount);
                    }
                    else if (result.Result == InMemoryOrchestrationExecutor.RecoveryActionResult.SkippedLeaseConflict)
                    {
                        this.logger.LogDebug("{LogKey} skipped waiting-boundary repair because another worker owns the lease (instanceId={InstanceId})", Constants.LogKey, snapshot.InstanceId);
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    this.logger.LogError(exception, "{LogKey} orchestration waiting-boundary recovery failed (instanceId={InstanceId})", Constants.LogKey, snapshot.InstanceId);
                }
            }

            if (batch.Items.Count < this.settings.BackgroundSweepBatchSize)
            {
                return;
            }

            skip += batch.Items.Count;
        }
    }

    private async Task ContinueDueTimersAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var instanceIds = (await this.persistenceProvider.Timers.GetDueAsync(this.clock.UtcNow, cancellationToken).ConfigureAwait(false))
                .Select(item => item.InstanceId)
                .Distinct()
                .Take(this.settings.BackgroundSweepBatchSize)
                .ToArray();

            if (instanceIds.Length == 0)
            {
                return;
            }

            foreach (var instanceId in instanceIds)
            {
                using var logScope = this.BeginOrchestrationScope(instanceId);
                try
                {
                    var result = await this.executor.ContinueInstanceForRecoveryAsync(instanceId, cancellationToken).ConfigureAwait(false);
                    if (result.Result == InMemoryOrchestrationExecutor.RecoveryActionResult.Continued)
                    {
                        this.logger.LogInformation("{LogKey} continued orchestration instance for due timer recovery (instanceId={InstanceId})", Constants.LogKey, instanceId);
                    }
                    else if (result.Result == InMemoryOrchestrationExecutor.RecoveryActionResult.SkippedLeaseConflict)
                    {
                        this.logger.LogDebug("{LogKey} skipped due-timer continuation because another worker owns the lease (instanceId={InstanceId})", Constants.LogKey, instanceId);
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    this.logger.LogError(exception, "{LogKey} orchestration due-timer continuation failed (instanceId={InstanceId})", Constants.LogKey, instanceId);
                }
            }

            if (instanceIds.Length < this.settings.BackgroundSweepBatchSize)
            {
                return;
            }
        }
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
}
