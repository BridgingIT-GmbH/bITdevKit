// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Isolates actual independent worker execution from inherited capture and suppression.</summary>
/// <example><code>using var boundary = boundaryAccessor.DetachExecution();</code></example>
public interface IAuditExecutionBoundary
{
    /// <summary>Clears inherited audit/correlation state and restores it on synchronous disposal.</summary>
    /// <remarks>Trusted transport causation is applied separately; this never invents a user.</remarks>
    /// <example><code>using var boundary = boundaryAccessor.DetachExecution(); await RunJobAsync();</code></example>
    IDisposable DetachExecution();

    /// <summary>Starts an isolated worker flow with an explicitly trusted copied source link.</summary>
    /// <remarks>Transport adapters must establish trust before using this overload.</remarks>
    /// <example><code>using var boundary = auditBoundary.DetachExecution(trustedLink);</code></example>
    IDisposable DetachExecution(AuditExecutionLink trustedLink) => this.DetachExecution();
}
