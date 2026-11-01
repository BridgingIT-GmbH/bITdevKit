// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Starts and joins business audit scopes in the current execution flow.</summary>
/// <example><code>await audit.RunAsync(s => s.Key("customers.rename"), async (scope, ct) => await RenameAsync(ct));</code></example>
public interface IAuditContext
{
    /// <summary>Whether a selected source may evaluate capture metadata in the current flow.</summary>
    /// <example><code>if (audit.IsEnabled) await RunSelectedBoundaryAsync();</code></example>
    bool IsEnabled => true;

    /// <summary>The live scope in this flow, or null.</summary>
    /// <example><code>var id = audit.Current?.AuditId;</code></example>
    IAuditScope Current { get; }

    /// <summary>Opens a scope using an immutable options snapshot.</summary>
    /// <example><code>await using var scope = audit.BeginScope(AuditScopeOptionsBuilder.Create("customers.rename").Build());</code></example>
    IAuditScope BeginScope(AuditScopeOptions options);

    /// <summary>Configures an admitted scope once. Disabled capture skips the callback.</summary>
    /// <example><code>await using var scope = audit.BeginScope(s => s.For&lt;Customer&gt;("Rename"));</code></example>
    IAuditScope BeginScope(Action<AuditScopeOptionsBuilder> configure);

    /// <summary>Suppresses all capture, including independent roots, until disposed.</summary>
    /// <example><code>using (audit.Suppress()) { await ReadEvidenceAsync(); }</code></example>
    IDisposable Suppress();

    /// <summary>Invokes business work once and asynchronously finalizes its observed outcome.</summary>
    /// <example><code>var result = await audit.RunAsync(options, (scope, ct) => RenameAsync(ct));</code></example>
    Task<T> RunAsync<T>(AuditScopeOptions options, Func<IAuditScope, CancellationToken, Task<T>> action, CancellationToken cancellationToken = default);

    /// <summary>Configures admitted capture and invokes business work once.</summary>
    /// <example><code>var result = await audit.RunAsync(s => s.Key("customers.rename"), (scope, ct) => RenameAsync(ct));</code></example>
    Task<T> RunAsync<T>(Action<AuditScopeOptionsBuilder> configure, Func<IAuditScope, CancellationToken, Task<T>> action, CancellationToken cancellationToken = default);
}
