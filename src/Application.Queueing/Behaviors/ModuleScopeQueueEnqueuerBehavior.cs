// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Queueing;

using System.Diagnostics;

/// <summary>
/// Adds module, tracing, and logging scope metadata around queue enqueue operations.
/// </summary>
/// <example>
/// <code>
/// services.AddQueueing()
///     .WithBehavior&lt;ModuleScopeQueueEnqueuerBehavior&gt;();
/// </code>
/// </example>
public class ModuleScopeQueueEnqueuerBehavior(
    ILoggerFactory loggerFactory,
    IEnumerable<IModuleContextAccessor> moduleAccessors = null) : IQueueEnqueuerBehavior
{
    private readonly ILogger<ModuleScopeQueueEnqueuerBehavior> logger =
        (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<ModuleScopeQueueEnqueuerBehavior>();

    /// <summary>
    /// Wraps queue enqueue execution in the current module scope and propagates transport metadata.
    /// </summary>
    /// <param name="message">The queue message being enqueued.</param>
    /// <param name="cancellationToken">The enqueue cancellation token.</param>
    /// <param name="next">The next enqueue delegate.</param>
    /// <returns>A task that completes when enqueue processing has finished.</returns>
    public async Task Enqueue(IQueueMessage message, CancellationToken cancellationToken, QueueEnqueuerDelegate next)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var module = moduleAccessors.Find(message?.GetType());
        var moduleName = module?.Name ?? ModuleConstants.UnknownModuleName;
        this.PropagateContext(message, moduleName);

        using (this.logger.BeginScope(new Dictionary<string, object>
        {
            [ModuleConstants.ModuleNameKey] = moduleName,
            [Constants.CorrelationIdKey] = message?.Properties?.GetValue(Constants.CorrelationIdKey)?.ToString(),
            [Constants.FlowIdKey] = message?.Properties?.GetValue(Constants.FlowIdKey)?.ToString()
        }))
        {
            if (module?.Enabled == false)
            {
                throw new ModuleNotEnabledException(moduleName);
            }

            var messageType = message?.GetType().PrettyName(false);

            var currentActivity = Activity.Current;
            if (currentActivity is null)
            {
                await next().AnyContext();
                return;
            }

            await currentActivity.StartActvity(
                $"QUEUE_ENQUEUE {messageType} [{moduleName}]",
                async (activity, _) =>
                {
                    if (message?.Properties?.ContainsKey(ModuleConstants.ActivityParentIdKey) == false)
                    {
                        message.Properties.Add(ModuleConstants.ActivityParentIdKey, activity?.Id);
                    }

                    await next().AnyContext();
                },
                ActivityKind.Producer,
                tags: new Dictionary<string, string>
                {
                    ["queueing.module.origin"] = message?.Properties?.GetValue(ModuleConstants.ModuleNameOriginKey)?.ToString(),
                    ["queueing.message_id"] = message?.MessageId,
                    ["queueing.message_type"] = messageType
                },
                baggages: new Dictionary<string, string>
                {
                    [ActivityConstants.ModuleNameTagKey] = moduleName,
                    [ActivityConstants.CorrelationIdTagKey] = message?.Properties?.GetValue(Constants.CorrelationIdKey)?.ToString(),
                    [ActivityConstants.FlowIdTagKey] = message?.Properties?.GetValue(Constants.FlowIdKey)?.ToString()
                },
                cancellationToken: cancellationToken);
        }
    }

    private void PropagateContext(IQueueMessage message, string moduleName)
    {
        if (message?.Properties?.ContainsKey(ModuleConstants.ModuleNameOriginKey) == false)
        {
            message.Properties.Add(ModuleConstants.ModuleNameOriginKey, moduleName);
        }

        if (message?.Properties?.ContainsKey(Constants.CorrelationIdKey) == false)
        {
            var correlationId = Activity.Current?.GetBaggageItem(ActivityConstants.CorrelationIdTagKey);
            message?.Properties?.Add(Constants.CorrelationIdKey, correlationId);
        }

        if (message?.Properties?.ContainsKey(Constants.FlowIdKey) == false)
        {
            var flowId = Activity.Current?.GetBaggageItem(ActivityConstants.FlowIdTagKey);
            message?.Properties?.Add(Constants.FlowIdKey, flowId);
        }
    }
}
