// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Fallback publisher that logs a warning when no publisher is registered.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="NoOpDomainEventPublisher"/> class.
/// </remarks>
/// <param name="loggerFactory">Optional logger factory for logging.</param>
public class NoOpNotifier(ILoggerFactory loggerFactory = null) : INotifier
{
    private readonly ILogger<NoOpNotifier> logger = loggerFactory?.CreateLogger<NoOpNotifier>() ?? NullLogger<NoOpNotifier>.Instance;

    public RegistrationInformation GetRegistrationInformation()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Logs a warning and completes without publishing the event.
    /// </summary>
    /// <param name="notification">The notification to publish.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A completed task.</returns>
    Task<IResult> INotifier.PublishAsync<TNotification>(TNotification notification, PublishOptions options, CancellationToken cancellationToken)
    {
        this.logger.LogWarning("{LogKey} no notifier available. Notification {NotificationType} not published (NotificationId={})", "NOT", notification.GetType().Name, notification.NotificationId);

        return Task.FromResult<IResult>(Result.Success());
    }

    /// <summary>
    /// Logs a warning and completes without publishing the event.
    /// </summary>
    /// <param name="notification">The notification to publish.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A completed task.</returns>
    public Task<IResult> PublishDynamicAsync(INotification notification, PublishOptions options = null, CancellationToken cancellationToken = default)
    {
        this.logger.LogWarning("{LogKey} no notifier available. Notification {NotificationType} not published (NotificationId={})", "NOT", notification.GetType().Name, notification.NotificationId);

        return Task.FromResult<IResult>(Result.Success());
    }
}