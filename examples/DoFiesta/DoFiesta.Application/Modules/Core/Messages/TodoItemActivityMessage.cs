// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Application.Messaging;

/// <summary>
/// Represents a durable broker message describing a todo item activity in the DoFiesta example.
/// </summary>
/// <remarks>
/// This message is published from the application's domain-event handlers so the Entity Framework broker
/// and operational messaging endpoints have real persisted traffic to inspect.
/// </remarks>
/// <example>
/// <code>
/// await broker.Publish(
///     new TodoItemActivityMessage(todoItem.Id.ToString(), todoItem.Title, "Created", todoItem.Status.ToString()),
///     cancellationToken);
/// </code>
/// </example>
public class TodoItemActivityMessage : MessageBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TodoItemActivityMessage"/> class.
    /// </summary>
    public TodoItemActivityMessage()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TodoItemActivityMessage"/> class.
    /// </summary>
    /// <param name="todoItemId">The todo item identifier.</param>
    /// <param name="title">The todo item title.</param>
    /// <param name="activity">The activity name, such as Created or Updated.</param>
    /// <param name="status">The todo item status snapshot.</param>
    public TodoItemActivityMessage(string todoItemId, string title, string activity, string status)
    {
        this.TodoItemId = todoItemId;
        this.Title = title;
        this.Activity = activity;
        this.Status = status;
        this.OccurredOn = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Gets or sets the todo item identifier.
    /// </summary>
    public string TodoItemId { get; set; }

    /// <summary>
    /// Gets or sets the todo item title snapshot.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the activity name.
    /// </summary>
    public string Activity { get; set; }

    /// <summary>
    /// Gets or sets the todo item status snapshot.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the activity message was created.
    /// </summary>
    public DateTimeOffset OccurredOn { get; set; } = DateTimeOffset.UtcNow;
}