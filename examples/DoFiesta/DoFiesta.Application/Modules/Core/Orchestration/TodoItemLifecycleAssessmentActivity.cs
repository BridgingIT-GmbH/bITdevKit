// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Application.Orchestrations;
using BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;

public class TodoItemLifecycleAssessmentActivity(IOrchestrationClock clock) : IOrchestrationActivity<TodoItemLifecycleOrchestrationData>
{
    private readonly IOrchestrationClock clock = clock;

    public Task<OrchestrationOutcome> ExecuteAsync(
        OrchestrationContext<TodoItemLifecycleOrchestrationData> context,
        CancellationToken cancellationToken = default)
    {
        var data = context.Data;
        data.ShouldSendLeadReminder = false;
        data.ShouldSendOverdueReminder = false;

        if (data.Deleted || IsClosedStatus(data.Status))
        {
            TodoItemLifecycleOrchestration.AddTrace(data, "monitor:closed");
            return Task.FromResult(OrchestrationOutcome.Continue());
        }

        if (!data.DueDate.HasValue)
        {
            TodoItemLifecycleOrchestration.AddTrace(data, "monitor:no-due-date");
            return Task.FromResult(OrchestrationOutcome.Wait("Waiting for a due date or status change."));
        }

        var now = this.clock.UtcNow.UtcDateTime;
        var dueDateUtc = NormalizeUtc(data.DueDate.Value);
        data.DueDate = dueDateUtc;

        if (!data.LeadReminderSentUtc.HasValue)
        {
            var leadReminderUtc = dueDateUtc.Add(-GetLeadReminderWindow(data.Priority));
            if (leadReminderUtc <= now && dueDateUtc > now)
            {
                data.ShouldSendLeadReminder = true;
                TodoItemLifecycleOrchestration.AddTrace(data, "monitor:lead-reminder-due");
                return Task.FromResult(OrchestrationOutcome.Continue());
            }

            if (leadReminderUtc > now)
            {
                TodoItemLifecycleOrchestration.AddTrace(data, "monitor:waiting-lead-reminder");
                return Task.FromResult(OrchestrationOutcome.Wait(leadReminderUtc - now, "Waiting for the lead reminder window."));
            }
        }

        if (dueDateUtc <= now)
        {
            var nextReminderUtc = data.LastOverdueReminderSentUtc.HasValue
                ? data.LastOverdueReminderSentUtc.Value.AddHours(24)
                : now;

            if (!data.LastOverdueReminderSentUtc.HasValue || nextReminderUtc <= now)
            {
                data.ShouldSendOverdueReminder = true;
                TodoItemLifecycleOrchestration.AddTrace(data, "monitor:overdue-reminder-due");
                return Task.FromResult(OrchestrationOutcome.Continue());
            }

            TodoItemLifecycleOrchestration.AddTrace(data, "monitor:waiting-overdue-reminder");
            return Task.FromResult(OrchestrationOutcome.Wait(nextReminderUtc - now, "Waiting for the next overdue reminder."));
        }

        TodoItemLifecycleOrchestration.AddTrace(data, "monitor:waiting-due-date");
        return Task.FromResult(OrchestrationOutcome.Wait(dueDateUtc - now, "Waiting until the todo item is due."));
    }

    private static bool IsClosedStatus(string status)
    {
        return string.Equals(status, TodoStatus.Completed.Value, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, TodoStatus.Cancelled.Value, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, TodoStatus.Deleted.Value, StringComparison.OrdinalIgnoreCase);
    }

    private static TimeSpan GetLeadReminderWindow(string priority)
    {
        return priority?.Trim().ToUpperInvariant() switch
        {
            nameof(TodoPriority.Critical) => TimeSpan.FromHours(1),
            nameof(TodoPriority.High) => TimeSpan.FromHours(4),
            nameof(TodoPriority.Medium) => TimeSpan.FromHours(12),
            _ => TimeSpan.FromHours(24),
        };
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
    }
}