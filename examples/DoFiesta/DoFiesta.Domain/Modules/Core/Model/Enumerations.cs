// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;

using System.Diagnostics;
using BridgingIT.DevKit.Common;

[DebuggerDisplay("Id={Id}, Value={Value}")]
public partial class TodoStatus : Enumeration
{
    public static readonly TodoStatus New = new(1, nameof(New), true, "Newly created task");
    public static readonly TodoStatus InProgress = new(2, nameof(InProgress), true, "Task is being worked on");
    public static readonly TodoStatus Completed = new(3, nameof(Completed), true, "Task has been completed");
    public static readonly TodoStatus Cancelled = new(4, nameof(Cancelled), false, "Task has been cancelled");
    public static readonly TodoStatus Deleted = new(5, nameof(Deleted), false, "Task has been deleted");

    public bool Enabled { get; private set; }

    public string Description { get; private set; }
}

[DebuggerDisplay("Id={Id}, Value={Value}")]
public partial class TodoPriority : Enumeration
{
    public static readonly TodoPriority Low = new(1, nameof(Low), true, "Low priority task");
    public static readonly TodoPriority Medium = new(2, nameof(Medium), true, "Medium priority task");
    public static readonly TodoPriority High = new(3, nameof(High), true, "High priority task");
    public static readonly TodoPriority Critical = new(4, nameof(Critical), false, "Critical priority task");

    public bool Enabled { get; private set; }

    public string Description { get; private set; }
}

[DebuggerDisplay("Id={Id}, Value={Value}")]
public partial class SubscriptionPlan : Enumeration
{
    public static readonly SubscriptionPlan Free = new(1, "Free", "Basic free plan",
        new SubscriptionPlanDetails("Free", 0, 50, 1, false, false, false, 1, false));

    public static readonly SubscriptionPlan Basic = new(2, "Basic", "Standard plan for individuals",
        new SubscriptionPlanDetails("Basic", 9.99m, 1000, 1, true, true, false, 5, true));

    public static readonly SubscriptionPlan Team = new(3, "Team", "Team collaboration plan",
        new SubscriptionPlanDetails("Team", 29.99m, 10000, 5, true, true, true, 20, true));

    public static readonly SubscriptionPlan Enterprise = new(4, "Enterprise", "Enterprise solution",
        new SubscriptionPlanDetails("Enterprise", 99.99m, -1, 100, true, true, true, -1, true));

    public string Description { get; private set; }

    public SubscriptionPlanDetails Details { get; private set; }
}

[DebuggerDisplay("Id={Id}, Value={Value}")]
public partial class SubscriptionStatus : Enumeration
{
    public static readonly SubscriptionStatus Pending = new(1, nameof(Pending), true, "Subscription is awaiting activation");
    public static readonly SubscriptionStatus Active = new(2, nameof(Active), true, "Subscription is active");
    public static readonly SubscriptionStatus Cancelled = new(3, nameof(Cancelled), true, "Subscription has been cancelled");
    public static readonly SubscriptionStatus Expired = new(4, nameof(Expired), true, "Subscription has expired");

    public bool Enabled { get; private set; }

    public string Description { get; private set; }
}

[DebuggerDisplay("Id={Id}, Value={Value}")]
public partial class SubscriptionBillingCycle : Enumeration
{
    public static readonly SubscriptionBillingCycle Never = new(0, nameof(Never), false, "One-time payment", false);
    public static readonly SubscriptionBillingCycle Monthly = new(1, nameof(Monthly), true, "Monthly billing cycle", true);
    public static readonly SubscriptionBillingCycle Yearly = new(2, nameof(Yearly), true, "Annual billing cycle", true);

    public bool Enabled { get; private set; }

    public string Description { get; private set; }

    public bool AutoRenew { get; private set; }
}

public class SubscriptionPlanDetails(
    string name,
    decimal pricePerMonth,
    int maxTodos,
    int maxUsersPerTodo,
    bool allowsRecurring,
    bool allowsAttachments,
    bool allowsTemplates,
    int maxProjects,
    bool allowsComments)
{
    public string Name { get; } = name;

    public decimal PricePerMonth { get; } = pricePerMonth;

    public int MaxTodos { get; } = maxTodos;

    public int MaxUsersPerTodo { get; } = maxUsersPerTodo;

    public bool AllowsRecurring { get; } = allowsRecurring;

    public bool AllowsAttachments { get; } = allowsAttachments;

    public bool AllowsTemplates { get; } = allowsTemplates;

    public int MaxProjects { get; } = maxProjects;

    public bool AllowsComments { get; } = allowsComments;
}