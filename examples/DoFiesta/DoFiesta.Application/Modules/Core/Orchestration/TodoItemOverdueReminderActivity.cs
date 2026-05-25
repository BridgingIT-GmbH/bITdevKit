// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Application.Notifications;
using BridgingIT.DevKit.Application.Orchestrations;
using Microsoft.Extensions.Logging;

public class TodoItemOverdueReminderActivity(
    INotificationService<EmailMessage> notificationService,
    IOrchestrationClock clock,
    ILoggerFactory loggerFactory) : IOrchestrationActivity<TodoItemLifecycleOrchestrationData>
{
    private readonly INotificationService<EmailMessage> notificationService = notificationService;
    private readonly IOrchestrationClock clock = clock;
    private readonly ILogger<TodoItemOverdueReminderActivity> logger = loggerFactory.CreateLogger<TodoItemOverdueReminderActivity>();

    public async Task<OrchestrationOutcome> ExecuteAsync(
        OrchestrationContext<TodoItemLifecycleOrchestrationData> context,
        CancellationToken cancellationToken = default)
    {
        var data = context.Data;
        var now = this.clock.UtcNow.UtcDateTime;

        if (string.IsNullOrWhiteSpace(data.Assignee))
        {
            data.LastOverdueReminderSentUtc = now;
            data.OverdueReminderCount++;
            data.LastNotificationKind = "OverdueReminderSkipped";
            TodoItemLifecycleOrchestration.AddTrace(data, "overdue-reminder:skipped-no-assignee");
            return OrchestrationOutcome.Continue();
        }

        var result = await this.notificationService.QueueAsync(
            new EmailMessage
            {
                Id = Guid.NewGuid(),
                To = [data.Assignee],
                Subject = $"DoFiesta todo #{data.Number} is overdue",
                Body = BuildBody(data, now),
                IsHtml = false,
                Properties =
                {
                    ["Source"] = "DoFiesta.Orchestration",
                    ["TodoItemId"] = data.TodoItemId.ToString("D"),
                    ["OrchestrationInstanceId"] = context.InstanceId.ToString("D"),
                    ["ReminderKind"] = "OverdueReminder"
                }
            },
            cancellationToken);

        if (result.IsFailure)
        {
            var error = result.Errors.FirstOrDefault()?.Message ?? "Overdue reminder could not be queued.";
            this.logger.LogWarning(
                "DoFiesta overdue reminder could not be queued for todo item {TodoItemId}: {Error}",
                data.TodoItemId,
                error);
            return OrchestrationOutcome.Retry(error);
        }

        data.LastOverdueReminderSentUtc = now;
        data.OverdueReminderCount++;
        data.LastNotificationKind = "OverdueReminderQueued";
        TodoItemLifecycleOrchestration.AddTrace(data, "overdue-reminder:queued");
        return OrchestrationOutcome.Continue();
    }

    private static string BuildBody(TodoItemLifecycleOrchestrationData data, DateTime now)
    {
        var overdueFor = data.DueDate.HasValue ? now - data.DueDate.Value : TimeSpan.Zero;

        return $"""
            The DoFiesta todo item below is overdue and still waiting for completion.

            Number: {data.Number}
            Title: {data.Title}
            Priority: {data.Priority}
            Status: {data.Status}
            Due date: {data.DueDate:yyyy-MM-dd HH:mm}
            Overdue for: {Math.Max(0, (int)Math.Floor(overdueFor.TotalHours))} hour(s)

            Description:
            {data.Description}
            """;
    }
}