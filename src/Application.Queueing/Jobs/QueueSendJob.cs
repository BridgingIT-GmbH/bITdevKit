// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Queueing;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

public sealed class QueueSendJob<TData, TMessage> : IJob<TData>
    where TMessage : class, IQueueMessage
{
    private readonly IServiceProvider serviceProvider;
    private readonly QueueSendJobRegistrationStore registrations;

    internal QueueSendJob(IServiceProvider serviceProvider, QueueSendJobRegistrationStore registrations)
    {
        this.serviceProvider = serviceProvider;
        this.registrations = registrations;
    }

    public async Task<IResult> ExecuteAsync(IJobExecutionContext<TData> context, CancellationToken cancellationToken = default)
    {
        var settings = this.registrations.Get<TData, TMessage>(context.JobName);
        if (settings is null)
        {
            return JobIntegrationResult.Failure($"No Queueing integration is registered for job '{context.JobName}'.");
        }

        var broker = this.serviceProvider.GetService<IQueueBroker>();
        if (broker is null)
        {
            return JobIntegrationResult.Failure($"IQueueBroker is not registered for job '{context.JobName}'.");
        }

        var message = settings.MessageFactory(context);
        if (message is null)
        {
            return JobIntegrationResult.Failure($"The Queueing job '{context.JobName}' produced a null queue message payload.");
        }

        settings.MessageConfigurator?.Invoke(context, message);
        foreach (var property in settings.Properties)
        {
            var value = property.Value(context);
            if (value is not null)
            {
                message.Properties[property.Key] = value;
            }
        }

        if (settings.WaitForPersistence)
        {
            await broker.EnqueueAndWait(message, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await broker.Enqueue(message, cancellationToken).ConfigureAwait(false);
        }

        context.Messages.Add($"Queueing integration sent '{typeof(TMessage).Name}'.");
        return JobIntegrationResult.Success();
    }

    async Task<IResult> IJob.ExecuteAsync(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        if (context is not IJobExecutionContext<TData> typedContext)
        {
            return JobIntegrationResult.Failure($"The Queueing job '{context.JobName}' expected data contract '{typeof(TData).FullName}'.");
        }

        return await this.ExecuteAsync(typedContext, cancellationToken).ConfigureAwait(false);
    }
}
