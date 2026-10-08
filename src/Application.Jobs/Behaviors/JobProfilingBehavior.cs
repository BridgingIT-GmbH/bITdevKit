// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>Measures the actual job behavior boundary, joining or owning optional operation capture.</summary>
/// <example><code>services.AddJobs().WithBehavior&lt;JobProfilingBehavior&gt;();</code></example>
public sealed class JobProfilingBehavior(IOperationProfiler profiling = null) : IJobBehavior
{
    /// <inheritdoc />
    public Task<IResult<JobExecutionResult>> HandleAsync(JobBehaviorContext context, JobBehaviorDelegate next, CancellationToken cancellationToken = default)
    {
        if (profiling is null) { return next(); }

        return profiling.JoinOrStartAsync(new OperationProfilingStartRequest
        {
            Key = "job:" + context.JobName, Kind = OperationProfilingKind.Job.ToString(), CorrelationId = context.ExecutionContext.CorrelationId,
        }, async (scope, _) =>
        {
            Enrich(scope, context);
            var result = await next().ConfigureAwait(false);
            try { if (scope.IsRecording) { scope.SetDimension("job.status", result?.Value?.Status.ToString()); } }
            catch (Exception) { /* Metadata faults do not replace the job result. */ }

            return result;
        }, cancellationToken, Classify);
    }

    private static void Enrich(IProfilingRecordingScope scope, JobBehaviorContext context)
    {
        try
        {
            if (!scope.IsRecording) { return; }

            scope.SetDimension("job.triggerKind", context.Trigger.TriggerType.ToString());
            scope.SetDimension("job.attempt", context.ExecutionContext.AttemptNumber);
            scope.SetDimension("job.executionId", context.ExecutionContext.ExecutionId.ToString("D"));
            scope.SetDimension("job.boundary", "BehaviorDelegate");
        }
        catch (Exception) { /* Only bounded extracted scalars participate in diagnostics. */ }
    }

    private static ProfilingResultClassification Classify(IResult<JobExecutionResult> result)
    {
        if (result is null) { return null; }

        if (result.IsFailure) { return new() { Outcome = OperationProfilingOutcome.Failed, Failure = new() { Source = "Job", Code = "Failed" } }; }

        var status = result.Value?.Status;
        if (status is null or JobExecutionStatus.Started) { return null; }

        var canceled = status is JobExecutionStatus.Cancelled or JobExecutionStatus.Interrupted;
        var timeout = result.Value?.TimedOut == true || status == JobExecutionStatus.TimedOut;
        var failed = timeout || status == JobExecutionStatus.Failed;
        return new()
        {
            Outcome = canceled ? OperationProfilingOutcome.Canceled : failed ? OperationProfilingOutcome.Failed : OperationProfilingOutcome.Completed,
            Failure = failed ? new ProfilingFailureDescriptor { Source = "Job", Code = timeout ? "Timeout" : "Failed" } : null,
        };
    }
}
