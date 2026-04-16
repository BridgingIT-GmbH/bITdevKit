// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Application.Queueing;

/// <summary>
/// Represents a queue-backed echo message for todo-item activity in the DoFiesta sample.
/// </summary>
/// <example>
/// <code>
/// await queueBroker.Enqueue(
///     new TodoItemEchoQueueMessage(todoItem.Id.ToString(), todoItem.Title, "Created", todoItem.Status.ToString()),
///     cancellationToken);
/// </code>
/// </example>
public class TodoItemEchoQueueMessage : QueueMessageBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TodoItemEchoQueueMessage"/> class.
    /// </summary>
    public TodoItemEchoQueueMessage()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TodoItemEchoQueueMessage"/> class.
    /// </summary>
    /// <param name="todoItemId">The todo-item identifier.</param>
    /// <param name="title">The todo-item title.</param>
    /// <param name="activity">The activity that triggered the queue message.</param>
    /// <param name="status">The todo-item status at the time of enqueue.</param>
    public TodoItemEchoQueueMessage(string todoItemId, string title, string activity, string status)
    {
        this.TodoItemId = todoItemId;
        this.Title = title;
        this.Activity = activity;
        this.Status = status;
        this.EchoText = $"Todo '{title}' was {activity} with status '{status}'.";
    }

    /// <summary>
    /// Gets or sets the todo-item identifier.
    /// </summary>
    public string TodoItemId { get; set; }

    /// <summary>
    /// Gets or sets the todo-item title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the activity name.
    /// </summary>
    public string Activity { get; set; }

    /// <summary>
    /// Gets or sets the todo-item status.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the human-readable echo text.
    /// </summary>
    public string EchoText { get; set; }
}