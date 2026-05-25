// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Application.Notifications;
using BridgingIT.DevKit.Application.Orchestrations;
using Microsoft.Extensions.Logging;

public class TodoItemLeadReminderActivity(
    INotificationService<EmailMessage> notificationService,
    IOrchestrationClock clock,
    ILoggerFactory loggerFactory) : IOrchestrationActivity<TodoItemLifecycleOrchestrationData>
{
    private readonly INotificationService<EmailMessage> notificationService = notificationService;
    private readonly IOrchestrationClock clock = clock;
    private readonly ILogger<TodoItemLeadReminderActivity> logger = loggerFactory.CreateLogger<TodoItemLeadReminderActivity>();

    public async Task<OrchestrationOutcome> ExecuteAsync(
        OrchestrationContext<TodoItemLifecycleOrchestrationData> context,
        CancellationToken cancellationToken = default)
    {
        var data = context.Data;
        var now = this.clock.UtcNow.UtcDateTime;

        if (string.IsNullOrWhiteSpace(data.Assignee))
        {
            data.LeadReminderSentUtc = now;
            data.LastNotificationKind = "LeadReminderSkipped";
            TodoItemLifecycleOrchestration.AddTrace(data, "lead-reminder:skipped-no-assignee");
            return OrchestrationOutcome.Continue();
        }

        var result = await this.notificationService.QueueAsync(
            new EmailMessage
            {
                Id = Guid.NewGuid(),
                To = [data.Assignee],
                Subject = $"DoFiesta todo #{data.Number} is approaching its due date",
                Body = BuildBody(data),
                IsHtml = false,
                Properties =
                {
                    ["Source"] = "DoFiesta.Orchestration",
                    ["TodoItemId"] = data.TodoItemId.ToString("D"),
                    ["OrchestrationInstanceId"] = context.InstanceId.ToString("D"),
                    ["ReminderKind"] = "LeadReminder"
                }
            },
            cancellationToken);

        if (result.IsFailure)
        {
            var error = result.Errors.FirstOrDefault()?.Message ?? "Lead reminder could not be queued.";
            this.logger.LogWarning(
                "DoFiesta lead reminder could not be queued for todo item {TodoItemId}: {Error}",
                data.TodoItemId,
                error);
            return OrchestrationOutcome.Retry(error);
        }

        data.LeadReminderSentUtc = now;
        data.LastNotificationKind = "LeadReminderQueued";
        TodoItemLifecycleOrchestration.AddTrace(data, "lead-reminder:queued");
        return OrchestrationOutcome.Continue();
    }

    private static string BuildBody(TodoItemLifecycleOrchestrationData data)
    {
        return $"""
            The DoFiesta todo item below is approaching its due date.

            Number: {data.Number}
            Title: {data.Title}
            Priority: {data.Priority}
            Status: {data.Status}
            Due date: {data.DueDate:yyyy-MM-dd HH:mm}

            Description:
            {data.Description}
            """;
    }
}