// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Optional safe failure reporting for source adapters without a dependency on an audit implementation.</summary>
/// <example><code>(audit as IAuditCaptureDiagnostics)?.ReportFailure("AuditSourceSelectionFailed");</code></example>
public interface IAuditCaptureDiagnostics
{
    /// <summary>Reports a bounded reason code, never an exception, payload, or secret value.</summary>
    /// <example><code>diagnostics.ReportFailure("AuditSourceMetadataFailed");</code></example>
    void ReportFailure(string reasonCode);
}
