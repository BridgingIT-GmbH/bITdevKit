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
/// Service to manage and execute startup tasks.
/// </summary>
public class StartupTasksService : IHostedService
{
    private readonly ILogger<StartupTasksService> logger;
    private readonly IServiceProvider serviceProvider;
    private readonly IHostApplicationLifetime applicationLifetime;
    private readonly IEnumerable<StartupTaskDefinition> definitions;
    private readonly StartupTaskServiceOptions options;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartupTasksService"/> class.
    /// </summary>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="serviceProvider">The service provider.</param>
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

        this.logger = loggerFactory?.CreateLogger<StartupTasksService>() ??
            NullLoggerFactory.Instance.CreateLogger<StartupTasksService>();
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
    /// Starts the service and executes the startup tasks.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!this.options.Enabled)
        {
            return Task.CompletedTask;
        }

        _ = Task.Run(async () =>
            {
                // Wait "indefinitely", until ApplicationStarted is triggered
                await Task.Delay(Timeout.InfiniteTimeSpan, this.applicationLifetime.ApplicationStarted)
                    .ContinueWith(_ =>
                        {
                            this.logger.LogDebug("{LogKey} startup tasks - application started", Constants.LogKey);
                        },
                        TaskContinuationOptions.OnlyOnCanceled)
                    .ConfigureAwait(false);

                if (this.options.StartupDelay.TotalMilliseconds > 0)
                {
                    this.logger.LogDebug("{LogKey} startup tasks service delayed", Constants.LogKey);

                    await Task.Delay(this.options.StartupDelay, cancellationToken).AnyContext();
                }

                try
                {
                    var tasks = this.definitions.SafeNull()
                        .Where(d => d.Options.Enabled)
                        .Select(async definition =>
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
                            catch (Exception ex)
                            {
                                this.logger.LogError(ex, "{LogKey} startup task {StartupTaskType} failed: {ErrorMessage}", Constants.LogKey, definition.TaskType.Name, ex.Message);
                            }
                        });

                    await Task.WhenAll(tasks).ConfigureAwait(false);
                    // Parallel.ForEach(this.definitions.SafeNull().Where(d => d.Options.Enabled),
                    //     new ParallelOptions
                    //     {
                    //         MaxDegreeOfParallelism = this.options.MaxDegreeOfParallelism, CancellationToken = cancellationToken
                    //     },
                    //     async definition =>
                    //     {
                    //         try
                    //         {
                    //             using var scope = this.serviceProvider.CreateScope();
                    //
                    //             if (scope.ServiceProvider.GetService(definition.TaskType) is not IStartupTask task)
                    //             {
                    //                 this.logger.LogInformation("{LogKey} startup task not registered (task={StartupTaskType})", Constants.LogKey, definition.TaskType.Name);
                    //
                    //                 return;
                    //             }
                    //
                    //             var behaviors = scope.ServiceProvider.GetServices<IStartupTaskBehavior>();
                    //             await this.ExecutePipelineAsync(definition, task, behaviors, cancellationToken).ConfigureAwait(false);
                    //         }
                    //         catch (Exception ex)
                    //         {
                    //             this.logger.LogError(ex, "{LogKey} startup task {StartupTaskType} failed: {ErrorMessage}", Constants.LogKey, definition.TaskType.Name, ex.Message);
                    //         }
                    //     });
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "{LogKey} startup tasks failed: {ErrorMessage}", Constants.LogKey, ex.Message);
                }
            },
            cancellationToken);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the service.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Executes a startup task definition.
    /// </summary>
    /// <param name="definition">The startup task definition.</param>
    /// <param name="task">The startup task.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
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

        this.logger.LogInformation(
            "{LogKey} startup task finished (task={StartupTaskType}) -> took {TimeElapsed:0.0000} ms",
            Constants.LogKey,
            task.GetType().PrettyName(),
            watch.GetElapsedMilliseconds());
    }

    /// <summary>
    /// Executes the startup task pipeline.
    /// </summary>
    /// <param name="definition">The startup task definition.</param>
    /// <param name="task">The startup task.</param>
    /// <param name="behaviors">The startup task behaviors.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task ExecutePipelineAsync(
        StartupTaskDefinition definition,
        IStartupTask task,
        IEnumerable<IStartupTaskBehavior> behaviors,
        CancellationToken cancellationToken)
    {
        // create a behavior pipeline and run it (execute > next)
        var startupTaskBehaviors = behaviors as IStartupTaskBehavior[] ?? behaviors.ToArray();

        this.logger.LogDebug(
            $"{{LogKey}} startup task behaviors: {startupTaskBehaviors.Select(b => b.GetType().Name).ToString(" -> ")} -> {task.GetType().PrettyName()}:Execute",
            Constants.LogKey);

        await startupTaskBehaviors
            .Reverse()
            .Aggregate((TaskDelegate)TaskExecutor,
                (next, pipeline) => async () =>
                    await pipeline.Execute(task, cancellationToken, next).AnyContext())().AnyContext();

        return;

        async Task TaskExecutor()
        {
            await this.ExecuteDefinitionAsync(definition, task, cancellationToken).AnyContext();
        }
    }
}