// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using BridgingIT.DevKit.Common;

/// <summary>Stores the explicitly selected host resolver, invoked in the active request or worker scope.</summary>
/// <example><code>var resolver = new AuditReadScopeResolver((services, ct) => Task.FromResult(Result&lt;AuditReadScope&gt;.Success(AuditReadScope.Unrestricted())));</code></example>
public sealed class AuditReadScopeResolver(Func<IServiceProvider, CancellationToken, Task<Result<AuditReadScope>>> resolve)
{
    /// <summary>Resolves one host decision for an operation; null/failure is never unrestricted.</summary>
    /// <example><code>var scope = await resolver.ResolveAsync(services, cancellationToken);</code></example>
    public Task<Result<AuditReadScope>> ResolveAsync(IServiceProvider services, CancellationToken cancellationToken) => resolve(services, cancellationToken);
}
