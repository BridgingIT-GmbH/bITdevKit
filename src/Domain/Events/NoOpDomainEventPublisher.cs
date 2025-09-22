// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Fallback publisher that logs a warning when no publisher is registered.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="NoOpDomainEventPublisher"/> class.
/// </remarks>
/// <param name="loggerFactory">Optional logger factory for logging.</param>
public class NoOpDomainEventPublisher(ILoggerFactory loggerFactory = null) : IDomainEventPublisher
{
    private readonly ILogger<NoOpDomainEventPublisher> logger = loggerFactory?.CreateLogger<NoOpDomainEventPublisher>() ?? NullLogger<NoOpDomainEventPublisher>.Instance;

    /// <summary>
    /// Logs a warning and completes without publishing the event.
    /// </summary>
    /// <param name="event">The domain event to publish.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A completed task.</returns>
    public Task<IResult> Send(IDomainEvent @event, CancellationToken cancellationToken = default)
    {
        this.logger.LogWarning("{LogKey} no domain events publisher available. Domain event {EventType} not published (EventId={})", Constants.LogKey, @event.GetType().Name, @event.EventId);
        return Task.FromResult<IResult>(Result.Success());
    }
}