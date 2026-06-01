// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Queueing.Jobs
{
    using BridgingIT.DevKit.Application.Jobs;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;

    /// <summary>
    /// Accepts queued messages into the scheduler event-trigger pipeline.
    /// </summary>
    public sealed class JobSchedulerQueueEnqueuerAcceptedEventBehavior : IQueueEnqueuerBehavior
    {
        private readonly IJobEventIngress ingress;
        private readonly ILogger<JobSchedulerQueueEnqueuerAcceptedEventBehavior> logger;

        public JobSchedulerQueueEnqueuerAcceptedEventBehavior(
            IJobEventIngress ingress,
            ILoggerFactory loggerFactory = null)
        {
            this.ingress = ingress ?? throw new ArgumentNullException(nameof(ingress));
            this.logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<JobSchedulerQueueEnqueuerAcceptedEventBehavior>();
        }

        public async Task Enqueue(IQueueMessage message, CancellationToken cancellationToken, QueueEnqueuerDelegate next)
        {
            await next().ConfigureAwait(false);

            var acceptResult = await this.ingress.AcceptAsync(
                JobEventSourceNames.Queueing,
                message,
                message?.GetType() ?? typeof(IQueueMessage),
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
                var messageText = acceptResult.Messages.FirstOrDefault() ?? acceptResult.Errors.FirstOrDefault()?.Message ?? "Queueing event acceptance failed.";
                this.logger.LogWarning("[Jobs] queueing event acceptance failed (message={MessageType}, messageId={MessageId}, error={Error})", message?.GetType().FullName, message?.MessageId, messageText);
                throw new InvalidOperationException(messageText);
            }
        }
    }
}

namespace Microsoft.Extensions.DependencyInjection
{
    using BridgingIT.DevKit.Application.Jobs;
    using BridgingIT.DevKit.Application.Queueing;
    using BridgingIT.DevKit.Application.Queueing.Jobs;

    /// <summary>
    /// Provides queueing adapter registration helpers for scheduler event triggers.
    /// </summary>
    public static class JobSchedulerQueueingEventTriggerExtensions
    {
        /// <summary>
        /// Connects Queueing enqueue operations to the scheduler event-trigger pipeline.
        /// </summary>
        public static QueueingBuilderContext UseJobSchedulerEventTriggers(this QueueingBuilderContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            EnsureJobs(context.Services).Register(JobEventSourceNames.Queueing);
            context.WithBehavior(sp => new JobSchedulerQueueEnqueuerAcceptedEventBehavior(
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

            throw new InvalidOperationException("Queueing event triggers require AddJobScheduler() to be configured before UseJobSchedulerEventTriggers().");
        }
    }
}