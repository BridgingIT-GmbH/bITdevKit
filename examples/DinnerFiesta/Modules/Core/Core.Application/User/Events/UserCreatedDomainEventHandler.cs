﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;

using System.Threading.Tasks;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;
using Microsoft.Extensions.Logging;
using BridgingIT.DevKit.Application.Messaging;

public class UserCreatedDomainEventHandler(
    ILoggerFactory loggerFactory,
    IMessageBroker messageBroker) : DomainEventHandlerBase<UserCreatedDomainEvent>(loggerFactory)
{
    public override bool CanHandle(UserCreatedDomainEvent notification)
    {
        return true;
    }

    public override async Task Process(UserCreatedDomainEvent @event, CancellationToken cancellationToken)
    {
        EnsureArg.IsNotNull(@event, nameof(@event));
        EnsureArg.IsNotNull(@event.User, nameof(@event.User));

        var message = new UserCreatedMessage { FirstName = @event.User.FirstName, LastName = @event.User.LastName, Email = @event.User.Email };
        await messageBroker.Publish(message, cancellationToken);
    }
}