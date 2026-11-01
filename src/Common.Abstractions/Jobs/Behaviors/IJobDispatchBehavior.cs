// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>Observes one explicit scheduler dispatch, independently from actual worker attempts and without a job payload.</summary>
/// <example><code>jobs.WithDispatchBehavior&lt;AuditingJobDispatchBehavior&gt;();</code></example>
public interface IJobDispatchBehavior
{
    /// <summary>Invokes the next dispatch once, preserving its original result or exception.</summary>
    /// <example><code>return await next();</code></example>
    Task<T> DispatchAsync<T>(JobDispatchBehaviorContext context, Func<Task<T>> next, CancellationToken cancellationToken = default);
}

/// <summary>Contains safe source dispatch identity and separately reported durable acceptance.</summary>
/// <example><code>var acceptedId = context.Accepted?.OccurrenceId;</code></example>
public sealed class JobDispatchBehaviorContext(string jobName, Type jobType, bool waitsForCompletion)
{
    /// <summary>The requested source name, without job data or properties.</summary>
    /// <example><code>var name = context.JobName;</code></example>
    public string JobName { get; } = jobName;

    /// <summary>The requested job type when supplied through a generic dispatch.</summary>
    /// <example><code>var type = context.JobType;</code></example>
    public Type JobType { get; } = jobType;

    /// <summary>Whether the caller also waits for the worker's independent outcome.</summary>
    /// <example><code>var waits = context.WaitsForCompletion;</code></example>
    public bool WaitsForCompletion { get; } = waitsForCompletion;

    /// <summary>Copied source-selected audit identity used only for protecting new occurrence handoffs.</summary>
    /// <remarks>This transient context is never persisted as job data.</remarks>
    /// <example><code>context.TrustedAuditLink = (scope as IAuditSourceScope)?.CaptureLink();</code></example>
    public AuditExecutionLink TrustedAuditLink { get; set; }

    /// <summary>Acceptance reported only after the scheduler has persisted or found the actual occurrence.</summary>
    /// <example><code>var occurrence = context.Accepted?.OccurrenceId;</code></example>
    public JobDispatchAcceptance Accepted { get; private set; }

    /// <summary>Copies actual source acceptance without treating later caller waiting as worker execution.</summary>
    /// <example><code>context.ReportAccepted(new("customer-export", occurrenceId, correlationId));</code></example>
    public void ReportAccepted(JobDispatchAcceptance acceptance) => this.Accepted = acceptance;
}

/// <summary>The source-confirmed occurrence identity; no job payload or mutable definition is retained.</summary>
/// <example><code>var accepted = new JobDispatchAcceptance("customer-export", occurrenceId, correlationId);</code></example>
public sealed record JobDispatchAcceptance(string JobName, Guid OccurrenceId, string CorrelationId, string HandlerType = null);
