// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

public partial class JobSchedulerService
{
    private readonly AsyncLocal<JobDispatchBehaviorContext> currentAuditDispatch = new();
    private readonly AsyncLocal<string> currentAuditedJob = new();
    private async Task<T> DispatchWithBehaviorsAsync<T>(string name, Type type, bool wait, Func<Task<T>> next, CancellationToken cancellationToken)
    {
        var types = registrations.GetDispatchBehaviorTypes();
        if (types.Count == 0) return await next().ConfigureAwait(false);
        await using var services = scopeFactory.CreateAsyncScope();
        var context = new JobDispatchBehaviorContext(name, type, wait);
        var previous = this.currentAuditDispatch.Value;
        this.currentAuditDispatch.Value = context;
        try
        {
            var pipeline = next;
            foreach (var behaviorType in types.OrderBy(type => type == typeof(AuditingJobDispatchBehavior) ? 0 : 1).Reverse())
            {
                var behavior = (IJobDispatchBehavior)ActivatorUtilities.CreateInstance(services.ServiceProvider, behaviorType);
                var continuation = pipeline;
                pipeline = () => behavior.DispatchAsync(context, continuation, cancellationToken);
            }

            return await pipeline().ConfigureAwait(false);
        }
        finally { this.currentAuditDispatch.Value = previous; }
    }
    private void StampDispatchHandoff(JobOccurrence occurrence)
    {
        occurrence.Properties.Remove(AuditHandoffMetadata.PropertyName);
        var context = this.currentAuditDispatch.Value;
        if (context?.TrustedAuditLink is not { } link) return;
        IAuditCaptureDiagnostics diagnostics = null;
        try
        {
            using var services = scopeFactory.CreateScope();
            diagnostics = services.ServiceProvider.GetService<IAuditContext>() as IAuditCaptureDiagnostics;
            var protector = services.ServiceProvider.GetService<IAuditExecutionLinkProtector>();
            if (protector is not null) occurrence.Properties.Set(AuditHandoffMetadata.PropertyName, protector.Protect(link, "Jobs", occurrence.OccurrenceId.ToString("D")));
            else diagnostics?.ReportFailure("AuditHandoffProtectionUnavailable");
        }
        catch (Exception)
        {
            try { diagnostics?.ReportFailure("AuditHandoffProtectionUnavailable"); }
            catch (Exception) { /* Handoff diagnostics cannot reject source acceptance. */ }
        }
    }
    private bool WorkerAuditingSelected(JobDefinition definition) => registrations.GetGlobalBehaviorTypes().Concat(definition.BehaviorTypes).Contains(typeof(AuditingJobBehavior));
    private async Task<Result<JobExecutionResult>> ExecuteOccurrenceAsync(JobDefinition definition, JobTriggerDefinition trigger, JobOccurrence occurrence, JobLeaseRecord lease, CancellationToken cancellationToken)
    {
        if (!this.WorkerAuditingSelected(definition)) return await AuditSourceExecutor.RunIsolatedAsync(scopeFactory, "Jobs", occurrence.OccurrenceId.ToString("D"),
            AuditHandoffMetadata.ReadToken(occurrence.Properties.Get(AuditHandoffMetadata.PropertyName)), occurrence.CorrelationId,
            () => this.ExecuteOccurrenceCoreAsync(definition, trigger, occurrence, lease, cancellationToken)).ConfigureAwait(false);
        var previous = this.currentAuditedJob.Value;
        this.currentAuditedJob.Value = occurrence.JobName;
        try
        {
            var token = AuditHandoffMetadata.ReadToken(occurrence.Properties.Get(AuditHandoffMetadata.PropertyName));
            return await AuditSourceExecutor.RunWorkerAsync(scopeFactory, "Jobs", occurrence.OccurrenceId.ToString("D"), token,
                builder => builder.For(definition.JobType, "Run").Kind("Job"),
                () => this.ExecuteOccurrenceCoreAsync(definition, trigger, occurrence, lease, cancellationToken),
                new() { Feature = "Jobs", JobName = occurrence.JobName, JobOccurrenceId = occurrence.OccurrenceId.ToString("D"), HandlerType = definition.JobType.FullName },
                completed: (scope, result) => AuditingJobBehavior.Classify(scope, result), correlationId: occurrence.CorrelationId).ConfigureAwait(false);
        }
        finally { this.currentAuditedJob.Value = previous; }
    }
    private void ObserveWorkerExecution(JobDefinition definition, JobOccurrence occurrence, Guid executionId, int attempt)
    {
        if (!this.WorkerAuditingSelected(definition)) return;
        try
        {
            using var scope = scopeFactory.CreateScope();
            var audit = scope.ServiceProvider.GetService<IAuditContext>();
            (audit?.Current as IAuditSourceScope)?.SetSource(new() { JobName = occurrence.JobName, JobOccurrenceId = occurrence.OccurrenceId.ToString("D"),
                JobExecutionId = executionId.ToString("D"), Attempt = attempt });
        }
        catch (Exception) { /* Source metadata cannot alter scheduler persistence or execution. */ }
    }
    private JobOccurrence StampDependentHandoff(JobOccurrence occurrence, string prerequisiteJobName)
    {
        if (!string.Equals(this.currentAuditedJob.Value, prerequisiteJobName, StringComparison.OrdinalIgnoreCase)) return occurrence;
        IAuditCaptureDiagnostics diagnostics = null;
        try
        {
            using var scope = scopeFactory.CreateScope();
            var audit = scope.ServiceProvider.GetService<IAuditContext>();
            diagnostics = audit as IAuditCaptureDiagnostics;
            var link = (audit?.Current as IAuditSourceScope)?.CaptureLink();
            var protector = scope.ServiceProvider.GetService<IAuditExecutionLinkProtector>();
            if (link is null || protector is null) return occurrence;
            var properties = occurrence.Properties.Clone();
            properties.Set(AuditHandoffMetadata.PropertyName, protector.Protect(link, "Jobs", occurrence.OccurrenceId.ToString("D")));
            // Carry metadata into the source's normal state transition; auditing adds no extra scheduler-store write.
            return occurrence with { Properties = properties };
        }
        catch (Exception)
        {
            try { diagnostics?.ReportFailure("AuditHandoffProtectionUnavailable"); }
            catch (Exception) { /* Diagnostic failure cannot alter dependency release. */ }

            return occurrence;
        }
    }
}
