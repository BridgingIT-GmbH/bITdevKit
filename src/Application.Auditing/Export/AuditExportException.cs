// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Auditing;

using BridgingIT.DevKit.Common;

/// <summary>A safe bounded export failure code, without source values or underlying exception text.</summary>
/// <example><code>throw new AuditExportException("AuditExportRecordTooLarge");</code></example>
public sealed class AuditExportException(string code) : Exception(code)
{
    /// <summary>The stable reason identifier suitable for terminal metadata.</summary>
    /// <example><code>var reason = error.Code;</code></example>
    public string Code { get; } = AuditStructuralValidation.Required(code, 128, nameof(code));
}
