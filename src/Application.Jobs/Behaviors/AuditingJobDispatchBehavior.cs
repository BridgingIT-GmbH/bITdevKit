// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Records dispatch intent and actual acceptance separately from caller waiting and worker results.</summary>
/// <example><code>jobs.WithDispatchBehavior&lt;AuditingJobDispatchBehavior&gt;();</code></example>
public sealed class AuditingJobDispatchBehavior(IServiceScopeFactory scopes) : IJobDispatchBehavior
{
    /// <inheritdoc />
    public Task<T> DispatchAsync<T>(JobDispatchBehaviorContext context, Func<Task<T>> next, CancellationToken cancellationToken = default)
        => AuditSourceExecutor.RunAsync(scopes, builder => builder.For(context.JobType ?? typeof(JobDispatchBehaviorContext), "Dispatch").Key("jobs.dispatch").DisplayName("Dispatch " + (context.JobName?.Length <= 128 ? context.JobName : "job")).Kind("JobDispatch"),
            next, new() { Feature = "Jobs", JobName = context.JobName, RequestType = context.JobType?.FullName },
            starting: (_, scope) =>
            {
                context.TrustedAuditLink = (scope as IAuditSourceScope)?.CaptureLink();
                scope.SetDetail("job.waitForCompletion", AuditValue.From(context.WaitsForCompletion));
            },
            completed: (scope, result) =>
            {
                Accepted(scope, context);
                if (context.WaitsForCompletion && result is IResult<JobExecutionResult> waited && waited.Value is not null)
                    scope.SetDetail("job.callerWait", AuditValue.From(waited.Value.Status.ToString()));
            }, failed: (scope, error) =>
            {
                if (context.Accepted is null) return;
                Accepted(scope, context);
                scope.SetDetail("job.callerWait", AuditValue.From(error is OperationCanceledException ? "Canceled" : "Failed"));
            });

    private static void Accepted(IAuditScope scope, JobDispatchBehaviorContext context)
    {
        if (context.Accepted is not { } accepted) return;
        scope.Complete(AuditOutcome.Succeeded);
        (scope as IAuditSourceScope)?.SetSource(new() { ExecutionStatus = "Accepted", JobName = accepted.JobName, JobOccurrenceId = accepted.OccurrenceId.ToString("D"), HandlerType = accepted.HandlerType });
        scope.SetDetail("job.name", accepted.JobName?.Length <= 512 ? AuditValue.From(accepted.JobName) : AuditValue.Policy("Oversized"));
        scope.SetDetail("job.occurrenceId", AuditValue.From(accepted.OccurrenceId.ToString("D")));
    }
}
