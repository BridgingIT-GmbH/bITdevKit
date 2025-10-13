// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;

using System.Diagnostics;
using DevKit.Domain.Model;

[DebuggerDisplay("Id={Id}, UserId={UserId}, Status={Status}, Title={Title}")]
[TypedEntityId<Guid>]
public class TodoItem : AuditableAggregateRoot<TodoItemId>, IConcurrency
{
    public string UserId { get; set; }

    public long Number { get; set; }

    public string Title { get; set; }

    public string Description { get; set; }

    public string Category { get; set; }

    public TodoStatus Status { get; set; }

    public void SetStatus(TodoStatus status)
    {
        this.Status = status;
    }

    public void SetCompleted()
    {
        this.Status = TodoStatus.Completed;
    }

    public TodoPriority Priority { get; set; }

    public DateTime? DueDate { get; set; }

    public int OrderIndex { get; set; }

    public EmailAddress Assignee { get; set; }

    public virtual ICollection<TodoStep> Steps { get; set; } = [];

    public Guid ConcurrencyVersion { get; set; }
}
