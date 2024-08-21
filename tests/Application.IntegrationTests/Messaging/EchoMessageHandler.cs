// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.Messaging;

using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;

public class EchoMessageHandler(ILoggerFactory loggerFactory) : MessageHandlerBase<EchoMessage>(loggerFactory),
    IRetryMessageHandler,
    ITimeoutMessageHandler,
    IChaosExceptionMessageHandler
{
    RetryMessageHandlerOptions IRetryMessageHandler.Options => new() { Attempts = 3, Backoff = new TimeSpan(0, 0, 0, 1) };

    TimeoutMessageHandlerOptions ITimeoutMessageHandler.Options => new() { Timeout = new TimeSpan(0, 0, 0, 10) };

    ChaosExceptionMessageHandlerOptions IChaosExceptionMessageHandler.Options => new() { InjectionRate = 0.25 };

    /// <summary>
    /// Handles the specified message.
    /// </summary>
    /// <param name="message">The event.</param>
    public override async Task Handle(EchoMessage message, CancellationToken cancellationToken)
    {
        var loggerState = new Dictionary<string, object>
        {
            ["MessageId"] = message.MessageId,
        };

        using (this.Logger.BeginScope(loggerState))
        {
            await Task.Delay(2000, cancellationToken);
            //throw new Exception("haha");
            this.Logger.LogInformation($"{{LogKey}} >>>>> MMMMMMMMMM1 echo {message.Text} (name={{MessageName}}, id={{MessageId}}, handler={{}}) ", Constants.LogKey, message.GetType().PrettyName(), message.MessageId, this.GetType().FullName);
        }
    }
}

public class AnotherEchoMessageHandler : IMessageHandler<EchoMessage> // TODO: obsolete
{
    public AnotherEchoMessageHandler(ILogger<AnotherEchoMessageHandler> logger)
    {
        EnsureArg.IsNotNull(logger, nameof(logger));

        this.Logger = logger;
    }

    protected ILogger<AnotherEchoMessageHandler> Logger { get; }

    /// <summary>
    /// Handles the specified message.
    /// </summary>
    /// <param name="message">The event.</param>
    public virtual async Task Handle(EchoMessage message, CancellationToken cancellationToken)
    {
        var loggerState = new Dictionary<string, object>
        {
            ["MessageId"] = message.MessageId,
        };

        using (this.Logger.BeginScope(loggerState))
        {
            await Task.Delay(100, cancellationToken);
            this.Logger.LogInformation($"{{LogKey}} >>>>> MMMMMMMMMM2 another echo {message.Text} (name={{MessageName}}, id={{MessageId}}, handler={{}}) ", Constants.LogKey, message.GetType().PrettyName(), message.MessageId, this.GetType().FullName);
        }
    }
}
