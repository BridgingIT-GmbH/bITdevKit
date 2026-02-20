// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Domain.Modules.Core;

using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

public class TodoItemCreatedDomainEventHandler(ILoggerFactory loggerFactory)
        : DomainEventHandlerBase<EntityCreatedDomainEvent<TodoItem>>(loggerFactory)
{
    /// <summary>
    /// Determines whether this handler can handle the given event.
    /// Returns <c>true</c> unconditionally in this template.
    /// </summary>
    public override bool CanHandle(EntityCreatedDomainEvent<TodoItem> notification) => true;

    /// <summary>
    /// Processes the <see cref="TodoItemCreatedDomainEvent"/>.
    /// Add custom logic here (e.g., start workflows, send welcome mails).
    /// </summary>
    public override Task Process(EntityCreatedDomainEvent<TodoItem> notification, CancellationToken cancellationToken)
    {
        // implement event reaction logic (audit, notify, etc.)
        this.Logger.LogInformation("DoFiesta - TodoItemCreatedDomainEvent handled in Domain " + notification.Entity?.Title);

        return Task.CompletedTask;
    }
}