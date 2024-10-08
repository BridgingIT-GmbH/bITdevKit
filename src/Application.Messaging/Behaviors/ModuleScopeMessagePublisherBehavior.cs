﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

using System.Diagnostics;
using Common;
using Microsoft.Extensions.Logging;

public class ModuleScopeMessagePublisherBehavior(
    ILoggerFactory loggerFactory,
    IEnumerable<IModuleContextAccessor> moduleAccessors = null,
    IEnumerable<ActivitySource> activitySources = null) : MessagePublisherBehaviorBase(loggerFactory)
{
    private readonly IEnumerable<IModuleContextAccessor> moduleAccessors = moduleAccessors;
    private readonly IEnumerable<ActivitySource> activitySources = activitySources;

    public override async Task Publish<TMessage>(
        TMessage message,
        CancellationToken cancellationToken,
        MessagePublisherDelegate next)
    {
        var module = this.moduleAccessors.Find(message.GetType());
        var moduleName = module?.Name ?? ModuleConstants.UnknownModuleName;

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        using (this.Logger.BeginScope(new Dictionary<string, object> { [ModuleConstants.ModuleNameKey] = moduleName }))
        {
            if (module?.Enabled == false)
            {
                throw new ModuleNotEnabledException(moduleName);
            }

            this.PropagateContext(message, moduleName);
            var messageType = message?.GetType().PrettyName(false);

            await this.activitySources.Find(moduleName)
                .StartActvity($"MESSAGE_PUBLISH {messageType}",
                    async (a, c) =>
                    {
                        if (message?.Properties?.ContainsKey(ModuleConstants.ActivityParentIdKey) == false)
                        {
                            message?.Properties?.Add(ModuleConstants.ActivityParentIdKey, a?.Id); // propagate parent activity id
                        }

                        await next().AnyContext();
                    },
                    ActivityKind.Producer,
                    tags: new Dictionary<string, string>
                    {
                        ["messaging.module.origin"] = message?.Properties?.GetValue(ModuleConstants.ModuleNameOriginKey)?.ToString(),
                        ["messaging.message_id"] = message?.MessageId,
                        ["messaging.message_type"] = messageType
                    },
                    cancellationToken: cancellationToken);
        }
    }

    private void PropagateContext<TMessage>(TMessage message, string moduleName)
        where TMessage : IMessage
    {
        if (message?.Properties?.ContainsKey(ModuleConstants.ModuleNameOriginKey) == false)
        {
            message?.Properties?.Add(ModuleConstants.ModuleNameOriginKey, moduleName);
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

        if (message?.Properties?.ContainsKey(Constants.TimestampKey) == false)
        {
            message?.Properties?.Add(Constants.TimestampKey, message.Timestamp.ToUnixTimeSeconds());
        }
    }
}