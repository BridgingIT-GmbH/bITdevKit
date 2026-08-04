// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>
/// Emits provider-neutral job scheduler metrics through the optional shared metrics service.
/// </summary>
/// <example>
/// <code>
/// services.AddMetrics(options => options.Enabled());
/// services.AddJobScheduler();
/// </code>
/// </example>
internal sealed class JobSchedulerMetrics(IMetricsService metricsService = null)
{
    private const string SchedulerInstanceIdTag = "jobs.scheduler.instance_id";
    private const string JobNameTag = "jobs.job.name";
    private const string TriggerNameTag = "jobs.trigger.name";
    private const string TriggerTypeTag = "jobs.trigger.type";
    private const string OccurrenceIdTag = "jobs.occurrence.id";
    private const string ExecutionIdTag = "jobs.execution.id";
    private const string CorrelationIdTag = "jobs.correlation.id";
    private const string LeaseOwnerTag = "jobs.lease.owner";
    private const string OperationTag = "jobs.operation";
    private const string SuccessTag = "jobs.operation.success";
    private const string StatusTag = "jobs.status";
    private const string EventSourceTag = "jobs.event.source";

    public void RecordSweepCycle(
        string schedulerInstanceId,
        int recoveredCount,
        int materializedCount,
        int dueCount,
        int activeExecutionCount,
        int maxConcurrency)
    {
        var tags = CreateTags(schedulerInstanceId: schedulerInstanceId);
        metricsService?.AddCounter("jobs_sweep_cycles", tags: tags);
        if (recoveredCount > 0)
        {
            metricsService?.AddCounter("jobs_leases_recovered", recoveredCount, tags);
        }

        metricsService?.RecordHistogram(
            "jobs_worker_utilization",
            maxConcurrency <= 0 ? 0D : (double)activeExecutionCount / maxConcurrency,
            tags: tags);
    }

    public void RecordMaterializedOccurrences(
        string schedulerInstanceId,
        string jobName,
        string triggerName,
        JobTriggerType triggerType,
        int count)
    {
        if (count <= 0)
        {
            return;
        }

        metricsService?.AddCounter(
            "jobs_occurrences_materialized",
            count,
            CreateTags(schedulerInstanceId, jobName, triggerName, triggerType));
    }

    public void RecordEventAccepted(string source, string correlationId, bool duplicate)
    {
        var tags = CreateTags(
            correlationId: correlationId,
            additional:
            [
                new(EventSourceTag, source),
                new(StatusTag, duplicate ? "duplicate" : "accepted"),
            ]);
        metricsService?.AddCounter("jobs_events_accepted", tags: tags);
    }

    public void RecordLeaseAcquired(
        string schedulerInstanceId,
        Guid occurrenceId,
        string leaseOwner)
    {
        metricsService?.AddCounter(
            "jobs_leases_acquired",
            tags: CreateTags(
                schedulerInstanceId: schedulerInstanceId,
                occurrenceId: occurrenceId,
                leaseOwner: leaseOwner));
    }

    public void RecordLeaseRenewed(
        string schedulerInstanceId,
        Guid occurrenceId,
        string leaseOwner)
    {
        metricsService?.AddCounter(
            "jobs_leases_renewed",
            tags: CreateTags(
                schedulerInstanceId: schedulerInstanceId,
                occurrenceId: occurrenceId,
                leaseOwner: leaseOwner));
    }

    public void RecordExecutionStarted(
        string schedulerInstanceId,
        JobOccurrence occurrence,
        JobTriggerDefinition trigger,
        Guid executionId,
        int activeExecutionCount,
        int maxConcurrency,
        string correlationId,
        DateTimeOffset nowUtc)
    {
        var tags = CreateTags(
            schedulerInstanceId,
            occurrence.JobName,
            occurrence.TriggerName,
            trigger?.TriggerType,
            occurrence.OccurrenceId,
            executionId,
            correlationId);
        metricsService?.AddCounter("jobs_executions_started", tags: tags);
        metricsService?.AddUpDownCounter("jobs_executions_active", 1, tags);
        metricsService?.RecordHistogram(
            "jobs_occurrence_age",
            Math.Max(0D, (nowUtc - occurrence.DueUtc).TotalMilliseconds),
            "ms",
            tags);
        metricsService?.RecordHistogram(
            "jobs_worker_utilization",
            maxConcurrency <= 0 ? 0D : (double)activeExecutionCount / maxConcurrency,
            tags: tags);
    }

    public void RecordExecutionCompleted(
        string schedulerInstanceId,
        JobOccurrence occurrence,
        JobTriggerDefinition trigger,
        Guid executionId,
        JobExecutionStatus status,
        TimeSpan duration,
        string correlationId)
    {
        var commonTags = CreateTags(
            schedulerInstanceId,
            occurrence.JobName,
            occurrence.TriggerName,
            trigger?.TriggerType,
            occurrence.OccurrenceId,
            executionId,
            correlationId);
        metricsService?.AddUpDownCounter("jobs_executions_active", -1, commonTags);

        var tags = CreateTags(
            schedulerInstanceId,
            occurrence.JobName,
            occurrence.TriggerName,
            trigger?.TriggerType,
            occurrence.OccurrenceId,
            executionId,
            correlationId,
            additional: [new(StatusTag, status.ToString())]);
        metricsService?.AddCounter(StatusCounterName(status), tags: tags);
        metricsService?.RecordHistogram(
            "jobs_execution_duration",
            Math.Max(0D, duration.TotalMilliseconds),
            "ms",
            tags);
    }

    public void RecordManagementOperation(
        string operation,
        bool success,
        string jobName = null,
        string triggerName = null,
        Guid? occurrenceId = null)
    {
        var tags = CreateTags(
            jobName: jobName,
            triggerName: triggerName,
            occurrenceId: occurrenceId,
            additional:
            [
                new(OperationTag, operation),
                new(SuccessTag, success),
            ]);
        metricsService?.AddCounter("jobs_management_operations", tags: tags);
    }

    private static string StatusCounterName(JobExecutionStatus status) => status switch
    {
        JobExecutionStatus.Completed => "jobs_executions_completed",
        JobExecutionStatus.Retried => "jobs_executions_retried",
        JobExecutionStatus.TimedOut => "jobs_executions_timedout",
        JobExecutionStatus.Cancelled => "jobs_executions_cancelled",
        JobExecutionStatus.Interrupted => "jobs_executions_interrupted",
        _ => "jobs_executions_failed",
    };

    private static MetricTag[] CreateTags(
        string schedulerInstanceId = null,
        string jobName = null,
        string triggerName = null,
        JobTriggerType? triggerType = null,
        Guid? occurrenceId = null,
        Guid? executionId = null,
        string correlationId = null,
        string leaseOwner = null,
        ReadOnlySpan<MetricTag> additional = default)
    {
        var tags = new List<MetricTag>(8 + additional.Length);

        Add(tags, SchedulerInstanceIdTag, schedulerInstanceId);
        Add(tags, JobNameTag, jobName);
        Add(tags, TriggerNameTag, triggerName);
        if (triggerType.HasValue)
        {
            tags.Add(new(TriggerTypeTag, triggerType.Value.ToString()));
        }

        if (occurrenceId.HasValue)
        {
            tags.Add(new(OccurrenceIdTag, occurrenceId.Value.ToString("D")));
        }

        if (executionId.HasValue)
        {
            tags.Add(new(ExecutionIdTag, executionId.Value.ToString("D")));
        }

        Add(tags, CorrelationIdTag, correlationId);
        Add(tags, LeaseOwnerTag, leaseOwner);
        tags.AddRange(additional);

        return [.. tags];
    }

    private static void Add(List<MetricTag> tags, string name, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            tags.Add(new(name, value));
        }
    }
}
