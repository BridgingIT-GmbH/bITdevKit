// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

using System.Diagnostics;

public class ModuleScopeMessageHandlerBehavior(
    ILoggerFactory loggerFactory,
    IEnumerable<IModuleContextAccessor> moduleAccessors = null,
    IEnumerable<ActivitySource> activitySources = null) : MessageHandlerBehaviorBase(loggerFactory)
{
    public override async Task Handle<TMessage>(
        TMessage message,
        CancellationToken cancellationToken,
        object handler,
        MessageHandlerDelegate next)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var moduleNameOrigin = message?.Properties?.GetValue(ModuleConstants.ModuleNameOriginKey)?.ToString().EmptyToNull() ?? ModuleConstants.UnknownModuleName;
        var correlationId = message?.Properties?.GetValue(Constants.CorrelationIdKey)?.ToString();
        var flowId = message?.Properties?.GetValue(Constants.FlowIdKey)?.ToString();
        var parentId = message?.Properties?.GetValue(ModuleConstants.ActivityParentIdKey)?.ToString();

        var module = moduleAccessors.Find(handler?.GetType()); //this.moduleAccessors.Find(message?.GetType());
        var moduleName = module?.Name ?? ModuleConstants.UnknownModuleName;

        using (this.Logger.BeginScope(new Dictionary<string, object>
               {
                   [ModuleConstants.ModuleNameKey] = moduleName,
                   [ModuleConstants.ModuleNameOriginKey] = moduleNameOrigin,
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
                    .StartActvity($"MODULE {moduleName}",
                        async (a, c) =>
                        {
                            using (this.Logger.BeginScope(new Dictionary<string, object>
                                   {
                                       ["TraceId"] = a.TraceId.ToString()
                                   }))
                            {
                                await Activity.Current.StartActvity($"{Constants.TraceOperationHandleName} {messageType} -> {handlerType} [{moduleName}]",
                                    async (a, c) => await next().AnyContext(),
                                    ActivityKind.Consumer,
                                    tags: new Dictionary<string, string>
                                    {
                                        ["messaging.module.origin"] = moduleNameOrigin,
                                        ["messaging.message_id"] = message?.MessageId,
                                        ["messaging.message_type"] = messageType
                                    },
                                    cancellationToken: c);
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
                    .StartActvity($"{Constants.TraceOperationHandleName} {messageType} [{moduleName}]",
                        async (a, c) => await next().AnyContext(),
                        ActivityKind.Consumer,
                        parentId,
                        new Dictionary<string, string>
                        {
                            ["messaging.module.origin"] = moduleNameOrigin,
                            ["messaging.message_id"] = message?.MessageId,
                            ["messaging.message_type"] = messageType
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
}