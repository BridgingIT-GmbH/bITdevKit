// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Common;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Scoped facade over flow-local frames and per-root collectors.</summary>
/// <example><code>var audit = services.GetRequiredService&lt;IAuditContext&gt;();</code></example>
public sealed class AuditContext : IAuditContext, IAuditExecutionBoundary, IAuditCaptureDiagnostics
{
    private readonly IServiceProvider services;
    private readonly AuditRuntime runtime;

    /// <summary>Creates the configured auditing component with explicit ownership dependencies.</summary>
    /// <example><code>var audit = new AuditContext(services, runtime);</code></example>
    public AuditContext(IServiceProvider services, AuditRuntime runtime) { this.services = services; this.runtime = runtime; }

    /// <summary>Whether selected adapters may evaluate metadata under current capture admission policy.</summary>
    /// <example><code>var enabled = audit.IsEnabled;</code></example>
    public bool IsEnabled => this.Eligible();

    /// <summary>The live current occurrence, or null under suppression/no capture.</summary>
    /// <example><code>var execution = audit.Current?.AuditId;</code></example>
    public IAuditScope Current => this.runtime.Current;

    /// <summary>Configures an eligible scope once, skipping metadata under disabled/suppressed capture.</summary>
    /// <example><code>await using var scope = audit.BeginScope(s => s.Key("customers.rename"));</code></example>
    public IAuditScope BeginScope(Action<AuditScopeOptionsBuilder> configure)
    {
        if (!this.Eligible()) return NoOpAuditScope.Instance;
        try { var builder = new AuditScopeOptionsBuilder(); configure(builder); return this.BeginScope(builder.Build()); }
        catch (Exception) { this.runtime.Current?.Collector.CaptureNote("AuditConfigurationFailed"); this.runtime.Report("AuditConfigurationFailed"); return NoOpAuditScope.Instance; }
    }

    /// <summary>Joins a live execution or owns an independent root as selected by immutable options.</summary>
    /// <example><code>await using var scope = audit.BeginScope(options);</code></example>
    public IAuditScope BeginScope(AuditScopeOptions options)
    {
        if (!this.Eligible(options?.Mode ?? AuditScopeMode.JoinOrStart)) return NoOpAuditScope.Instance;
        IDisposable correlation = null;
        AuditScope opened = null;
        try
        {
            ArgumentNullException.ThrowIfNull(options);
            var descriptor = options.Descriptor;
            if (descriptor.ContextType is not null)
            {
                var naming = this.runtime.Options.NamingConvention;
                options = new AuditScopeOptions(new AuditDescriptor(
                    descriptor.KeyDerived ? naming.GetKey(descriptor.ContextType, descriptor.Operation) : descriptor.Key,
                    descriptor.DisplayNameDerived ? naming.GetDisplayName(descriptor.ContextType, descriptor.Operation) : descriptor.DisplayName,
                    descriptor.Reason, descriptor.Resources, descriptor.Details, descriptor.ContextType, descriptor.Operation,
                    descriptor.KeyDerived, descriptor.DisplayNameDerived), options.Kind, options.Mode);
            }

            var parent = this.runtime.Current;
            string module = null;
            var moduleFailed = false;
            try { module = this.services.GetService<IAuditModuleContextAccessor>()?.Find(options.Descriptor.ContextType); }
            catch (Exception) { moduleFailed = true; this.runtime.Report("AuditModuleUnavailable"); }

            if (module is not null && AuditCollector.Text(module, 512) != module) { module = null; moduleFailed = true; }

            AuditScope scope;
            if (parent is not null && options.Mode == AuditScopeMode.JoinOrStart)
            {
                var action = parent.Collector.AddAction(options, parent.ActionId, parent.Depth + 1, AuditCollector.Text(module, 512));
                if (action is null) return NoOpAuditScope.Instance;
                scope = new AuditScope(this.runtime, parent.Collector, action, parent.Depth + 1);
            }
            else
            {
                if (!this.runtime.CanStart) { this.runtime.Report("AuditRootLimit"); parent?.Collector.CaptureNote("AuditRootLimit"); return NoOpAuditScope.Instance; }

                correlation = CorrelationId.BeginScope(CorrelationId.IsValid(CorrelationId.Current) ? CorrelationId.Current : CorrelationIdGenerator.Create());
                var actor = new AuditActor { Kind = this.runtime.Worker ? "Service" : "Anonymous" };
                var actorFailed = false;
                var actorIdentityOmitted = false;
                try
                {
                    if (!this.runtime.Worker && this.services.GetService<ICurrentUserAccessor>() is { IsAuthenticated: true } user)
                    {
                        var userId = user.UserId;
                        var userName = user.UserName;
                        actorIdentityOmitted = userId is not null && BoundedIdentity(userId) is null;
                        actor = new AuditActor { Kind = "User", IsAuthenticated = true, UserId = BoundedIdentity(userId), DisplayName = userName?.Contains('@') == true ? null : AuditCollector.Text(userName, 1_024) };
                    }
                }
                catch (Exception) { actorFailed = true; this.runtime.Report("AuditActorUnavailable"); }

                var id = Guid.NewGuid();
                var parentRecord = parent?.Collector.Snapshot();
                var root = new AuditRecord
                {
                    AuditId = id, OriginAuditId = parentRecord?.OriginAuditId ?? id,
                    CausationAuditId = parent?.AuditId, CausationActionId = parent?.ActionId,
                    InitiatingActor = parentRecord?.InitiatingActor, InitiatingModuleName = parentRecord?.InitiatingModuleName,
                    Key = options.Descriptor.Key, DisplayName = AuditCollector.Text(options.Descriptor.DisplayName, 1_024), Kind = options.Kind,
                    Reason = AuditCollector.Text(options.Descriptor.Reason, 4_096), Application = this.runtime.Options.Application, Node = this.runtime.Options.Node,
                    Actor = actor, ModuleName = AuditCollector.Text(module, 512), CorrelationId = CorrelationId.Current, StartedUtc = this.runtime.Time.GetUtcNow()
                };
                var link = parent is null ? this.runtime.InitiatingLink : null;
                if (link is not null)
                {
                    root = root with
                    {
                        OriginAuditId = link.OriginAuditId, CausationAuditId = link.CausationAuditId, CausationActionId = link.CausationActionId,
                        InitiatingModuleName = AuditCollector.Text(link.InitiatingModuleName, 512),
                        InitiatingActor = link.InitiatingActor is not { } initiator ? null : new()
                        {
                            Kind = AuditCollector.Text(initiator.Kind, 128), UserId = BoundedIdentity(initiator.UserId),
                            DisplayName = initiator.DisplayName?.Contains('@') == true ? null : AuditCollector.Text(initiator.DisplayName, 1_024), IsAuthenticated = initiator.IsAuthenticated
                        }
                    };
                }

                var collector = new AuditCollector(root, this.runtime.Options, this.runtime.Time);
                if (actorFailed) collector.CaptureNote("AuditActorUnavailable");
                if (actorIdentityOmitted) collector.CaptureNote("AuditActorIdentityOmitted");
                if (!this.runtime.Admit(collector)) { correlation?.Dispose(); return NoOpAuditScope.Instance; }

                scope = new AuditScope(this.runtime, collector, null, 0, correlation);
            }

            opened = scope;
            this.runtime.Push(scope);
            if (moduleFailed) scope.Collector.CaptureNote("AuditModuleUnavailable");
            if (AuditCollector.Text(options.Descriptor.DisplayName, 1_024) != options.Descriptor.DisplayName) scope.Collector.CaptureNote("AuditLabelTruncated");
            if (AuditCollector.Text(options.Descriptor.Reason, 4_096) != options.Descriptor.Reason) scope.Collector.CaptureNote("AuditReasonTruncated");
            foreach (var detail in options.Descriptor.Details) scope.SetDetail(detail.Key, detail.Value);
            foreach (var resource in options.Descriptor.Resources) scope.AddResource(resource);
            scope.SetContributors(this.services.GetServices<IAuditContributor>().Take(32).ToArray(), this.services);
            return scope;
        }
        catch (Exception) { opened?.Dispose(); correlation?.Dispose(); this.runtime.Current?.Collector.CaptureNote("AuditEnrichmentFailed"); this.runtime.Report("AuditEnrichmentFailed"); return NoOpAuditScope.Instance; }
    }

    /// <summary>Reports source observation failures using safe bounded codes.</summary>
    /// <example><code>audit.ReportFailure("AuditSourceSelectionFailed");</code></example>
    public void ReportFailure(string reasonCode)
    {
        if (string.IsNullOrEmpty(reasonCode) || reasonCode.Length > 64 || reasonCode.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('.' or '_' or '-' or ':'))) reasonCode = "AuditReasonInvalid";
        this.runtime.Report(reasonCode);
        this.runtime.Current?.Collector.CaptureNote(reasonCode);
    }

    /// <summary>Suppresses all capture including RequiresNew until disposed.</summary>
    /// <example><code>using var suppressed = audit.Suppress();</code></example>
    public IDisposable Suppress() => this.runtime.Suppress();

    /// <summary>Isolates real worker execution and restores this flow afterward.</summary>
    /// <example><code>using var boundary = audit.DetachExecution();</code></example>
    public IDisposable DetachExecution() => this.runtime.DetachExecution();

    /// <summary>Isolates an actual worker using explicitly trusted copied causation.</summary>
    /// <example><code>using var boundary = audit.DetachExecution(trustedLink);</code></example>
    public IDisposable DetachExecution(AuditExecutionLink trustedLink) => this.runtime.DetachExecution(trustedLink);

    /// <summary>Executes business work once and awaits root sealing/admission.</summary>
    /// <example><code>var result = await audit.RunAsync(options, (scope, ct) => RenameAsync(ct));</code></example>
    public Task<T> RunAsync<T>(AuditScopeOptions options, Func<IAuditScope, CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
        => this.RunOwnedAsync(() => this.BeginScope(options), action, cancellationToken);

    /// <summary>Configures eligible metadata once and preserves business return/exception semantics.</summary>
    /// <example><code>var result = await audit.RunAsync(s => s.For&lt;Customer&gt;("Rename"), (scope, ct) => RenameAsync(ct));</code></example>
    public Task<T> RunAsync<T>(Action<AuditScopeOptionsBuilder> configure, Func<IAuditScope, CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
        => this.RunOwnedAsync(() => this.BeginScope(configure), action, cancellationToken);

    private static string BoundedIdentity(string value) => value is null || AuditCollector.Text(value, 1_024) == value ? value : null;
    private bool Eligible(AuditScopeMode mode = AuditScopeMode.JoinOrStart)
    {
        var current = this.runtime.Current;
        var reason = AuditCaptureEligibility.Reason(this.runtime.Options.Enabled, this.runtime.IsStopping, this.runtime.Enriching, this.runtime.Suppressed,
            current is not null && mode == AuditScopeMode.JoinOrStart, current?.IsRecording == true, this.runtime.CanStart);
        if (reason == "AuditRootLimit") this.runtime.Report(reason);
        return reason == "Eligible";
    }
    private async Task<T> RunOwnedAsync<T>(Func<IAuditScope> begin, Func<IAuditScope, CancellationToken, Task<T>> action, CancellationToken token)
    {
        var scope = begin();
        try
        {
            ArgumentNullException.ThrowIfNull(action);
            var value = await action(scope, token).ConfigureAwait(false);
            scope.Complete(value is IResult { IsFailure: true } ? AuditOutcome.Rejected : AuditOutcome.Succeeded);
            return value;
        }
        catch (Exception error) { scope.Fail(error); throw; }
        finally { await scope.DisposeAsync().ConfigureAwait(false); }
    }
}
