// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Application.Orchestrations;
using BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;

/// <summary>
/// Tracks one todo item through its durable lifecycle, including due-date waiting,
/// lead reminders, overdue reminders, completion, and deletion.
/// </summary>
/// <remarks>
/// <para>
/// The orchestration keeps a persisted snapshot of the todo item in
/// <see cref="TodoItemLifecycleOrchestrationData"/> and re-evaluates reminder timing
/// whenever a synchronization or delete signal arrives.
/// </para>
/// <para>
/// In DoFiesta this orchestration is intended to run once per todo item and is exposed
/// through the operations dashboard for inspection, signaling, and lifecycle control.
/// </para>
/// <para>State diagram:</para>
/// <code>
/// [*] --start--&gt; Monitor
/// Monitor --TodoUpdated signal--&gt; Monitor
/// Monitor --TodoDeleted signal--&gt; Cancelled
/// Monitor --Status == Completed--&gt; Completed
/// Monitor --Deleted || Status == Cancelled/Deleted--&gt; Cancelled
/// Monitor --ShouldSendLeadReminder--&gt; LeadReminder
/// Monitor --ShouldSendOverdueReminder--&gt; OverdueReminder
/// LeadReminder --activity complete--&gt; Monitor
/// OverdueReminder --activity complete--&gt; Monitor
/// Completed --terminal--&gt; [*]
/// Cancelled --terminal--&gt; [*]
/// </code>
/// <para>
/// While the instance is in <c>Monitor</c>, <see cref="TodoItemLifecycleAssessmentActivity"/>
/// can also return a durable wait outcome. That wait does not transition to another business
/// state; it suspends execution in <c>Monitor</c> until either a matching signal arrives or a
/// persisted timer resumes the orchestration.
/// </para>
/// </remarks>
public class TodoItemLifecycleOrchestration : Orchestration<TodoItemLifecycleOrchestrationData>
{
    /// <summary>
    /// Defines the durable state machine for monitoring and reminding on a single todo item.
    /// </summary>
    /// <param name="builder">The orchestration builder used to define states, signals, and transitions.</param>
    protected override void Define(IOrchestrationBuilder<TodoItemLifecycleOrchestrationData> builder)
    {
        builder
            // Monitor is the durable hub state. It evaluates the current todo snapshot,
            // reacts to external signals, and either waits, reminds, or terminates.
            .State("Monitor", state => state
                .Activity<TodoItemLifecycleAssessmentActivity>()
                .WaitForSignal<TodoItemLifecycleUpdateSignal>(TodoItemLifecycleSignals.Updated, signal => signal
                    .MapToContext((context, payload) => ApplyUpdate(context.Data, payload))
                    .TransitionTo("Monitor"))
                .WaitForSignal<TodoItemLifecycleDeletedSignal>(TodoItemLifecycleSignals.Deleted, signal => signal
                    .MapToContext((context, payload) => ApplyDeleted(context.Data, payload))
                    .TransitionTo("Cancelled"))
                .TransitionTo("Completed", context => IsCompleted(context.Data.Status))
                .TransitionTo("Cancelled", context => context.Data.Deleted || IsCancelled(context.Data.Status))
                .TransitionTo("LeadReminder", context => context.Data.ShouldSendLeadReminder)
                .TransitionTo("OverdueReminder", context => context.Data.ShouldSendOverdueReminder))
            // Reminder states are short-lived. They perform the side effect and then loop
            // back into Monitor so the next wait or reminder decision is recalculated.
            .State("LeadReminder", state => state
                .Activity<TodoItemLeadReminderActivity>(activity => activity
                    .Retry(new OrchestrationRetryPolicy
                    {
                        MaxAttempts = 3,
                        Delay = TimeSpan.FromSeconds(10),
                        BackoffMode = OrchestrationRetryBackoffMode.FixedDelay,
                    }))
                .TransitionTo("Monitor"))
            .State("OverdueReminder", state => state
                .Activity<TodoItemOverdueReminderActivity>(activity => activity
                    .Retry(new OrchestrationRetryPolicy
                    {
                        MaxAttempts = 3,
                        Delay = TimeSpan.FromSeconds(10),
                        BackoffMode = OrchestrationRetryBackoffMode.FixedDelay,
                    }))
                .TransitionTo("Monitor"))
            .State("Completed", state => state
                .Complete("Todo item completed."))
            .State("Cancelled", state => state
                .Cancel("Todo item was removed from the work queue."));
    }

    internal static void AddTrace(TodoItemLifecycleOrchestrationData data, string entry)
    {
        if (data?.Trace is null || string.IsNullOrWhiteSpace(entry))
        {
            return;
        }

        // Keep only a compact rolling trace so the persisted context stays readable.
        data.Trace.Add(entry);
        if (data.Trace.Count > 32)
        {
            data.Trace.RemoveRange(0, data.Trace.Count - 32);
        }
    }

    private static void ApplyUpdate(TodoItemLifecycleOrchestrationData data, TodoItemLifecycleUpdateSignal payload)
    {
        var dueDateChanged = data.DueDate != NormalizeUtc(payload.DueDate);
        var priorityChanged = !string.Equals(data.Priority, payload.Priority, StringComparison.OrdinalIgnoreCase);
        var assigneeChanged = !string.Equals(data.Assignee, payload.Assignee, StringComparison.OrdinalIgnoreCase);

        data.Title = payload.Title;
        data.Description = payload.Description;
        data.Assignee = payload.Assignee;
        data.Status = payload.Status;
        data.Priority = payload.Priority;
        data.DueDate = NormalizeUtc(payload.DueDate);
        data.LastSynchronizedBy = payload.SynchronizedBy;
        data.LastSynchronizedUtc = NormalizeUtc(payload.SynchronizedUtc);
        data.Deleted = false;
        data.DeletedReason = null;

        if (dueDateChanged || priorityChanged || assigneeChanged)
        {
            // When reminder-driving inputs change, the orchestration must re-plan both the
            // lead reminder window and the overdue reminder cadence from the updated snapshot.
            data.LeadReminderSentUtc = null;

            if (data.DueDate.HasValue)
            {
                data.LastOverdueReminderSentUtc = null;
                data.OverdueReminderCount = 0;
            }
        }

        AddTrace(data, $"signal:updated:{payload.Status}");
    }

    private static void ApplyDeleted(TodoItemLifecycleOrchestrationData data, TodoItemLifecycleDeletedSignal payload)
    {
        data.Deleted = true;
        data.Status = TodoStatus.Deleted.Value;
        data.DeletedReason = payload.Reason;
        data.LastSynchronizedBy = payload.DeletedBy;
        data.LastSynchronizedUtc = NormalizeUtc(payload.DeletedUtc);

        AddTrace(data, "signal:deleted");
    }

    private static bool IsCompleted(string status)
    {
        return string.Equals(status, TodoStatus.Completed.Value, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCancelled(string status)
    {
        return string.Equals(status, TodoStatus.Cancelled.Value, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, TodoStatus.Deleted.Value, StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime? NormalizeUtc(DateTime? value)
    {
        return value.HasValue ? NormalizeUtc(value.Value) : null;
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