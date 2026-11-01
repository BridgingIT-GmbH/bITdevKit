// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Trusted host handoff metadata captured from a live source execution, never from arbitrary client headers.</summary>
/// <example><code>var metadata = new AuditExecutionLink();</code></example>
public sealed record AuditExecutionLink
{
    /// <summary>Execution that began the causal trail.</summary>
    /// <example><code>var value = metadata.OriginAuditId;</code></example>
    public Guid OriginAuditId { get; init; }

    /// <summary>Immediate initiating execution.</summary>
    /// <example><code>var value = metadata.CausationAuditId;</code></example>
    public Guid CausationAuditId { get; init; }

    /// <summary>Immediate initiating action occurrence.</summary>
    /// <example><code>var value = metadata.CausationActionId;</code></example>
    public Guid? CausationActionId { get; init; }

    /// <summary>Captured application correlation identity.</summary>
    /// <example><code>var value = metadata.CorrelationId;</code></example>
    public string CorrelationId { get; init; }

    /// <summary>Original initiating module, when known.</summary>
    /// <example><code>var value = metadata.InitiatingModuleName;</code></example>
    public string InitiatingModuleName { get; init; }

    /// <summary>Copied permitted original initiating identity.</summary>
    /// <example><code>var value = metadata.InitiatingActor;</code></example>
    public AuditInitiator InitiatingActor { get; init; }
}
