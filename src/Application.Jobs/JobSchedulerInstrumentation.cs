// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using System.Diagnostics;
using System.Diagnostics.Metrics;
using BridgingIT.DevKit.Common;

internal static class JobSchedulerInstrumentation
{
    public const string ActivitySourceName = "BridgingIT.DevKit.Application.Jobs";
    public const string MeterName = "BridgingIT.DevKit.Application.Jobs";

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

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> SweepCycles = Meter.CreateCounter<long>("jobs.sweep.cycles");
    private static readonly Counter<long> MaterializedOccurrences = Meter.CreateCounter<long>("jobs.occurrences.materialized");
    private static readonly Counter<long> EventAccepted = Meter.CreateCounter<long>("jobs.events.accepted");
    private static readonly Counter<long> ManagementOperations = Meter.CreateCounter<long>("jobs.management.operations");
    private static readonly Counter<long> LeasesAcquired = Meter.CreateCounter<long>("jobs.leases.acquired");
    private static readonly Counter<long> LeasesRenewed = Meter.CreateCounter<long>("jobs.leases.renewed");
    private static readonly Counter<long> LeasesRecovered = Meter.CreateCounter<long>("jobs.leases.recovered");
    private static readonly Counter<long> ExecutionsStarted = Meter.CreateCounter<long>("jobs.executions.started");
    private static readonly Counter<long> ExecutionsCompleted = Meter.CreateCounter<long>("jobs.executions.completed");
    private static readonly Counter<long> ExecutionsFailed = Meter.CreateCounter<long>("jobs.executions.failed");
    private static readonly Counter<long> ExecutionsRetried = Meter.CreateCounter<long>("jobs.executions.retried");
    private static readonly Counter<long> ExecutionsTimedOut = Meter.CreateCounter<long>("jobs.executions.timedout");
    private static readonly Counter<long> ExecutionsCancelled = Meter.CreateCounter<long>("jobs.executions.cancelled");
    private static readonly Counter<long> ExecutionsInterrupted = Meter.CreateCounter<long>("jobs.executions.interrupted");
    private static readonly UpDownCounter<long> ActiveExecutions = Meter.CreateUpDownCounter<long>("jobs.executions.active");
    private static readonly Histogram<double> ExecutionDurationMs = Meter.CreateHistogram<double>("jobs.execution.duration.ms", unit: "ms");
    private static readonly Histogram<double> OccurrenceAgeMs = Meter.CreateHistogram<double>("jobs.occurrence.age.ms", unit: "ms");
    private static readonly Histogram<double> WorkerUtilization = Meter.CreateHistogram<double>("jobs.worker.utilization");

    public static Activity StartSweepActivity(string schedulerInstanceId)
    {
        var activity = ActivitySource.StartActivity("jobs.sweep", ActivityKind.Internal);
        activity?.SetTag(SchedulerInstanceIdTag, schedulerInstanceId);
        return activity;
    }

    public static Activity StartMaterializationActivity(string schedulerInstanceId)
    {
        var activity = ActivitySource.StartActivity("jobs.trigger.materialize", ActivityKind.Internal);
        activity?.SetTag(SchedulerInstanceIdTag, schedulerInstanceId);
        return activity;
    }

    public static Activity StartExecutionActivity(string schedulerInstanceId, JobOccurrence occurrence, JobTriggerDefinition trigger, Guid executionId, string correlationId)
    {
        var activity = ActivitySource.StartActivity("jobs.execution", ActivityKind.Internal);
        SetCommonOccurrenceTags(activity, schedulerInstanceId, occurrence, trigger, executionId, correlationId);
        return activity;
    }

    public static Activity StartLeaseActivity(string activityName, string schedulerInstanceId, Guid occurrenceId, string leaseOwner = null)
    {
        var activity = ActivitySource.StartActivity(activityName, ActivityKind.Internal);
        activity?.SetTag(SchedulerInstanceIdTag, schedulerInstanceId);
        activity?.SetTag(OccurrenceIdTag, occurrenceId.ToString("D"));
        if (!string.IsNullOrWhiteSpace(leaseOwner))
        {
            activity?.SetTag(LeaseOwnerTag, leaseOwner);
        }

        return activity;
    }

    public static Activity StartRetrySchedulingActivity(string schedulerInstanceId, JobOccurrence occurrence, JobTriggerDefinition trigger, Guid executionId, string correlationId)
    {
        var activity = ActivitySource.StartActivity("jobs.retry.schedule", ActivityKind.Internal);
        SetCommonOccurrenceTags(activity, schedulerInstanceId, occurrence, trigger, executionId, correlationId);
        return activity;
    }

    public static Activity StartEventAcceptanceActivity(string source, string correlationId)
    {
        var activity = ActivitySource.StartActivity("jobs.event.accept", ActivityKind.Internal);
        activity?.SetTag(EventSourceTag, source);
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            activity?.SetTag(CorrelationIdTag, correlationId);
        }

        return activity;
    }

    public static Activity StartManagementActivity(string operation, string jobName = null, string triggerName = null, Guid? occurrenceId = null)
    {
        var activity = ActivitySource.StartActivity("jobs.management", ActivityKind.Internal);
        activity?.SetTag(OperationTag, operation);
        if (!string.IsNullOrWhiteSpace(jobName))
        {
            activity?.SetTag(JobNameTag, jobName);
        }

        if (!string.IsNullOrWhiteSpace(triggerName))
        {
            activity?.SetTag(TriggerNameTag, triggerName);
        }

        if (occurrenceId.HasValue)
        {
            activity?.SetTag(OccurrenceIdTag, occurrenceId.Value.ToString("D"));
        }

        return activity;
    }

    public static void RecordSweepCycle(string schedulerInstanceId, int recoveredCount, int materializedCount, int dueCount, int activeExecutionCount, int maxConcurrency)
    {
        var tags = CreateTags(schedulerInstanceId: schedulerInstanceId);
        SweepCycles.Add(1, tags);
        if (recoveredCount > 0)
        {
            LeasesRecovered.Add(recoveredCount, tags);
        }

        if (materializedCount > 0)
        {
            MaterializedOccurrences.Add(materializedCount, tags);
        }

        WorkerUtilization.Record(maxConcurrency <= 0 ? 0d : (double)activeExecutionCount / maxConcurrency, tags);
    }

    public static void RecordMaterializedOccurrences(string schedulerInstanceId, string jobName, string triggerName, JobTriggerType triggerType, int count)
    {
        if (count <= 0)
        {
            return;
        }

        MaterializedOccurrences.Add(count, CreateTags(schedulerInstanceId, jobName, triggerName, triggerType));
    }

    public static void RecordEventAccepted(string source, string correlationId, bool duplicate)
    {
        var tags = CreateTags(correlationId: correlationId);
        tags.Add(EventSourceTag, source);
        tags.Add(StatusTag, duplicate ? "duplicate" : "accepted");
        EventAccepted.Add(1, tags);
    }

    public static void RecordLeaseAcquired(string schedulerInstanceId, Guid occurrenceId, string leaseOwner)
    {
        LeasesAcquired.Add(1, CreateTags(schedulerInstanceId: schedulerInstanceId, occurrenceId: occurrenceId, leaseOwner: leaseOwner));
    }

    public static void RecordLeaseRenewed(string schedulerInstanceId, Guid occurrenceId, string leaseOwner)
    {
        LeasesRenewed.Add(1, CreateTags(schedulerInstanceId: schedulerInstanceId, occurrenceId: occurrenceId, leaseOwner: leaseOwner));
    }

    public static void RecordExecutionStarted(string schedulerInstanceId, JobOccurrence occurrence, JobTriggerDefinition trigger, Guid executionId, int activeExecutionCount, int maxConcurrency, string correlationId, DateTimeOffset nowUtc)
    {
        var tags = CreateTags(schedulerInstanceId, occurrence.JobName, occurrence.TriggerName, trigger?.TriggerType, occurrence.OccurrenceId, executionId, correlationId);
        ExecutionsStarted.Add(1, tags);
        ActiveExecutions.Add(1, tags);
        OccurrenceAgeMs.Record(Math.Max(0d, (nowUtc - occurrence.DueUtc).TotalMilliseconds), tags);
        WorkerUtilization.Record(maxConcurrency <= 0 ? 0d : (double)activeExecutionCount / maxConcurrency, tags);
    }

    public static void RecordExecutionCompleted(string schedulerInstanceId, JobOccurrence occurrence, JobTriggerDefinition trigger, Guid executionId, JobExecutionStatus status, TimeSpan duration, string correlationId)
    {
        var tags = CreateTags(schedulerInstanceId, occurrence.JobName, occurrence.TriggerName, trigger?.TriggerType, occurrence.OccurrenceId, executionId, correlationId);
        tags.Add(StatusTag, status.ToString());

        switch (status)
        {
            case JobExecutionStatus.Completed:
                ExecutionsCompleted.Add(1, tags);
                break;
            case JobExecutionStatus.Retried:
                ExecutionsRetried.Add(1, tags);
                break;
            case JobExecutionStatus.TimedOut:
                ExecutionsTimedOut.Add(1, tags);
                break;
            case JobExecutionStatus.Cancelled:
                ExecutionsCancelled.Add(1, tags);
                break;
            case JobExecutionStatus.Interrupted:
                ExecutionsInterrupted.Add(1, tags);
                break;
            default:
                ExecutionsFailed.Add(1, tags);
                break;
        }

        ExecutionDurationMs.Record(Math.Max(0d, duration.TotalMilliseconds), tags);
        ActiveExecutions.Add(-1, tags);
    }

    public static void RecordManagementOperation(string operation, bool success, string jobName = null, string triggerName = null, Guid? occurrenceId = null)
    {
        var tags = CreateTags(jobName: jobName, triggerName: triggerName, occurrenceId: occurrenceId);
        tags.Add(OperationTag, operation);
        tags.Add(SuccessTag, success);
        ManagementOperations.Add(1, tags);
    }

    private static void SetCommonOccurrenceTags(Activity activity, string schedulerInstanceId, JobOccurrence occurrence, JobTriggerDefinition trigger, Guid executionId, string correlationId)
    {
        activity?.SetTag(SchedulerInstanceIdTag, schedulerInstanceId);
        activity?.SetTag(JobNameTag, occurrence.JobName);
        activity?.SetTag(TriggerNameTag, occurrence.TriggerName);
        activity?.SetTag(TriggerTypeTag, trigger?.TriggerType.ToString());
        activity?.SetTag(OccurrenceIdTag, occurrence.OccurrenceId.ToString("D"));
        activity?.SetTag(ExecutionIdTag, executionId.ToString("D"));
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            activity?.SetTag(CorrelationIdTag, correlationId);
        }
    }

    private static TagList CreateTags(
        string schedulerInstanceId = null,
        string jobName = null,
        string triggerName = null,
        JobTriggerType? triggerType = null,
        Guid? occurrenceId = null,
        Guid? executionId = null,
        string correlationId = null,
        string leaseOwner = null)
    {
        TagList tags = [];

        if (!string.IsNullOrWhiteSpace(schedulerInstanceId))
        {
            tags.Add(SchedulerInstanceIdTag, schedulerInstanceId);
        }

        if (!string.IsNullOrWhiteSpace(jobName))
        {
            tags.Add(JobNameTag, jobName);
        }

        if (!string.IsNullOrWhiteSpace(triggerName))
        {
            tags.Add(TriggerNameTag, triggerName);
        }

        if (triggerType.HasValue)
        {
            tags.Add(TriggerTypeTag, triggerType.Value.ToString());
        }

        if (occurrenceId.HasValue)
        {
            tags.Add(OccurrenceIdTag, occurrenceId.Value.ToString("D"));
        }

        if (executionId.HasValue)
        {
            tags.Add(ExecutionIdTag, executionId.Value.ToString("D"));
        }

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            tags.Add(CorrelationIdTag, correlationId);
        }

        if (!string.IsNullOrWhiteSpace(leaseOwner))
        {
            tags.Add(LeaseOwnerTag, leaseOwner);
        }

        return tags;
    }
}