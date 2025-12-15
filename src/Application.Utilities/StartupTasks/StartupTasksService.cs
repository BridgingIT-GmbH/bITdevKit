// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Utilities;

using Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Service responsible for executing configured startup tasks after the application host has started.
/// </summary>
/// <remarks>
/// Startup tasks are executed once during application startup. As execution is triggered via
/// <see cref="IHostApplicationLifetime.ApplicationStarted"/>, asynchronous execution is explicitly
/// tracked and coordinated with host shutdown to avoid running after the dependency injection
/// container has been disposed.
/// </remarks>
public class StartupTasksService : IHostedService
{
    private readonly ILogger<StartupTasksService> logger;
    private readonly IServiceProvider serviceProvider;
    private readonly IHostApplicationLifetime applicationLifetime;
    private readonly IEnumerable<StartupTaskDefinition> definitions;
    private readonly StartupTaskServiceOptions options;

    private IDisposable startupRegistration;
    private CancellationTokenSource linkedCts;
    private Task startupTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartupTasksService"/> class.
    /// </summary>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="serviceProvider">The root service provider.</param>
    /// <param name="applicationLifetime">The application lifetime.</param>
    /// <param name="definitions">The startup task definitions.</param>
    /// <param name="options">The startup task service options.</param>
    public StartupTasksService(
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider,
        IHostApplicationLifetime applicationLifetime,
        IEnumerable<StartupTaskDefinition> definitions = null,
        StartupTaskServiceOptions options = null)
    {
        EnsureArg.IsNotNull(serviceProvider, nameof(serviceProvider));

        this.logger = loggerFactory?.CreateLogger<StartupTasksService>() ?? NullLoggerFactory.Instance.CreateLogger<StartupTasksService>();
        this.serviceProvider = serviceProvider;
        this.applicationLifetime = applicationLifetime;
        this.definitions = (definitions ?? []).ToArray();
        this.options = options ?? new StartupTaskServiceOptions();

        if (this.definitions.Any(d => d.Options.Order > 0))
        {
            this.definitions = this.definitions.OrderBy(d => d.Options.Order);
        }
    }

    /// <summary>
    /// Registers execution of startup tasks once the application host has fully started.
    /// </summary>
    /// <param name="cancellationToken">A token triggered when the host begins shutting down.</param>
    /// <returns>A completed task.</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!this.options.Enabled)
        {
            return Task.CompletedTask;
        }

        this.linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        this.startupRegistration = this.applicationLifetime.ApplicationStarted.Register(() =>
        {
            this.startupTask = Task.Run(() => this.StartInternalAsync(this.linkedCts.Token), this.linkedCts.Token);
        });

        cancellationToken.Register(this.OnStopping);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Waits for startup task execution to complete before allowing host shutdown.
    /// </summary>
    /// <param name="cancellationToken">A token that limits shutdown waiting.</param>
    /// <returns>A task that completes once startup task execution has finished or timed out.</returns>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        this.logger.LogInformation("{LogKey} startup tasks service stopping", Constants.LogKey);

        this.linkedCts?.Cancel();

        if (this.startupTask != null)
        {
            try
            {
                await Task.WhenAny(this.startupTask, Task.Delay(TimeSpan.FromSeconds(10), cancellationToken));
                this.logger.LogInformation("{LogKey} startup tasks service stopped", Constants.LogKey);
            }
            catch
            {
                // Ignore shutdown-time failures
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
            // Ignore shutdown-time exceptions
        }
    }

    private async Task StartInternalAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (this.options.StartupDelay.TotalMilliseconds > 0)
            {
                this.logger.LogDebug("{LogKey} startup tasks service delayed", Constants.LogKey);
                await Task.Delay(this.options.StartupDelay, cancellationToken);
            }

            var tasks = this.definitions.SafeNull()
                .Where(d => d.Options.Enabled)
                .Select(definition => Task.Run(async () =>
                {
                    try
                    {
                        using var scope = this.serviceProvider.CreateScope();

                        if (scope.ServiceProvider.GetService(definition.TaskType) is not IStartupTask task)
                        {
                            this.logger.LogInformation("{LogKey} startup task not registered (task={StartupTaskType})", Constants.LogKey, definition.TaskType.Name);
                            return;
                        }

                        var behaviors = scope.ServiceProvider.GetServices<IStartupTaskBehavior>();

                        await this.ExecutePipelineAsync(definition, task, behaviors, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected during shutdown
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogError(ex, "{LogKey} startup task {StartupTaskType} failed: {ErrorMessage}", Constants.LogKey, definition.TaskType.Name, ex.Message);
                    }
                }, cancellationToken));

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            this.logger.LogDebug("{LogKey} startup tasks canceled", Constants.LogKey);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "{LogKey} startup tasks failed: {ErrorMessage}", Constants.LogKey, ex.Message);
            throw;
        }
    }

    private async Task ExecuteDefinitionAsync(
        StartupTaskDefinition definition,
        IStartupTask task,
        CancellationToken cancellationToken)
    {
        if (definition.Options.StartupDelay.TotalMilliseconds > 0)
        {
            this.logger.LogDebug("{LogKey} startup task delayed (task={StartupTaskType})", Constants.LogKey, task.GetType().PrettyName());
            await Task.Delay(definition.Options.StartupDelay, cancellationToken);
        }

        this.logger.LogInformation("{LogKey} startup task started (task={StartupTaskType})", Constants.LogKey, task.GetType().PrettyName());
        var watch = ValueStopwatch.StartNew();

        await task.ExecuteAsync(cancellationToken);

        this.logger.LogInformation("{LogKey} startup task finished (task={StartupTaskType}) -> took {TimeElapsed:0.0000} ms", Constants.LogKey, task.GetType().PrettyName(), watch.GetElapsedMilliseconds());
    }

    private async Task ExecutePipelineAsync(
        StartupTaskDefinition definition,
        IStartupTask task,
        IEnumerable<IStartupTaskBehavior> behaviors,
        CancellationToken cancellationToken)
    {
        var startupTaskBehaviors = behaviors as IStartupTaskBehavior[] ?? behaviors.ToArray();

        this.logger.LogDebug("{LogKey} startup task behaviors: {BehaviorPipeline}", Constants.LogKey, startupTaskBehaviors.Select(b => b.GetType().Name).ToString(" -> "));

        await startupTaskBehaviors
            .Reverse()
            .Aggregate((TaskDelegate)TaskExecutor,
                (next, pipeline) => async () => await pipeline.Execute(task, cancellationToken, next).AnyContext())().AnyContext();

        async Task TaskExecutor()
        {
            await this.ExecuteDefinitionAsync(definition, task, cancellationToken).AnyContext();
        }
    }
}