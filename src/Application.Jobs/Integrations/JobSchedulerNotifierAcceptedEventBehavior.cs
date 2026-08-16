// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Accepts published notifications into the scheduler event-trigger pipeline.
/// </summary>
public sealed class JobSchedulerNotifierAcceptedEventBehavior<TNotification, TResult> : IPipelineBehavior<TNotification, TResult>
    where TNotification : class, INotification
    where TResult : IResult
{
    private readonly IJobEventIngress ingress;
    private readonly ILogger<JobSchedulerNotifierAcceptedEventBehavior<TNotification, TResult>> logger;

    /// <summary>
    /// Initializes a new instance of the <c>JobSchedulerNotifierAcceptedEventBehavior</c> class.
    /// </summary>
    /// <param name="ingress">The ingress used by the operation.</param>
    /// <param name="loggerFactory">The factory used to create loggers.</param>
    public JobSchedulerNotifierAcceptedEventBehavior(
        IJobEventIngress ingress,
        ILoggerFactory loggerFactory = null)
    {
        this.ingress = ingress ?? throw new ArgumentNullException(nameof(ingress));
        this.logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<JobSchedulerNotifierAcceptedEventBehavior<TNotification, TResult>>();
    }

    /// <summary>
    /// Handles .
    /// </summary>
    /// <param name="request">The request used by the operation.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="handlerType">The handler type used by the operation.</param>
    /// <param name="next">The next used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<TResult> HandleAsync(TNotification request, object options, Type handlerType, Func<Task<TResult>> next, CancellationToken cancellationToken = default)
    {
        var result = await next().ConfigureAwait(false);
        if (result.IsFailure)
        {
            return result;
        }

        var acceptResult = await this.ingress.AcceptAsync(
            JobEventSourceNames.Notifier,
            request,
            BuildOptions(options as PublishOptions),
            cancellationToken).ConfigureAwait(false);

        if (acceptResult.IsFailure)
        {
            var message = acceptResult.Messages.FirstOrDefault() ?? acceptResult.Errors.FirstOrDefault()?.Message ?? "Notifier event acceptance failed.";
            this.logger.LogWarning("[Jobs] notifier event acceptance failed (notification={NotificationType}, message={Message})", typeof(TNotification).FullName, message);
            return (TResult)(object)Result.Failure().WithErrors(acceptResult.Errors).WithMessages(acceptResult.Messages.Any() ? acceptResult.Messages : [message]);
        }

        return result;
    }

    /// <summary>
    /// Determines whether is handler specific.
    /// </summary>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public bool IsHandlerSpecific() => false;

    private static JobAcceptedEventOptions BuildOptions(PublishOptions options)
    {
        var properties = options?.Context?.Properties;
        PropertyBag acceptedEventProperties = null;
        if (properties is not null)
        {
            acceptedEventProperties = new PropertyBag();
            foreach (var (key, value) in properties)
            {
                acceptedEventProperties[key] = value;
            }
        }

        return new JobAcceptedEventOptions
        {
            SourceId = GetProperty(properties, JobEventContextPropertyNames.SourceId),
            IdempotencyKey = GetProperty(properties, JobEventContextPropertyNames.IdempotencyKey),
            CorrelationId = GetProperty(properties, JobEventContextPropertyNames.CorrelationId),
            Properties = acceptedEventProperties,
        };
    }

    private static string GetProperty(IReadOnlyDictionary<string, string> properties, string key)
        => properties is not null && properties.TryGetValue(key, out var value) ? value : null;
}
