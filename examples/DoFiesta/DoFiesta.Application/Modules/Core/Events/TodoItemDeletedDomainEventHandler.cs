// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Examples.DoFiesta.Domain.Modules.Core;
using Microsoft.Extensions.Logging;

/// <summary>
/// Handles <see cref="TodoItemDeletedDomainEvent"/> notifications.
/// Triggered whenever a <see cref="Domain.Model.TodoItem"/> is deleted.
/// Extend <see cref="Process"/> with integration, logging, or side-effects.
/// </summary>
/// <remarks>
/// Initializes a new instance of the handler with logging support.
/// </remarks>
/// <param name="loggerFactory">Factory used for creating loggers.</param>
/// <param name="broker">Message broker used to persist example activity messages.</param>
public class TodoItemDeletedDomainEventHandler(ILoggerFactory loggerFactory, IMessageBroker broker)
    : DomainEventHandlerBase<TodoItemDeletedDomainEvent>(loggerFactory)
{
    private readonly IMessageBroker broker = broker;

    /// <summary>
    /// Determines whether this handler can handle the given event.
    /// Returns <c>true</c> unconditionally in this template.
    /// </summary>
    public override bool CanHandle(TodoItemDeletedDomainEvent notification) => true;

    /// <summary>
    /// Processes the <see cref="TodoItemDeletedDomainEvent"/>.
    /// Add custom logic here (e.g., start workflows, send mails, or publish messages).
    /// </summary>
    public override async Task Process(TodoItemDeletedDomainEvent notification, CancellationToken cancellationToken)
    {
        this.Logger.LogInformation("DoFiesta - TodoItemDeletedDomainEvent handled in Application " + notification.Model?.Title);

        await this.broker.Publish(
            new TodoItemActivityMessage(
                notification.Model?.Id?.ToString(),
                notification.Model?.Title,
                "Deleted",
                notification.Model?.Status.ToString()),
            cancellationToken);
    }
}
