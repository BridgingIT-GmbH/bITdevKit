// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging.Jobs
{
    using BridgingIT.DevKit.Application.Jobs;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Accepts published messages into the scheduler event-trigger pipeline.
    /// </summary>
    public sealed class JobSchedulerMessagePublisherAcceptedEventBehavior : MessagePublisherBehaviorBase
    {
        private readonly IJobEventIngress ingress;

        public JobSchedulerMessagePublisherAcceptedEventBehavior(
            IJobEventIngress ingress,
            ILoggerFactory loggerFactory = null)
            : base(loggerFactory)
        {
            this.ingress = ingress ?? throw new ArgumentNullException(nameof(ingress));
        }

        public override async Task Publish<TMessage>(TMessage message, CancellationToken cancellationToken, MessagePublisherDelegate next)
        {
            await next().ConfigureAwait(false);

            var acceptResult = await this.ingress.AcceptAsync(
                JobEventSourceNames.Messaging,
                message,
                typeof(TMessage),
                new JobAcceptedEventOptions
                {
                    SourceId = message?.MessageId,
                    IdempotencyKey = message?.MessageId,
                    CorrelationId = message?.Properties?.TryGetValue(JobEventContextPropertyNames.CorrelationId, out var correlationId) == true ? correlationId?.ToString() : null,
                    Properties = message?.Properties is null
                        ? null
                        : new PropertyBag(message.Properties.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase)),
                    AcceptedUtc = message?.Timestamp,
                },
                cancellationToken).ConfigureAwait(false);

            if (acceptResult.IsFailure)
            {
                var messageText = acceptResult.Messages.FirstOrDefault() ?? acceptResult.Errors.FirstOrDefault()?.Message ?? "Messaging event acceptance failed.";
                this.Logger.LogWarning("[Jobs] messaging event acceptance failed (message={MessageType}, messageId={MessageId}, error={Error})", typeof(TMessage).FullName, message?.MessageId, messageText);
                throw new InvalidOperationException(messageText);
            }
        }
    }
}

namespace Microsoft.Extensions.DependencyInjection
{
    using BridgingIT.DevKit.Application.Jobs;
    using BridgingIT.DevKit.Application.Messaging;
    using BridgingIT.DevKit.Application.Messaging.Jobs;

    /// <summary>
    /// Provides messaging adapter registration helpers for scheduler event triggers.
    /// </summary>
    public static class JobSchedulerMessagingEventTriggerExtensions
    {
        /// <summary>
        /// Connects Messaging publish operations to the scheduler event-trigger pipeline.
        /// </summary>
        public static MessagingBuilderContext UseJobSchedulerEventTriggers(this MessagingBuilderContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            EnsureJobs(context.Services).Register(JobEventSourceNames.Messaging);
            context.WithBehavior(sp => new JobSchedulerMessagePublisherAcceptedEventBehavior(
                sp.GetRequiredService<IJobEventIngress>(),
                sp.GetService<ILoggerFactory>()));
            return context;
        }

        private static JobEventSourceRegistry EnsureJobs(IServiceCollection services)
        {
            var descriptor = services.FirstOrDefault(x => x.ServiceType == typeof(JobEventSourceRegistry));
            if (descriptor?.ImplementationInstance is JobEventSourceRegistry registry)
            {
                return registry;
            }

            throw new InvalidOperationException("Messaging event triggers require AddJobScheduler() to be configured before UseJobSchedulerEventTriggers().");
        }
    }
}