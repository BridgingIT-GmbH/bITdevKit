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
    private const string JobNameTag = "jobs.job.name";
    private const string TriggerNameTag = "jobs.trigger.name";
    private const string TriggerTypeTag = "jobs.trigger.type";
    private const string OperationTag = "jobs.operation";
    private const string SuccessTag = "jobs.operation.success";
    private const string StatusTag = "jobs.status";
    private const string EventSourceTag = "jobs.event.source";

    public void RecordSweepCycle(
        int recoveredCount,
        int materializedCount,
        int dueCount,
        int activeExecutionCount,
        int maxConcurrency)
    {
        metricsService?.AddCounter("jobs_sweep_cycles");
        if (recoveredCount > 0)
        {
            metricsService?.AddCounter("jobs_leases_recovered", recoveredCount);
        }

        metricsService?.RecordHistogram(
            "jobs_worker_utilization",
            maxConcurrency <= 0 ? 0D : (double)activeExecutionCount / maxConcurrency);
    }

    public void RecordMaterializedOccurrences(
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
            CreateTags(jobName, triggerName, triggerType));
    }

    public void RecordEventAccepted(string source, bool duplicate)
    {
        var tags = CreateTags(
            additional:
            [
                new(EventSourceTag, source),
                new(StatusTag, duplicate ? "duplicate" : "accepted"),
            ]);
        metricsService?.AddCounter("jobs_events_accepted", tags: tags);
    }

    public void RecordLeaseAcquired()
    {
        metricsService?.AddCounter("jobs_leases_acquired");
    }

    public void RecordLeaseRenewed()
    {
        metricsService?.AddCounter("jobs_leases_renewed");
    }

    public void RecordExecutionStarted(
        JobOccurrence occurrence,
        JobTriggerDefinition trigger,
        int activeExecutionCount,
        int maxConcurrency,
        DateTimeOffset nowUtc)
    {
        var tags = CreateTags(
            occurrence.JobName,
            occurrence.TriggerName,
            trigger?.TriggerType);
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
        JobOccurrence occurrence,
        JobTriggerDefinition trigger,
        JobExecutionStatus status,
        TimeSpan duration)
    {
        var commonTags = CreateTags(
            occurrence.JobName,
            occurrence.TriggerName,
            trigger?.TriggerType);
        metricsService?.AddUpDownCounter("jobs_executions_active", -1, commonTags);

        var tags = CreateTags(
            occurrence.JobName,
            occurrence.TriggerName,
            trigger?.TriggerType,
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
        string triggerName = null)
    {
        var tags = CreateTags(
            jobName,
            triggerName,
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
        string jobName = null,
        string triggerName = null,
        JobTriggerType? triggerType = null,
        ReadOnlySpan<MetricTag> additional = default)
    {
        var tags = new List<MetricTag>(3 + additional.Length);

        Add(tags, JobNameTag, jobName);
        Add(tags, TriggerNameTag, triggerName);
        if (triggerType.HasValue)
        {
            tags.Add(new(TriggerTypeTag, triggerType.Value.ToString()));
        }

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
