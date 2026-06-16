// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Queueing;

using System.Diagnostics;

/// <summary>
/// Adds module, tracing, and logging scope metadata around queue handler execution.
/// </summary>
/// <example>
/// <code>
/// services.AddQueueing()
///     .WithBehavior&lt;ModuleScopeQueueHandlerBehavior&gt;();
/// </code>
/// </example>
public class ModuleScopeQueueHandlerBehavior(
    ILoggerFactory loggerFactory,
    IEnumerable<IModuleContextAccessor> moduleAccessors = null,
    IEnumerable<ActivitySource> activitySources = null) : IQueueHandlerBehavior
{
    private readonly ILogger<ModuleScopeQueueHandlerBehavior> logger =
        (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<ModuleScopeQueueHandlerBehavior>();

    /// <summary>
    /// Wraps queue handler execution in the current module scope and restores transport metadata.
    /// </summary>
    /// <param name="message">The queue message being processed.</param>
    /// <param name="cancellationToken">The handler cancellation token.</param>
    /// <param name="handler">The concrete queue handler instance.</param>
    /// <param name="next">The next handler delegate.</param>
    /// <returns>A task that completes when handler processing has finished.</returns>
    public async Task Handle(IQueueMessage message, CancellationToken cancellationToken, object handler, QueueHandlerDelegate next)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var moduleNameOrigin = message?.Properties?.GetValue(ModuleConstants.ModuleNameOriginKey)?.ToString().EmptyToNull() ?? ModuleConstants.UnknownModuleName;
        var correlationId = message?.Properties?.GetValue(Constants.CorrelationIdKey)?.ToString();
        var flowId = message?.Properties?.GetValue(Constants.FlowIdKey)?.ToString();
        var parentId = message?.Properties?.GetValue(ModuleConstants.ActivityParentIdKey)?.ToString();

        var module = moduleAccessors.Find(handler?.GetType());
        var moduleName = module?.Name ?? ModuleConstants.UnknownModuleName;

        using (this.logger.BeginScope(new Dictionary<string, object>
        {
            [ModuleConstants.ModuleNameKey] = moduleName,
            [Constants.CorrelationIdKey] = correlationId,
            [Constants.FlowIdKey] = flowId
        }))
        {
            if (module is not null && !module.Enabled)
            {
                throw new ModuleNotEnabledException(moduleName);
            }

            var messageType = message?.GetType().PrettyName(false);
            var handlerType = handler?.GetType().PrettyName(false);

            if (!string.IsNullOrEmpty(moduleNameOrigin) &&
                !moduleNameOrigin.Equals(module?.Name, StringComparison.OrdinalIgnoreCase))
            {
                await activitySources.Find(moduleName)
                    .StartActvity(
                        $"MODULE {moduleName}",
                        async (activity, activityCancellationToken) =>
                        {
                            using (this.logger.BeginScope(new Dictionary<string, object>
                            {
                                ["TraceId"] = activity?.TraceId.ToString()
                            }))
                            {
                                await this.HandleWithActivity(
                                    message,
                                    messageType,
                                    handlerType,
                                    moduleName,
                                    moduleNameOrigin,
                                    activityCancellationToken,
                                    next);
                            }
                        },
                        parentId: parentId,
                        baggages: new Dictionary<string, string>
                        {
                            [ActivityConstants.ModuleNameTagKey] = moduleName,
                            [ActivityConstants.CorrelationIdTagKey] = correlationId,
                            [ActivityConstants.FlowIdTagKey] = flowId
                        },
                        cancellationToken: cancellationToken);
            }
            else
            {
                await activitySources.Find(moduleName)
                    .StartActvity(
                        $"QUEUE_HANDLE {messageType} [{moduleName}]",
                        async (_, _) => await next().AnyContext(),
                        ActivityKind.Consumer,
                        parentId,
                        new Dictionary<string, string>
                        {
                            ["queueing.module.origin"] = moduleNameOrigin,
                            ["queueing.message_id"] = message?.MessageId,
                            ["queueing.message_type"] = messageType
                        },
                        new Dictionary<string, string>
                        {
                            [ActivityConstants.ModuleNameTagKey] = moduleName,
                            [ActivityConstants.CorrelationIdTagKey] = correlationId,
                            [ActivityConstants.FlowIdTagKey] = flowId
                        },
                        cancellationToken: cancellationToken);
            }
        }
    }

    private async Task HandleWithActivity(
        IQueueMessage message,
        string messageType,
        string handlerType,
        string moduleName,
        string moduleNameOrigin,
        CancellationToken cancellationToken,
        QueueHandlerDelegate next)
    {
        var currentActivity = Activity.Current;
        if (currentActivity is null)
        {
            await next().AnyContext();
            return;
        }

        await currentActivity.StartActvity(
            $"QUEUE_HANDLE {messageType} -> {handlerType} [{moduleName}]",
            async (_, _) => await next().AnyContext(),
            ActivityKind.Consumer,
            tags: new Dictionary<string, string>
            {
                ["queueing.module.origin"] = moduleNameOrigin,
                ["queueing.message_id"] = message?.MessageId,
                ["queueing.message_type"] = messageType
            },
            cancellationToken: cancellationToken);
    }
}
