// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Application.Messaging;
using Microsoft.Extensions.Logging;
using Constants = BridgingIT.DevKit.Application.Messaging.Constants;

/// <summary>
/// Handles persisted <see cref="TodoItemActivityMessage"/> messages for the DoFiesta example.
/// </summary>
/// <remarks>
/// The handler intentionally keeps side effects light and logs the processed message so the example can
/// demonstrate the full Entity Framework broker lifecycle without introducing extra infrastructure.
/// </remarks>
public class TodoItemActivityMessageHandler(ILoggerFactory loggerFactory) : MessageHandlerBase<TodoItemActivityMessage>(loggerFactory),
    IRetryMessageHandler,
    ITimeoutMessageHandler
{
    RetryMessageHandlerOptions IRetryMessageHandler.Options =>
        new() { Attempts = 3, Backoff = TimeSpan.FromSeconds(1) };

    TimeoutMessageHandlerOptions ITimeoutMessageHandler.Options =>
        new() { Timeout = TimeSpan.FromSeconds(10) };

    /// <summary>
    /// Handles the specified todo item activity message.
    /// </summary>
    /// <param name="message">The todo item activity message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when handling has finished.</returns>
    public override async Task Handle(TodoItemActivityMessage message, CancellationToken cancellationToken)
    {
        using var scope = this.Logger.BeginScope(new Dictionary<string, object>
        {
            ["MessageId"] = message.MessageId,
            ["TodoItemId"] = message.TodoItemId,
        });

        await Task.Delay(300, cancellationToken); // simulate some work

        this.Logger.LogInformation(
            "{LogKey} processed todo activity message (messageId={MessageId}, todoItemId={TodoItemId}, activity={Activity}, status={Status}, title={Title}, handler={HandlerType})",
            Constants.LogKey,
            message.MessageId,
            message.TodoItemId,
            message.Activity,
            message.Status,
            message.Title,
            this.GetType().FullName);
    }
}