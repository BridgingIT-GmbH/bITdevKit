// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Jobs;

using System.Text.Json;
using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Common;

/// <summary>
/// Provides MCP operations for the DevKit jobs scheduler.
/// </summary>
/// <example>
/// <code>
/// services.AddJobScheduler().AddMcpHandlers();
/// </code>
/// </example>
public sealed class JobSchedulerMcpHandler(
    IJobSchedulerQueryService query,
    IJobSchedulerService scheduler,
    IJobSchedulerMaintenanceService maintenance) : IMcpHandler
{
    /// <inheritdoc />
    public IReadOnlyCollection<McpCapability> Capabilities { get; } =
    [
        Capability("jobs.list", McpToolset.Diagnostics, "Lists registered jobs."),
        Capability("jobs.details", McpToolset.Diagnostics, "Returns job definition, trigger, and recent occurrence details."),
        Capability("jobs.runs", McpToolset.Diagnostics, "Returns retained job execution attempts."),
        Capability("jobs.runStats", McpToolset.Diagnostics, "Returns aggregate job scheduler metrics."),
        Capability("jobs.trigger", McpToolset.Operations, "Dispatches a job through its manual trigger."),
        Capability("jobs.pause", McpToolset.Operations, "Pauses a registered job."),
        Capability("jobs.resume", McpToolset.Operations, "Resumes a paused job."),
        Capability("jobs.interrupt", McpToolset.Operations, "Interrupts a running job occurrence."),
        Capability("jobs.purgeRuns", McpToolset.Admin, "Purges retained terminal job occurrences."),
        Capability("investigate.jobRun", McpToolset.Diagnostics, "Aggregates diagnostics for a job execution or occurrence.")
    ];

    /// <inheritdoc />
    public async ValueTask<McpResponse> HandleAsync(McpRequest request, CancellationToken cancellationToken)
        => request.Operation switch
        {
            "jobs.list" => await this.ListAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            "jobs.details" => await this.DetailsAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            "jobs.runs" => await this.RunsAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            "jobs.runStats" => await this.RunStatsAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            "jobs.trigger" => await this.TriggerAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            "jobs.pause" => await this.PauseAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            "jobs.resume" => await this.ResumeAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            "jobs.interrupt" => await this.InterruptAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            "jobs.purgeRuns" => await this.PurgeRunsAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            "investigate.jobRun" => await this.InvestigateJobRunAsync(request.Arguments, cancellationToken).ConfigureAwait(false),
            _ => McpResponse.Unavailable(McpErrorCode.FeatureUnavailable, $"Operation '{request.Operation}' is not handled by jobs.")
        };

    private async Task<McpResponse> ListAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var result = await query.QueryJobsAsync(new JobSchedulerJobQueryRequest
        {
            JobName = McpArgumentReader.GetString(arguments, "jobName", McpArgumentReader.GetString(arguments, "name")),
            Group = McpArgumentReader.GetString(arguments, "group"),
            Module = McpArgumentReader.GetString(arguments, "module"),
            Enabled = McpArgumentReader.GetBoolean(arguments, "enabled"),
            Paused = McpArgumentReader.GetBoolean(arguments, "paused"),
            Take = ClampTake(arguments, 50, 500),
            SortBy = McpArgumentReader.GetString(arguments, "sortBy", "JobName"),
            SortDescending = McpArgumentReader.GetBoolean(arguments, "sortDescending", false) ?? false
        }, cancellationToken).ConfigureAwait(false);

        return result.IsFailure
            ? Failure(result, "Job list query failed.")
            : McpResponse.Success(
                $"Returned {result.Value.Count()} registered job{(result.Value.Count() == 1 ? string.Empty : "s")}.",
                new
                {
                    jobs = result.Value,
                    page = Page(result)
                });
    }

    private async Task<McpResponse> DetailsAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var jobName = Required(arguments, "jobName");
        if (jobName.Response is not null)
        {
            return jobName.Response;
        }

        var jobs = await query.QueryJobsAsync(new JobSchedulerJobQueryRequest
        {
            JobName = jobName.Value,
            Take = 5,
            SortBy = "JobName",
            SortDescending = false
        }, cancellationToken).ConfigureAwait(false);
        if (jobs.IsFailure)
        {
            return Failure(jobs, $"Job '{jobName.Value}' details query failed.");
        }

        var job = jobs.Value.FirstOrDefault(item => string.Equals(item.JobName, jobName.Value, StringComparison.OrdinalIgnoreCase)) ??
            jobs.Value.FirstOrDefault();
        if (job is null)
        {
            return McpResponse.Unavailable(McpErrorCode.FeatureUnavailable, $"Job '{jobName.Value}' was not found.");
        }

        var triggers = await query.QueryTriggersAsync(new JobSchedulerTriggerQueryRequest
        {
            JobName = jobName.Value,
            Take = 50,
            SortBy = "TriggerName",
            SortDescending = false
        }, cancellationToken).ConfigureAwait(false);
        var occurrences = await query.QueryOccurrencesAsync(new JobSchedulerOccurrenceQueryRequest
        {
            JobName = jobName.Value,
            Take = ClampTake(arguments, 10, 100),
            SortBy = "CreatedDate",
            SortDescending = true
        }, cancellationToken).ConfigureAwait(false);

        return McpResponse.Success(
            $"Returned details for job '{job.JobName}'.",
            new
            {
                job,
                triggers = triggers.IsSuccess ? triggers.Value : [],
                recentOccurrences = occurrences.IsSuccess ? occurrences.Value : [],
                warnings = FailureMessages(triggers).Concat(FailureMessages(occurrences)).ToArray()
            });
    }

    private async Task<McpResponse> RunsAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var result = await query.QueryExecutionsAsync(new JobSchedulerExecutionQueryRequest
        {
            JobName = McpArgumentReader.GetString(arguments, "jobName", McpArgumentReader.GetString(arguments, "name")),
            TriggerName = McpArgumentReader.GetString(arguments, "triggerName", McpArgumentReader.GetString(arguments, "trigger")),
            TriggerType = McpArgumentReader.GetEnum<JobTriggerType>(arguments, "triggerType"),
            Statuses = McpArgumentReader.GetEnumArray<JobExecutionStatus>(arguments, "statuses"),
            SchedulerInstanceId = McpArgumentReader.GetString(arguments, "schedulerInstanceId"),
            CorrelationId = McpArgumentReader.GetString(arguments, "correlationId"),
            IdempotencyKey = McpArgumentReader.GetString(arguments, "idempotencyKey"),
            StartedFrom = McpArgumentReader.GetDateTimeOffset(arguments, "startedFrom", McpArgumentReader.GetDateTimeOffset(arguments, "from")),
            StartedTo = McpArgumentReader.GetDateTimeOffset(arguments, "startedTo", McpArgumentReader.GetDateTimeOffset(arguments, "to")),
            CompletedFrom = McpArgumentReader.GetDateTimeOffset(arguments, "completedFrom"),
            CompletedTo = McpArgumentReader.GetDateTimeOffset(arguments, "completedTo"),
            Take = ClampTake(arguments, 50, 500),
            SortBy = McpArgumentReader.GetString(arguments, "sortBy", "StartedUtc"),
            SortDescending = McpArgumentReader.GetBoolean(arguments, "sortDescending", true) ?? true
        }, cancellationToken).ConfigureAwait(false);

        return result.IsFailure
            ? Failure(result, "Job execution query failed.")
            : McpResponse.Success(
                $"Returned {result.Value.Count()} job execution{(result.Value.Count() == 1 ? string.Empty : "s")}.",
                new
                {
                    executions = result.Value,
                    page = Page(result)
                });
    }

    private async Task<McpResponse> RunStatsAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var result = await query.GetMetricsAsync(new JobSchedulerMetricsRequest
        {
            JobName = McpArgumentReader.GetString(arguments, "jobName", McpArgumentReader.GetString(arguments, "name")),
            TriggerName = McpArgumentReader.GetString(arguments, "triggerName", McpArgumentReader.GetString(arguments, "trigger")),
            TriggerType = McpArgumentReader.GetEnum<JobTriggerType>(arguments, "triggerType"),
            OccurrenceStatuses = McpArgumentReader.GetEnumArray<JobOccurrenceStatus>(arguments, "occurrenceStatuses"),
            ExecutionStatuses = McpArgumentReader.GetEnumArray<JobExecutionStatus>(arguments, "executionStatuses"),
            SchedulerInstanceId = McpArgumentReader.GetString(arguments, "schedulerInstanceId"),
            DueFrom = McpArgumentReader.GetDateTimeOffset(arguments, "dueFrom", McpArgumentReader.GetDateTimeOffset(arguments, "from")),
            DueTo = McpArgumentReader.GetDateTimeOffset(arguments, "dueTo", McpArgumentReader.GetDateTimeOffset(arguments, "to")),
            CompletedFrom = McpArgumentReader.GetDateTimeOffset(arguments, "completedFrom"),
            CompletedTo = McpArgumentReader.GetDateTimeOffset(arguments, "completedTo")
        }, cancellationToken).ConfigureAwait(false);

        return result.IsFailure
            ? Failure(result, "Job metrics query failed.")
            : McpResponse.Success("Returned job scheduler metrics.", new { metrics = result.Value });
    }

    private async Task<McpResponse> TriggerAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var jobName = Required(arguments, "jobName");
        if (jobName.Response is not null)
        {
            return jobName.Response;
        }

        var wait = McpArgumentReader.GetBoolean(arguments, "wait", false) ?? false;
        if (wait)
        {
            var waitResult = await scheduler.DispatchAndWaitAsync(
                jobName.Value,
                GetDispatchData(arguments),
                timeout: TimeSpan.FromSeconds(Math.Max(1, McpArgumentReader.GetInt32(arguments, "timeout", 60) ?? 60)),
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return waitResult.IsFailure
                ? Failure(waitResult, $"Job '{jobName.Value}' dispatch failed.")
                : McpResponse.Success($"Job '{jobName.Value}' completed with status '{waitResult.Value.Status}'.", new { execution = waitResult.Value });
        }

        var result = await scheduler.DispatchAsync(jobName.Value, GetDispatchData(arguments), cancellationToken: cancellationToken).ConfigureAwait(false);
        return result.IsFailure
            ? Failure(result, $"Job '{jobName.Value}' dispatch failed.")
            : McpResponse.Success($"Dispatched job '{jobName.Value}'.", new { dispatch = result.Value });
    }

    private async Task<McpResponse> PauseAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var jobName = Required(arguments, "jobName");
        if (jobName.Response is not null)
        {
            return jobName.Response;
        }

        var result = await scheduler.PauseJobAsync(jobName.Value, McpArgumentReader.GetString(arguments, "reason"), cancellationToken).ConfigureAwait(false);
        return result.IsFailure
            ? Failure(result, $"Job '{jobName.Value}' pause failed.")
            : McpResponse.Success($"Paused job '{jobName.Value}'.", new { jobName = jobName.Value });
    }

    private async Task<McpResponse> ResumeAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var jobName = Required(arguments, "jobName");
        if (jobName.Response is not null)
        {
            return jobName.Response;
        }

        var result = await scheduler.ResumeJobAsync(jobName.Value, McpArgumentReader.GetString(arguments, "reason"), cancellationToken).ConfigureAwait(false);
        return result.IsFailure
            ? Failure(result, $"Job '{jobName.Value}' resume failed.")
            : McpResponse.Success($"Resumed job '{jobName.Value}'.", new { jobName = jobName.Value });
    }

    private async Task<McpResponse> InterruptAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var occurrenceId = McpArgumentReader.GetGuid(arguments, "occurrenceId");
        if (!occurrenceId.HasValue)
        {
            var jobName = Required(arguments, "jobName");
            if (jobName.Response is not null)
            {
                return McpResponse.Unavailable(
                    McpErrorCode.OperationFailed,
                    "occurrenceId is required when jobName is not supplied.",
                    "Supply occurrenceId, or supply jobName to interrupt the latest running occurrence for that job.");
            }

            var running = await query.QueryOccurrencesAsync(new JobSchedulerOccurrenceQueryRequest
            {
                JobName = jobName.Value,
                Statuses = [JobOccurrenceStatus.Running],
                Take = 1,
                SortBy = "UpdatedDate",
                SortDescending = true
            }, cancellationToken).ConfigureAwait(false);
            if (running.IsFailure)
            {
                return Failure(running, $"Running occurrence lookup for job '{jobName.Value}' failed.");
            }

            occurrenceId = running.Value.FirstOrDefault()?.OccurrenceId;
            if (!occurrenceId.HasValue)
            {
                return McpResponse.Unavailable(McpErrorCode.FeatureUnavailable, $"Job '{jobName.Value}' has no running occurrence to interrupt.");
            }
        }

        var result = await scheduler.InterruptOccurrenceAsync(occurrenceId.Value, McpArgumentReader.GetString(arguments, "reason"), cancellationToken).ConfigureAwait(false);
        return result.IsFailure
            ? Failure(result, $"Occurrence '{occurrenceId.Value}' interrupt failed.")
            : McpResponse.Success($"Interrupt requested for occurrence '{occurrenceId.Value}'.", new { occurrenceId });
    }

    private async Task<McpResponse> PurgeRunsAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        if (!HasConfirmation(arguments, "purge job runs"))
        {
            return McpResponse.Unavailable("confirmation_required", "Job run purge requires confirmation.", "Set confirm=true and confirmation='purge job runs'.");
        }

        var report = await maintenance.PurgeOccurrencesAsync(new JobPurgeOccurrencesRequest
        {
            JobName = McpArgumentReader.GetString(arguments, "jobName", McpArgumentReader.GetString(arguments, "name")),
            TriggerName = McpArgumentReader.GetString(arguments, "triggerName", McpArgumentReader.GetString(arguments, "trigger")),
            OlderThan = McpArgumentReader.GetDateTimeOffset(arguments, "olderThan"),
            IsArchived = McpArgumentReader.GetBoolean(arguments, "isArchived"),
            Statuses = McpArgumentReader.GetEnumArray<JobOccurrenceStatus>(arguments, "statuses"),
            DryRun = McpArgumentReader.GetBoolean(arguments, "dryRun", false) ?? false,
            BatchSize = Math.Max(1, McpArgumentReader.GetInt32(arguments, "batchSize", 100) ?? 100)
        }, cancellationToken).ConfigureAwait(false);

        return McpResponse.Success($"Purged job occurrences for {report.Operation}.", new { report });
    }

    private async Task<McpResponse> InvestigateJobRunAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var occurrenceId = McpArgumentReader.GetGuid(arguments, "occurrenceId");
        var executionId = McpArgumentReader.GetGuid(arguments, "executionId");
        var jobName = McpArgumentReader.GetString(arguments, "jobName", McpArgumentReader.GetString(arguments, "name"));

        var executions = await query.QueryExecutionsAsync(new JobSchedulerExecutionQueryRequest
        {
            JobName = jobName,
            Take = ClampTake(arguments, 20, 100),
            SortBy = "StartedUtc",
            SortDescending = true
        }, cancellationToken).ConfigureAwait(false);
        var history = await query.QueryExecutionHistoryAsync(new JobSchedulerExecutionHistoryQueryRequest
        {
            OccurrenceId = occurrenceId,
            ExecutionId = executionId,
            JobName = jobName,
            Take = ClampTake(arguments, 50, 200),
            SortBy = "RecordedAt",
            SortDescending = true
        }, cancellationToken).ConfigureAwait(false);

        return McpResponse.Success(
            "Returned job run investigation data.",
            new
            {
                filter = new { jobName, occurrenceId, executionId },
                executions = executions.IsSuccess ? executions.Value : [],
                history = history.IsSuccess ? history.Value : [],
                warnings = FailureMessages(executions).Concat(FailureMessages(history)).ToArray()
            });
    }

    private static object GetDispatchData(JsonElement arguments)
    {
        var data = McpArgumentReader.GetObjectDictionary(arguments, "data");
        return data ?? (object)McpArgumentReader.GetString(arguments, "data");
    }

    private static int ClampTake(JsonElement arguments, int defaultValue, int maximum)
        => Math.Min(Math.Max(1, McpArgumentReader.GetInt32(arguments, "take", defaultValue) ?? defaultValue), maximum);

    private static NamedValue Required(JsonElement arguments, string name)
    {
        var value = McpArgumentReader.GetString(arguments, name);

        return string.IsNullOrWhiteSpace(value)
            ? new NamedValue(null, McpResponse.Unavailable(McpErrorCode.OperationFailed, $"{name} is required."))
            : new NamedValue(value, null);
    }

    private static bool HasConfirmation(JsonElement arguments, string confirmation)
        => McpArgumentReader.GetBoolean(arguments, "confirm") == true &&
            string.Equals(McpArgumentReader.GetString(arguments, "confirmation"), confirmation, StringComparison.OrdinalIgnoreCase);

    private static McpResponse Failure(IResult result, string summary)
        => McpResponse.Unavailable(McpErrorCode.OperationFailed, summary, string.Join("; ", FailureMessages(result)));

    private static IEnumerable<string> FailureMessages(IResult result)
    {
        if (result is null || result.IsSuccess)
        {
            return [];
        }

        return result.Messages
            .Concat(result.Errors.Select(error => error.Message))
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .DefaultIfEmpty("The job scheduler operation failed.");
    }

    private static object Page<T>(IResultPaged<T> result)
        => new
        {
            result.CurrentPage,
            result.PageSize,
            result.TotalCount,
            result.TotalPages,
            result.HasNextPage,
            result.HasPreviousPage
        };

    private static McpCapability Capability(string name, string toolset, string description)
        => new(name, toolset, "jobs", description) { Owner = "bdk", Category = toolset == McpToolset.Diagnostics ? "inspect" : "operate" };

    private sealed record NamedValue(string Value, McpResponse Response);
}
