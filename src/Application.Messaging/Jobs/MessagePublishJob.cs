// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

public sealed class MessagePublishJob<TData, TMessage> : IJob<TData>
    where TMessage : class, IMessage
{
    private readonly IServiceProvider serviceProvider;
    private readonly MessagePublishJobRegistrationStore registrations;

    internal MessagePublishJob(IServiceProvider serviceProvider, MessagePublishJobRegistrationStore registrations)
    {
        this.serviceProvider = serviceProvider;
        this.registrations = registrations;
    }

    public async Task<IResult> ExecuteAsync(IJobExecutionContext<TData> context, CancellationToken cancellationToken = default)
    {
        var settings = this.registrations.Get<TData, TMessage>(context.JobName);
        if (settings is null)
        {
            return JobIntegrationResult.Failure($"No Messaging integration is registered for job '{context.JobName}'.");
        }

        var broker = this.serviceProvider.GetService<IMessageBroker>();
        if (broker is null)
        {
            return JobIntegrationResult.Failure($"IMessageBroker is not registered for job '{context.JobName}'.");
        }

        var message = settings.MessageFactory(context);
        if (message is null)
        {
            return JobIntegrationResult.Failure($"The Messaging job '{context.JobName}' produced a null message payload.");
        }

        settings.MessageConfigurator?.Invoke(context, message);
        ApplyProperties(message.Properties, context, settings.Properties);

        await broker.Publish(message, cancellationToken).ConfigureAwait(false);
        context.Messages.Add($"Messaging integration published '{typeof(TMessage).Name}'.");
        return JobIntegrationResult.Success();
    }

    async Task<IResult> IJob.ExecuteAsync(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        if (context is not IJobExecutionContext<TData> typedContext)
        {
            return JobIntegrationResult.Failure($"The Messaging job '{context.JobName}' expected data contract '{typeof(TData).FullName}'.");
        }

        return await this.ExecuteAsync(typedContext, cancellationToken).ConfigureAwait(false);
    }

    private static void ApplyProperties(
        IDictionary<string, object> properties,
        IJobExecutionContext<TData> context,
        IReadOnlyList<KeyValuePair<string, Func<IJobExecutionContext<TData>, object>>> mappedProperties)
    {
        foreach (var property in mappedProperties)
        {
            var value = property.Value(context);
            if (value is not null)
            {
                properties[property.Key] = value;
            }
        }
    }
}
