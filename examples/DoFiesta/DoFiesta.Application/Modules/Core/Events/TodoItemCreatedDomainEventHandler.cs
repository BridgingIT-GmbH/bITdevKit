// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Application.Notifications;
using BridgingIT.DevKit.Application.Queueing;
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
/// <param name="queueBroker">Queue broker used to enqueue example echo work items.</param>
/// <param name="notificationService">Notification service used to queue outbox email for the assigned user.</param>
public class TodoItemCreatedDomainEventHandler(
    ILoggerFactory loggerFactory,
    IMessageBroker broker,
    IQueueBroker queueBroker,
    INotificationService<EmailMessage> notificationService)
    : DomainEventHandlerBase<TodoItemCreatedDomainEvent>(loggerFactory)
{
    private readonly IMessageBroker broker = broker;
    private readonly IQueueBroker queueBroker = queueBroker;
    private readonly INotificationService<EmailMessage> notificationService = notificationService;

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

        await this.queueBroker.Enqueue(
            new TodoItemEchoQueueMessage(
                notification.Model?.Id?.ToString(),
                notification.Model?.Title,
                "Created",
                notification.Model?.Status.ToString()),
            cancellationToken);

        if (string.IsNullOrWhiteSpace(notification.Model?.Assignee))
        {
            this.Logger.LogWarning(
                "DoFiesta - Skipping notification email for todo item {TodoItemId} because no assignee is present",
                notification.Model?.Id);

            return;
        }

        var result = await this.notificationService.QueueAsync(
            new EmailMessage
            {
                Id = Guid.NewGuid(),
                To = [notification.Model.Assignee],
                Subject = $"DoFiesta Todo #{notification.Model?.Number}: {notification.Model?.Title}",
                Body = BuildBody(notification),
                IsHtml = false,
                Properties =
                {
                    ["Source"] = "DoFiesta.TodoItems",
                    ["TodoItemId"] = notification.Model?.Id?.ToString(),
                    ["TodoItemTitle"] = notification.Model?.Title
                }
            },
            cancellationToken);

        if (result.IsFailure)
        {
            this.Logger.LogWarning(
                "DoFiesta - Failed to queue notification email for todo item {TodoItemId}: {Error}",
                notification.Model?.Id,
                result.Errors?.FirstOrDefault()?.Message);
        }
    }

    private static string BuildBody(TodoItemCreatedDomainEvent notification)
    {
        return $"""
            A new DoFiesta todo was created for you.

            Number: {notification.Model?.Number}
            Title: {notification.Model?.Title}
            Status: {notification.Model?.Status}
            Priority: {notification.Model?.Priority}
            Due date: {notification.Model?.DueDate:yyyy-MM-dd HH:mm}

            Description:
            {notification.Model?.Description}
            """;
    }
}
