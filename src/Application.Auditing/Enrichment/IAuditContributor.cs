// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using BridgingIT.DevKit.Common;

/// <summary>Adds explicit execution metadata through the same capture policies as manual scopes.</summary>
/// <remarks>Callbacks are synchronous and must not retain the scope or service provider.</remarks>
/// <example><code>scope.SetDetail("process", AuditValue.From("customer-maintenance"));</code></example>
public interface IAuditContributor
{
    /// <summary>Adds approved context after the scope is admitted.</summary>
    /// <example><code>public void OnStarting(IAuditScope scope, IServiceProvider services) => scope.SetDetail("process", AuditValue.From("customer-maintenance"));</code></example>
    void OnStarting(IAuditScope scope, IServiceProvider services);

    /// <summary>Adds approved completion context before freezing.</summary>
    /// <example><code>public void OnCompleted(IAuditScope scope, IServiceProvider services) { }</code></example>
    void OnCompleted(IAuditScope scope, IServiceProvider services);
}
