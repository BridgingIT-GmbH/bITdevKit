// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Owns optional source capture across a deferred enumerator lifetime, including its asynchronous cleanup.</summary>
/// <example><code>await using var lease = AuditSourceLease.Begin(scopes, builder => builder.Key("customers.import"));</code></example>
public sealed class AuditSourceLease : IAsyncDisposable
{
    private AsyncServiceScope? services;
    private IAuditScope scope = NoOpAuditScope.Instance;
    private IAuditCaptureDiagnostics diagnostics;
    private int disposed;
    private AuditSourceLease() { }

    /// <summary>Opens capture synchronously in the iterator's current flow. Disabled capture skips configuration.</summary>
    /// <example><code>await using var lease = AuditSourceLease.Begin(scopes, builder => builder.Key("customers.import"), new() { Feature = "DataPorter" });</code></example>
    public static AuditSourceLease Begin(IServiceScopeFactory scopes, Action<AuditScopeOptionsBuilder> configure, AuditSourceMetadata metadata = null, Action<IAuditScope> starting = null)
    {
        var lease = new AuditSourceLease();
        try
        {
            lease.services = scopes.CreateAsyncScope();
            var audit = lease.services.Value.ServiceProvider.GetService<IAuditContext>();
            lease.diagnostics = audit as IAuditCaptureDiagnostics;
            lease.scope = audit?.BeginScope(configure) ?? NoOpAuditScope.Instance;
            lease.Observe(scope => { (scope as IAuditSourceScope)?.SetSource(metadata); starting?.Invoke(scope); });
        }
        catch (Exception) { lease.Report(); }

        return lease;
    }

    /// <summary>Applies approved source facts only during recording; instrumentation failures do not alter enumeration.</summary>
    /// <example><code>lease.Observe(scope => scope.SetDetail("rows", AuditValue.From(12L)));</code></example>
    public void Observe(Action<IAuditScope> action)
    {
        try { if (this.scope.IsRecording) action(this.scope); }
        catch (Exception) { this.Report(); }
    }

    /// <summary>Records the original enumeration or cleanup exception without capturing its text.</summary>
    /// <example><code>catch (Exception error) { lease.Fail(error); throw; }</code></example>
    public void Fail(Exception error)
    {
        try { this.scope.Fail(error); }
        catch (Exception) { this.Report(); }
    }

    /// <summary>Finalizes capture after source cleanup and releases the per-operation service scope.</summary>
    /// <example><code>await lease.DisposeAsync();</code></example>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) != 0) return;
        try { await this.scope.DisposeAsync().ConfigureAwait(false); }
        catch (Exception) { this.Report(); }

        try { if (this.services.HasValue) await this.services.Value.DisposeAsync().ConfigureAwait(false); }
        catch (Exception) { this.Report(); }
    }

    private void Report()
    {
        try { this.diagnostics?.ReportFailure("AuditDeferredCaptureUnavailable"); }
        catch (Exception) { /* Source enumeration and cleanup remain authoritative. */ }
    }
}
