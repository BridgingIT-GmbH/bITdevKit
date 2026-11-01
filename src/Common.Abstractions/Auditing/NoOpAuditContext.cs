// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>An explicit injection fallback that invokes business work without capture.</summary>
/// <example><code>IAuditContext audit = new NoOpAuditContext();</code></example>
public sealed class NoOpAuditContext : IAuditContext
{
    /// <summary>Metadata callbacks are always disabled.</summary>
    /// <example><code>audit.IsEnabled.ShouldBeFalse();</code></example>
    public bool IsEnabled => false;

    /// <summary>No recording scope is active.</summary>
    /// <example><code>var absent = audit.Current is null;</code></example>
    public IAuditScope Current => null;

    /// <summary>Returns a stateless handle.</summary>
    /// <example><code>using var scope = audit.BeginScope(options);</code></example>
    public IAuditScope BeginScope(AuditScopeOptions options) => NoOpAuditScope.Instance;

    /// <summary>Skips the metadata callback.</summary>
    /// <example><code>using var scope = audit.BeginScope(s => s.Key("customers.rename"));</code></example>
    public IAuditScope BeginScope(Action<AuditScopeOptionsBuilder> configure) => NoOpAuditScope.Instance;

    /// <summary>Returns a no-work suppression handle.</summary>
    /// <example><code>using var suppressed = audit.Suppress();</code></example>
    public IDisposable Suppress() => NoOpAuditScope.Instance;

    /// <summary>Invokes the delegate once with its original result and cancellation token.</summary>
    /// <example><code>var result = await audit.RunAsync(options, (scope, ct) => RenameAsync(ct));</code></example>
    public Task<T> RunAsync<T>(AuditScopeOptions options, Func<IAuditScope, CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        return action(NoOpAuditScope.Instance, cancellationToken);
    }

    /// <summary>Skips configuration and invokes business work once.</summary>
    /// <example><code>var result = await audit.RunAsync(s => s.Key("customers.rename"), (scope, ct) => RenameAsync(ct));</code></example>
    public Task<T> RunAsync<T>(Action<AuditScopeOptionsBuilder> configure, Func<IAuditScope, CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        return action(NoOpAuditScope.Instance, cancellationToken);
    }
}
