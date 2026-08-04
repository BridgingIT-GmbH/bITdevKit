// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using System.Diagnostics;

internal static class JobSchedulerInstrumentation
{
    public const string ActivitySourceName = "BridgingIT.DevKit.Application.Jobs";
    private const string SchedulerInstanceIdTag = "jobs.scheduler.instance_id";
    private const string JobNameTag = "jobs.job.name";
    private const string TriggerNameTag = "jobs.trigger.name";
    private const string TriggerTypeTag = "jobs.trigger.type";
    private const string OccurrenceIdTag = "jobs.occurrence.id";
    private const string ExecutionIdTag = "jobs.execution.id";
    private const string CorrelationIdTag = "jobs.correlation.id";
    private const string LeaseOwnerTag = "jobs.lease.owner";
    private const string OperationTag = "jobs.operation";
    private const string EventSourceTag = "jobs.event.source";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);

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

}
