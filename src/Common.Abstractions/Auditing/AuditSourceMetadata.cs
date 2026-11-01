// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Approved scalar source identity for an explicitly selected boundary, without payload serialization.</summary>
/// <example><code>var metadata = new AuditSourceMetadata();</code></example>
public sealed record AuditSourceMetadata
{
    /// <summary>Selected source feature name.</summary>
    /// <example><code>var value = metadata.Feature;</code></example>
    public string Feature { get; init; }

    /// <summary>Stable request or notification type name.</summary>
    /// <example><code>var value = metadata.RequestType;</code></example>
    public string RequestType { get; init; }

    /// <summary>Actual invoked handler type name.</summary>
    /// <example><code>var value = metadata.HandlerType;</code></example>
    public string HandlerType { get; init; }

    /// <summary>Opaque source request or notification identity.</summary>
    /// <example><code>var value = metadata.RequestId;</code></example>
    public string RequestId { get; init; }

    /// <summary>Observed source status, distinct from business outcome.</summary>
    /// <example><code>var value = metadata.ExecutionStatus;</code></example>
    public string ExecutionStatus { get; init; }

    /// <summary>Actual source attempt number, when available.</summary>
    /// <example><code>var value = metadata.Attempt;</code></example>
    public int? Attempt { get; init; }

    /// <summary>Opaque source flow identity, when available.</summary>
    /// <example><code>var value = metadata.FlowId;</code></example>
    public string FlowId { get; init; }

    /// <summary>Source job name, when available.</summary>
    /// <example><code>var value = metadata.JobName;</code></example>
    public string JobName { get; init; }

    /// <summary>Unique job occurrence identity, when available.</summary>
    /// <example><code>var value = metadata.JobOccurrenceId;</code></example>
    public string JobOccurrenceId { get; init; }

    /// <summary>Actual job execution identity, when available.</summary>
    /// <example><code>var value = metadata.JobExecutionId;</code></example>
    public string JobExecutionId { get; init; }
}
