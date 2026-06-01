// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>
/// Emits execution telemetry around the job behavior pipeline.
/// </summary>
public sealed class JobMetricsBehavior(TimeProvider timeProvider) : IJobBehavior
{
    /// <inheritdoc />
    public async Task<IResult<JobExecutionResult>> HandleAsync(
        JobBehaviorContext context,
        JobBehaviorDelegate next,
        CancellationToken cancellationToken = default)
    {
        var occurrence = CreateOccurrence(context, JobOccurrenceStatus.Running);

        using var activity = JobSchedulerInstrumentation.StartExecutionActivity(
            context.SchedulerInstanceId,
            occurrence,
            context.Trigger,
            context.ExecutionContext.ExecutionId,
            context.ExecutionContext.CorrelationId);

        JobSchedulerInstrumentation.RecordExecutionStarted(
            context.SchedulerInstanceId,
            occurrence,
            context.Trigger,
            context.ExecutionContext.ExecutionId,
            context.ActiveExecutionCount,
            context.MaxConcurrency,
            context.ExecutionContext.CorrelationId,
            timeProvider.GetUtcNow());

        try
        {
            var result = await next().ConfigureAwait(false);
            if (result.IsSuccess && result.Value is not null)
            {
                JobSchedulerInstrumentation.RecordExecutionCompleted(
                    context.SchedulerInstanceId,
                    occurrence with { Status = ToOccurrenceStatus(result.Value.Status) },
                    context.Trigger,
                    context.ExecutionContext.ExecutionId,
                    result.Value.Status,
                    (result.Value.CompletedUtc ?? timeProvider.GetUtcNow()) - result.Value.StartedUtc,
                    context.ExecutionContext.CorrelationId);
            }

            return result;
        }
        catch (Exception)
        {
            JobSchedulerInstrumentation.RecordExecutionCompleted(
                context.SchedulerInstanceId,
                occurrence with { Status = JobOccurrenceStatus.Failed },
                context.Trigger,
                context.ExecutionContext.ExecutionId,
                JobExecutionStatus.Failed,
                timeProvider.GetUtcNow() - context.ExecutionContext.StartedUtc,
                context.ExecutionContext.CorrelationId);
            throw;
        }
    }

    private static JobOccurrenceStatus ToOccurrenceStatus(JobExecutionStatus status)
    {
        return status switch
        {
            JobExecutionStatus.Completed => JobOccurrenceStatus.Completed,
            JobExecutionStatus.Retried => JobOccurrenceStatus.RetryScheduled,
            JobExecutionStatus.TimedOut => JobOccurrenceStatus.Failed,
            JobExecutionStatus.Cancelled => JobOccurrenceStatus.Cancelled,
            JobExecutionStatus.Interrupted => JobOccurrenceStatus.Cancelled,
            _ => JobOccurrenceStatus.Failed,
        };
    }

    private static JobOccurrence CreateOccurrence(JobBehaviorContext context, JobOccurrenceStatus status)
    {
        return new JobOccurrence
        {
            OccurrenceId = context.ExecutionContext.OccurrenceId,
            JobName = context.ExecutionContext.JobName,
            TriggerName = context.ExecutionContext.TriggerName,
            TriggerType = context.Trigger.TriggerType,
            Status = status,
            DueUtc = context.ExecutionContext.DueUtc,
            ScheduledUtc = context.ExecutionContext.ScheduledUtc,
            Data = context.ExecutionContext.Data,
            DataType = context.ExecutionContext.DataType,
            Properties = context.ExecutionContext.Properties,
            CorrelationId = context.ExecutionContext.CorrelationId,
            IdempotencyKey = context.ExecutionContext.IdempotencyKey,
            CreatedDate = context.ExecutionContext.StartedUtc,
            UpdatedDate = context.ExecutionContext.StartedUtc,
        };
    }
}
