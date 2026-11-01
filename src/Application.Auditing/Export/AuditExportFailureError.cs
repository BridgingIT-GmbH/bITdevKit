// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Auditing;

using BridgingIT.DevKit.Common;

/// <summary>Retains safe failed output metadata without granting usable progress.</summary>
/// <example><code>var failed = error.Completion;</code></example>
public sealed class AuditExportFailureError(AuditExportCompletion completion) : ResultErrorBase("AuditExportUnconfirmed")
{
    /// <summary>Observed terminal disposition with no confirmed tokens.</summary>
    /// <example><code>var reason = error.Completion.ReasonCode;</code></example>
    public AuditExportCompletion Completion { get; } = completion;
}
