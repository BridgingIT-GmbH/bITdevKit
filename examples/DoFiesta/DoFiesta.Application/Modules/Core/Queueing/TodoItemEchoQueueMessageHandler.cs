// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Application.Queueing;
using Microsoft.Extensions.Logging;

/// <summary>
/// Handles <see cref="TodoItemEchoQueueMessage"/> messages for the DoFiesta queueing sample.
/// </summary>
/// <remarks>
/// The handler logs the echoed todo-item activity so operators can observe queue processing end-to-end.
/// </remarks>
/// <param name="loggerFactory">The logger factory.</param>
public class TodoItemEchoQueueMessageHandler(ILoggerFactory loggerFactory) : IQueueMessageHandler<TodoItemEchoQueueMessage>
{
    private readonly ILogger<TodoItemEchoQueueMessageHandler> logger = loggerFactory?.CreateLogger<TodoItemEchoQueueMessageHandler>()
        ?? throw new ArgumentNullException(nameof(loggerFactory));

    /// <summary>
    /// Handles the queued echo message.
    /// </summary>
    /// <param name="message">The queued echo message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public Task Handle(TodoItemEchoQueueMessage message, CancellationToken cancellationToken)
    {
        this.logger.LogInformation(
            "DoFiesta queue echo processed (messageId={MessageId}, todoItemId={TodoItemId}, activity={Activity}, status={Status}, title={Title})",
            message.MessageId,
            message.TodoItemId,
            message.Activity,
            message.Status,
            message.Title);

        return Task.CompletedTask;
    }
}