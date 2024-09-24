// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;

using DevKit.Application.Messaging;
using DevKit.Domain;
using Domain;
using Microsoft.Extensions.Logging;

public class UserCreatedDomainEventHandler(
    ILoggerFactory loggerFactory,
    IMessageBroker messageBroker)
    : DomainEventHandlerBase<UserCreatedDomainEvent>(loggerFactory)
{
    public override bool CanHandle(UserCreatedDomainEvent notification)
    {
        return true;
    }

    public override async Task Process(UserCreatedDomainEvent @event, CancellationToken cancellationToken)
    {
        EnsureArg.IsNotNull(@event, nameof(@event));

        // forward the domain event as a message so it can be handled by other modules
        await messageBroker.Publish(
            new UserCreatedMessage { FirstName = @event.FirstName, LastName = @event.LastName, Email = @event.Email },
            cancellationToken);
    }
}