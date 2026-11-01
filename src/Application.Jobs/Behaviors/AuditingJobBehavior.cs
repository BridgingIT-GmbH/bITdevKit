// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Observes an actual job execution attempt without confusing scheduler status with outer Result success.</summary>
/// <example><code>jobs.WithBehavior&lt;AuditingJobBehavior&gt;();</code></example>
public sealed class AuditingJobBehavior(IServiceScopeFactory scopes) : IJobBehavior
{
    /// <inheritdoc />
    public Task<IResult<JobExecutionResult>> HandleAsync(JobBehaviorContext context, JobBehaviorDelegate next, CancellationToken cancellationToken = default)
        => AuditSourceExecutor.RunAsync(scopes, builder => builder.For(context.JobType, "Execute").Kind("JobHandler"), () => next(),
            new() { Feature = "Jobs", HandlerType = context.JobType.FullName, JobName = context.JobName,
                JobOccurrenceId = context.ExecutionContext.OccurrenceId.ToString("D"), JobExecutionId = context.ExecutionContext.ExecutionId.ToString("D"),
                Attempt = context.ExecutionContext.AttemptNumber, ExecutionStatus = "Invoked" }, completed: Classify);

    /// <summary>Classifies the actual scheduler terminal status, retaining retry status separately from this failed attempt.</summary>
    /// <example><code>AuditingJobBehavior.Classify(scope, result);</code></example>
    public static void Classify(IAuditScope scope, IResult<JobExecutionResult> result)
    {
        var status = result?.Value?.Status;
        scope.Complete(result?.IsFailure == true || status is JobExecutionStatus.Failed or JobExecutionStatus.TimedOut or JobExecutionStatus.Retried ? AuditOutcome.Failed
            : status is JobExecutionStatus.Cancelled or JobExecutionStatus.Interrupted ? AuditOutcome.Canceled
            : status == JobExecutionStatus.Completed ? AuditOutcome.Succeeded : AuditOutcome.Unknown);
        (scope as IAuditSourceScope)?.SetSource(new() { ExecutionStatus = status?.ToString() ?? "Unavailable" });
    }
}
