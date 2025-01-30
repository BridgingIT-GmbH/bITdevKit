// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;

public class TodoItemModel
{
    public string Id { get; set; }

    public string UserId { get; set; }

    public string Title { get; set; }

    public string Description { get; set; }

    public int Status { get; set; }

    public int Priority { get; set; }

    public DateTimeOffset? DueDate { get; set; }

    public int OrderIndex { get; set; }

    public string Assignee { get; set; }

    public ICollection<TodoStepModel> Steps { get; set; } = [];

    public string ConcurrencyVersion { get; set; }
}

public class TodoStepModel
{
    public string Id { get; set; }

    public string TodoItemId { get; set; }

    public string Description { get; set; }

    public int Status { get; set; }

    public int OrderIndex { get; set; }

    public bool? IsDeleted { get; set; }
}

public class SubscriptionModel
{
    public string Id { get; set; }

    public string UserId { get; set; }

    public int Plan { get; set; }

    public int Status { get; set; }

    public int BillingCycle { get; set; }

    public DateTimeOffset StartDate { get; set; }

    public DateTimeOffset? EndDate { get; set; }

    public string ConcurrencyVersion { get; set; }
}

public class EnumerationModel
{
    public IEnumerable<TodoStatus> TodoStatuses { get; set; } = TodoStatus.GetAll();

    public IEnumerable<TodoPriority> TodoPriorities { get; set; } = TodoPriority.GetAll();

    public IEnumerable<SubscriptionStatus> SubscriptionStatuses { get; set; } = SubscriptionStatus.GetAll();

    public IEnumerable<SubscriptionPlan> SubscriptionPlans { get; set; } = SubscriptionPlan.GetAll();

    public IEnumerable<SubscriptionBillingCycle> SubscriptionBillingCycles { get; set; } = SubscriptionBillingCycle.GetAll();
}