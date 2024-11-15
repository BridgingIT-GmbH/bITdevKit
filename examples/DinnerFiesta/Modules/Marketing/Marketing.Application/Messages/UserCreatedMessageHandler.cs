// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Marketing.Application;

using Common;
using DevKit.Application.Messaging;
using MediatR;
using Microsoft.Extensions.Logging;
using Constants = BridgingIT.DevKit.Application.Messaging.Constants;

public class UserCreatedMessageHandler(
    ILoggerFactory loggerFactory,
    IMediator mediator) : MessageHandlerBase<UserCreatedMessage>(loggerFactory),
    IRetryMessageHandler,
    ITimeoutMessageHandler
//IChaosExceptionMessageHandler
{
    RetryMessageHandlerOptions IRetryMessageHandler.Options =>
        new() { Attempts = 3, Backoff = new TimeSpan(0, 0, 0, 1) };

    TimeoutMessageHandlerOptions ITimeoutMessageHandler.Options => new() { Timeout = new TimeSpan(0, 0, 0, 10) };

    //ChaosExceptionMessageHandlerOptions IChaosExceptionMessageHandler.Options => new() { InjectionRate = 0.25 };

    /// <summary>
    ///     Handles the specified message.
    /// </summary>
    /// <param name="message">The event.</param>
    public override async Task Handle(UserCreatedMessage message, CancellationToken cancellationToken)
    {
        var loggerState = new Dictionary<string, object> { ["MessageId"] = message.MessageId };

        using (this.Logger.BeginScope(loggerState))
        {
            var command = new CustomerCreateCommand
            {
                FirstName = message.FirstName, LastName = message.LastName, Email = message.Email
            };
            await mediator.Send(command, cancellationToken).AnyContext();

            this.Logger.LogInformation(
                $"{{LogKey}} >>>>> user created {message.Email} (name={{MessageName}}, id={{MessageId}}, handler={{}}) ",
                Constants.LogKey,
                message.GetType().PrettyName(),
                message.MessageId,
                this.GetType().FullName);
        }
    }
}