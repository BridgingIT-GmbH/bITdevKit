// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using EnsureThat;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public partial class MediatorDomainEventPublisher : IDomainEventPublisher
{
    private readonly ILogger<MediatorDomainEventPublisher> logger;
    private readonly IMediator mediator;

    public MediatorDomainEventPublisher(ILoggerFactory loggerFactory, IMediator mediator)
    {
        EnsureArg.IsNotNull(mediator, nameof(mediator));

        this.logger = loggerFactory?.CreateLogger<MediatorDomainEventPublisher>() ?? NullLoggerFactory.Instance.CreateLogger<MediatorDomainEventPublisher>();
        this.mediator = mediator;
    }

    public async Task Send(IDomainEvent @event, CancellationToken cancellationToken = default)
    {
        if (@event is not null)
        {
            TypedLogger.LogSend(this.logger, Constants.LogKey, @event.EventId, @event.GetType().Name);

            await this.mediator.Publish(@event, cancellationToken).AnyContext();
        }
    }

    public static partial class TypedLogger
    {
        [LoggerMessage(0, LogLevel.Information, "{LogKey} publish domain event (eventId={DomainEventId}, eventType={DomainEventType})")]
        public static partial void LogSend(ILogger logger, string logKey, Guid domainEventId, string domainEventType);
    }
}