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

public class StartupTasksService : BackgroundService
{
    private readonly ILogger<StartupTasksService> logger;
    private readonly IServiceProvider serviceProvider;
    private readonly IHostApplicationLifetime applicationLifetime;
    private readonly IEnumerable<StartupTaskDefinition> definitions;
    private readonly StartupTaskServiceOptions options;

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
        this.definitions = definitions ?? [];
        this.options = options ?? new StartupTaskServiceOptions();

        if (this.definitions.Any(d => d.Options.Order > 0))
        {
            this.definitions = this.definitions.OrderBy(d => d.Options.Order);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!this.options.Enabled)
        {
            return;
        }

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
            using var scope = this.serviceProvider.CreateScope();
            var behaviors = scope.ServiceProvider.GetServices<IStartupTaskBehavior>();

            Parallel.ForEach(this.definitions.SafeNull().Where(d => d.Options.Enabled),
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = this.options.MaxDegreeOfParallelism, CancellationToken = cancellationToken
                },
                definition =>
                {
                    try
                    {
                        if (scope.ServiceProvider.GetService(definition.TaskType) is not IStartupTask task)
                        {
                            this.logger.LogInformation("{LogKey} startup task not registered (task={StartupTaskType})",
                                Constants.LogKey,
                                definition.TaskType.Name);

                            return;
                        }

                        var correlationId = GuidGenerator.CreateSequential().ToString("N");
                        var flowId = GuidGenerator.Create(task.GetType().ToString()).ToString("N");

                        using (this.logger.BeginScope(new Dictionary<string, object>
                               {
                                   [Constants.CorrelationIdKey] = correlationId,
                                   [Constants.FlowIdKey] = flowId,
                                   [Constants.StartupTaskKey] = task.GetType().PrettyName()
                               }))
                        {
                            this.ExecutePipeline(definition, task, behaviors, cancellationToken).Wait();
                        }
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogError(ex,
                            "{LogKey} startup task {StartupTaskType} failed: {ErrorMessage}",
                            Constants.LogKey,
                            definition.TaskType.Name,
                            ex.Message);
                    }
                });
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "{LogKey} startup tasks failed: {ErrorMessage}", Constants.LogKey, ex.Message);
        }
    }

    private async Task ExecuteDefinitionAsync(
        StartupTaskDefinition definition,
        IStartupTask task,
        CancellationToken cancellationToken)
    {
        if (definition.Options.StartupDelay.TotalMilliseconds > 0)
        {
            this.logger.LogDebug("{LogKey} startup tasks delayed (task={StartupTaskType})",
                Constants.LogKey,
                task.GetType().Name);

            await Task.Delay(definition.Options.StartupDelay, cancellationToken);
        }

        try
        {
            this.logger.LogInformation("{LogKey} startup task started (task={StartupTaskType})",
                Constants.LogKey,
                task.GetType().Name);
            var watch = ValueStopwatch.StartNew();

            await task.ExecuteAsync(cancellationToken);
            this.logger.LogInformation(
                "{LogKey} startup task finished (task={StartupTaskType}) -> took {TimeElapsed:0.0000} ms",
                Constants.LogKey,
                task.GetType().Name,
                watch.GetElapsedMilliseconds());
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex,
                "{LogKey} startup task failed: {ErrorMessage} (task={StartupTaskType})",
                Constants.LogKey,
                ex.Message,
                task.GetType().Name);
        }
    }

    private async Task ExecutePipeline(
        StartupTaskDefinition definition,
        IStartupTask task,
        IEnumerable<IStartupTaskBehavior> behaviors,
        CancellationToken cancellationToken)
    {
        // create a behavior pipeline and run it (execute > next)
        this.logger.LogDebug(
            $"{{LogKey}} startup task behaviors: {behaviors.SafeNull().Select(b => b.GetType().Name).ToString(" -> ")} -> {task.GetType().Name}:Execute",
            Constants.LogKey);

        async Task TaskExecutor()
        {
            await this.ExecuteDefinitionAsync(definition, task, cancellationToken).AnyContext();
        }

        await behaviors.SafeNull()
            .Reverse()
            .Aggregate((TaskDelegate)TaskExecutor,
                (next, pipeline) => async () => await pipeline.Execute(task, cancellationToken, next))();
    }
}