// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;

using System.Diagnostics;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using BridgingIT.DevKit.Domain;
using Common;
using DevKit.Domain.Model;

[DebuggerDisplay("Id={Id}, Value={Value}")]
public class TodoStatus : Enumeration
{
    public static readonly TodoStatus New = new(1, nameof(New), "Newly created task");
    public static readonly TodoStatus InProgress = new(2, nameof(InProgress), "Task is being worked on");
    public static readonly TodoStatus Completed = new(3, nameof(Completed), "Task has been completed");
    public static readonly TodoStatus Cancelled = new(4, nameof(Cancelled), "Task has been cancelled");

    private TodoStatus(int id, string value, string description)
        : base(id, value)
    {
        this.Description = description;
    }

    public string Description { get; }

    public static IEnumerable<TodoStatus> GetAll()
    {
        return GetAll<TodoStatus>();
    }
}

[DebuggerDisplay("Id={Id}, Value={Value}")]
public class TodoPriority : Enumeration
{
    public static readonly TodoPriority Low = new(1, nameof(Low), "Low priority task");
    public static readonly TodoPriority Medium = new(2, nameof(Medium), "Medium priority task");
    public static readonly TodoPriority High = new(3, nameof(High), "High priority task");
    public static readonly TodoPriority Critical = new(4, nameof(Critical), "Critical priority task");

    private TodoPriority(int id, string value, string description)
        : base(id, value)
    {
        this.Description = description;
    }

    public string Description { get; }

    public static IEnumerable<TodoPriority> GetAll()
    {
        return GetAll<TodoPriority>();
    }
}

[DebuggerDisplay("Id={Id}, Value={Value}")]
public class SubscriptionPlan : Enumeration
{
    public static readonly SubscriptionPlan Free = new(1, "Free", "Basic free plan",
        new SubscriptionPlanDetails("Free", 0, 50, 1, false, false, false, 1, false));

    public static readonly SubscriptionPlan Basic = new(2, "Basic", "Standard plan for individuals",
        new SubscriptionPlanDetails("Basic", 9.99m, 1000, 1, true, true, false, 5, true));

    public static readonly SubscriptionPlan Team = new(3, "Team", "Team collaboration plan",
        new SubscriptionPlanDetails("Team", 29.99m, 10000, 5, true, true, true, 20, true));

    public static readonly SubscriptionPlan Enterprise = new(4, "Enterprise", "Enterprise solution",
        new SubscriptionPlanDetails("Enterprise", 99.99m, -1, 100, true, true, true, -1, true));

    private SubscriptionPlan(int id, string value, string description, SubscriptionPlanDetails details)
        : base(id, value)
    {
        this.Description = description;
        this.Details = details;
    }

    public string Description { get; }
    public SubscriptionPlanDetails Details { get; }

    public static IEnumerable<SubscriptionPlan> GetAll()
    {
        return GetAll<SubscriptionPlan>();
    }
}

[DebuggerDisplay("Id={Id}, Value={Value}")]
public class SubscriptionStatus : Enumeration
{
    public static readonly SubscriptionStatus Pending = new(1, nameof(Pending), "Subscription is awaiting activation");
    public static readonly SubscriptionStatus Active = new(2, nameof(Active), "Subscription is active");
    public static readonly SubscriptionStatus Cancelled = new(3, nameof(Cancelled), "Subscription has been cancelled");
    public static readonly SubscriptionStatus Expired = new(4, nameof(Expired), "Subscription has expired");

    private SubscriptionStatus(int id, string value, string description)
        : base(id, value)
    {
        this.Description = description;
    }

    public string Description { get; }

    public static IEnumerable<SubscriptionStatus> GetAll()
    {
        return GetAll<SubscriptionStatus>();
    }
}

[DebuggerDisplay("Id={Id}, Value={Value}")]
public class SubscriptionBillingCycle : Enumeration
{
    public static readonly SubscriptionBillingCycle Never = new(0, nameof(Never), "One-time payment", false);
    public static readonly SubscriptionBillingCycle Monthly = new(1, nameof(Monthly), "Monthly billing cycle", true);
    public static readonly SubscriptionBillingCycle Yearly = new(2, nameof(Yearly), "Annual billing cycle", true);

    private SubscriptionBillingCycle(int id, string value, string description, bool autoRenew)
        : base(id, value)
    {
        this.Description = description;
        this.AutoRenew = autoRenew;
    }

    public string Description { get; }
    public bool AutoRenew { get; }

    public static IEnumerable<SubscriptionBillingCycle> GetAll()
    {
        return GetAll<SubscriptionBillingCycle>();
    }
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

public class EmailAddress : ValueObject
{
    private EmailAddress() { }

    private EmailAddress(string value)
    {
        this.Value = value;
    }

    public string Value { get; }

    public static implicit operator string(EmailAddress email)
    {
        return email.Value;
    }

    public static EmailAddress Create(string value)
    {
        value = value?.Trim()?.ToLowerInvariant();

        Rule.Add(EmailAddressRules.IsValid(value)).Check();

        return new EmailAddress(value);
    }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return this.Value;
    }
}

public class IsValidEmailAddressRule(string value) : RuleBase
{
    private static readonly Regex Regex = new( // TODO: change to compiled regex (source gen)
        @"^(?("")("".+?(?<!\\)""@)|(([0-9a-z]((\.(?!\.))|[-!#\$%&'\*\+/=\?\^`\{\}\|~\w])*)(?<=[0-9a-z])@))" +
        @"(?(\[)(\[(\d{1,3}\.){3}\d{1,3}\])|(([0-9a-z][-0-9a-z]*[0-9a-z]*\.)+[a-z0-9][\-a-z0-9]{0,22}[a-z0-9]))$",
        RegexOptions.Compiled,
        new TimeSpan(0, 0, 3));

    private readonly string value = value?.ToLowerInvariant();

    public override string Message => "Not a valid email address";

    protected override Result Execute()
    {
        return Result.SuccessIf(!string.IsNullOrEmpty(this.value) &&
            this.value.Length <= 255 &&
            Regex.IsMatch(this.value));
    }
}

public static class EmailAddressRules
{
    public static IRule IsValid(string value)
    {
        return new IsValidEmailAddressRule(value);
    }
}

public class TodoItemIsNotDeletedSpecification : Specification<TodoItem>
{
    public override Expression<Func<TodoItem, bool>> ToExpression()
    {
        return e => !e.AuditState.IsDeleted();
    }
}

public class ActiveSubscriptionSpecification : Specification<Subscription>
{
    public override Expression<Func<Subscription, bool>> ToExpression()
    {
        return e => e.Status == SubscriptionStatus.Active &&
                   (e.EndDate == null || e.EndDate > DateTime.UtcNow);
    }
}

public class TodoItemByUserSpecification(string userId) : Specification<TodoItem>
{
    public override Expression<Func<TodoItem, bool>> ToExpression()
    {
        return item => item.UserId == userId;
    }
}