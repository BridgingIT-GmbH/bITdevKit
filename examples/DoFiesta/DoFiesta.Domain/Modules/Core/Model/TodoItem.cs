// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;
using DevKit.Domain.Model;

[TypedEntityId<Guid>]
public class TodoItem : AuditableAggregateRoot<TodoItemId>
{
    public string Title { get; set; }

    public string Description { get; set; }

    public TodoStatus Status { get; set; }

    public TodoPriority Priority { get; set; }

    public DateTime? DueDate { get; set; }

    public int OrderIndex { get; set; }

    public string UserId { get; set; }

    public EmailAddress Assignee { get; set; }

    public virtual ICollection<TodoStep> Steps { get; set; } = new List<TodoStep>();
}
