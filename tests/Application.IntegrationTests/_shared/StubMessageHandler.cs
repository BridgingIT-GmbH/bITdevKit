// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.Messaging;

using BridgingIT.DevKit.Application.Messaging;
using Microsoft.Extensions.Logging;

public class StubMessageHandler(ILoggerFactory loggerFactory) : MessageHandlerBase<StubMessage>(loggerFactory),
    IRetryMessageHandler,
    ITimeoutMessageHandler
{
    public static bool Processed { get; set; }

    public static string Result { get; set; }

    RetryMessageHandlerOptions IRetryMessageHandler.Options => new() { Attempts = 3, Backoff = new TimeSpan(0, 0, 0, 1) };

    TimeoutMessageHandlerOptions ITimeoutMessageHandler.Options => new() { Timeout = new TimeSpan(0, 0, 0, 10) };

    public override async Task Handle(StubMessage message, CancellationToken cancellationToken)
    {
        var loggerState = new Dictionary<string, object>
        {
            ["MessageId"] = message.MessageId,
        };

        using (this.Logger.BeginScope(loggerState))
        {
            Processed = true;
            Result = $"{message.FirstName} {message.Ticks}";
            await Task.Delay(100, cancellationToken);
            //throw new Exception("haha");
            this.Logger.LogInformation($"{{LogKey}} firstname={message.FirstName} (name={{MessageName}}, id={{MessageId}}) ", Constants.LogKey, message.GetType().PrettyName(), message.MessageId);
        }
    }
}

public class AnotherStubMessageHandler(ILoggerFactory loggerFactory) : MessageHandlerBase<StubMessage>(loggerFactory),
    IRetryMessageHandler,
    ITimeoutMessageHandler
{
    public static bool Processed { get; set; }

    public static string Result { get; set; }

    RetryMessageHandlerOptions IRetryMessageHandler.Options => new() { Attempts = 3, Backoff = new TimeSpan(0, 0, 0, 1) };

    TimeoutMessageHandlerOptions ITimeoutMessageHandler.Options => new() { Timeout = new TimeSpan(0, 0, 0, 10) };

    public override async Task Handle(StubMessage message, CancellationToken cancellationToken)
    {
        var loggerState = new Dictionary<string, object>
        {
            ["MessageId"] = message.MessageId,
        };

        using (this.Logger.BeginScope(loggerState))
        {
            Processed = true;
            Result = $"{message.FirstName} {message.Ticks}";
            await Task.Delay(100, cancellationToken);
            //throw new Exception("haha");
            this.Logger.LogInformation($"{{LogKey}} firstname={message.FirstName} (name={{MessageName}}, id={{MessageId}}) ", Constants.LogKey, message.GetType().PrettyName(), message.MessageId);
        }
    }
}