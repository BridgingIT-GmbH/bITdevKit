// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using BridgingIT.DevKit.Common;

/// <summary>Resolves external host keys without persisting secret material with audit evidence.</summary>
/// <remarks>Return an operation-owned snapshot. The caller disposes and clears it after use; cached providers return fresh copies.</remarks>
/// <example><code>var key = await keys.ResolveActiveAsync(cancellationToken);</code></example>
public interface IAuditIntegrityKeyProvider
{
    /// <summary>Resolves the active key used only for new finalized documents.</summary>
    /// <example><code>var active = await keys.ResolveActiveAsync();</code></example>
    Task<Result<AuditIntegrityKey>> ResolveActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>Resolves a historical key by exact authenticated identifier.</summary>
    /// <example><code>var historical = await keys.ResolveAsync(keyId);</code></example>
    Task<Result<AuditIntegrityKey>> ResolveAsync(string keyId, CancellationToken cancellationToken = default);
}
