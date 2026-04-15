// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Examples.DoFiesta.Domain.Modules.Core;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Handles <see cref="TodoItemCreatedDomainEvent"/> notifications.
/// Triggered whenever a new <see cref="Domain.Model.TodoItem"/> is created.
/// Extend <see cref="Process"/> with integration, logging, or side-effects.
/// </summary>
/// <remarks>
/// Initializes a new instance of the handler with logging support.
/// </remarks>
/// <param name="loggerFactory">Factory used for creating loggers.</param>
/// <param name="broker">Message broker used to persist example activity messages.</param>
public class TodoItemCreatedDomainEventHandler(ILoggerFactory loggerFactory, IMessageBroker broker)
    : DomainEventHandlerBase<TodoItemCreatedDomainEvent>(loggerFactory)
{
    private readonly IMessageBroker broker = broker;

    /// <summary>
    /// Determines whether this handler can handle the given event.
    /// Returns <c>true</c> unconditionally in this template.
    /// </summary>
    public override bool CanHandle(TodoItemCreatedDomainEvent notification) => true;

    /// <summary>
    /// Processes the <see cref="TodoItemCreatedDomainEvent"/>.
    /// Add custom logic here (e.g., start workflows, send welcome mails).
    /// </summary>
    public override async Task Process(TodoItemCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        // implement event reaction logic (audit, notify, etc.)
        this.Logger.LogInformation("DoFiesta - TodoItemCreatedDomainEvent handled in Application " + notification.Model?.Title);

        await this.broker.Publish(
            new TodoItemActivityMessage(
                notification.Model?.Id?.ToString(),
                notification.Model?.Title,
                "Created",
                notification.Model?.Status.ToString()),
            cancellationToken);

    }
}