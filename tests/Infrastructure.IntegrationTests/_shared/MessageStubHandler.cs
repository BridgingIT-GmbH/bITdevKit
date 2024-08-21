// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests;

using BridgingIT.DevKit.Application.Messaging;
using Microsoft.Extensions.Logging;

public class MessageState
{
    public List<string> HandledMessageIds { get; set; } = [];

    public List<string> HandledMessageResults { get; set; } = [];
}

public class MessageStubHandler(ILoggerFactory loggerFactory, MessageState messageState) : MessageHandlerBase<MessageStub>(loggerFactory),
    IRetryMessageHandler,
    ITimeoutMessageHandler
{
    private readonly MessageState messageState = messageState;

    RetryMessageHandlerOptions IRetryMessageHandler.Options => new() { Attempts = 3, Backoff = new TimeSpan(0, 0, 0, 1) };

    TimeoutMessageHandlerOptions ITimeoutMessageHandler.Options => new() { Timeout = new TimeSpan(0, 0, 0, 10) };

    public override async Task Handle(MessageStub message, CancellationToken cancellationToken)
    {
        var loggerState = new Dictionary<string, object>
        {
            ["MessageId"] = message.MessageId,
        };

        using (this.Logger.BeginScope(loggerState))
        {
            this.messageState.HandledMessageIds.Add(message.MessageId);
            this.messageState.HandledMessageResults.Add($"{message.FirstName} {message.LastName}");
            await Task.Delay(100, cancellationToken);
            //throw new Exception("haha");
            this.Logger.LogInformation($"{{LogKey}} firstname={message.FirstName}, firstname={message.LastName} (name={{MessageName}}, id={{MessageId}}) ", Constants.LogKey, message.GetType().PrettyName(), message.MessageId);
        }
    }
}

public class AnotherMessageStubHandler(ILoggerFactory loggerFactory, MessageState messageState) : MessageHandlerBase<MessageStub>(loggerFactory),
    IRetryMessageHandler,
    ITimeoutMessageHandler
{
    private readonly MessageState messageState = messageState;

    RetryMessageHandlerOptions IRetryMessageHandler.Options => new() { Attempts = 3, Backoff = new TimeSpan(0, 0, 0, 1) };

    TimeoutMessageHandlerOptions ITimeoutMessageHandler.Options => new() { Timeout = new TimeSpan(0, 0, 0, 10) };

    public override async Task Handle(MessageStub message, CancellationToken cancellationToken)
    {
        var loggerState = new Dictionary<string, object>
        {
            ["MessageId"] = message.MessageId,
        };

        using (this.Logger.BeginScope(loggerState))
        {
            this.messageState.HandledMessageIds.Add(message.MessageId);
            this.messageState.HandledMessageResults.Add($"{message.FirstName} {message.LastName}");
            await Task.Delay(100, cancellationToken);
            //throw new Exception("haha");
            this.Logger.LogInformation($"{{LogKey}} firstname={message.FirstName}, firstname={message.LastName} (name={{MessageName}}, id={{MessageId}}) ", Constants.LogKey, message.GetType().PrettyName(), message.MessageId);
        }
    }
}