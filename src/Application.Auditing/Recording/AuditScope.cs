// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Common;

/// <summary>Owns one recording handle; root disposal must be asynchronous to seal and admit evidence.</summary>
/// <example><code>await using var scope = audit.BeginScope(s =&gt; s.Key("customers.rename"));</code></example>
public sealed class AuditScope : IAuditScope, IAuditSourceScope, IAuditCaptureDiagnostics, IAuditChangeEvidenceSink
{
    private readonly AuditRuntime runtime;
    private readonly AuditCollector collector;
    private readonly IDisposable correlation;
    private int state;
    private Action completing;

    /// <summary>Creates the configured auditing component with explicit ownership dependencies.</summary>
    /// <example><code>var scope = new AuditScope(runtime, collector, null, 0);</code></example>
    public AuditScope(AuditRuntime runtime, AuditCollector collector, Guid? action, int depth, IDisposable correlation = null)
    { this.runtime = runtime; this.collector = collector; this.ActionId = action; this.Depth = depth; this.correlation = correlation; }

    /// <summary>Creates the configured auditing component with explicit ownership dependencies.</summary>
    /// <example><code>var value = scope.AuditId;</code></example>
    public Guid AuditId => this.collector.Id;

    /// <summary>Implements the audit scope ownership contract.</summary>
    /// <example><code>var value = scope.ActionId;</code></example>
    public Guid? ActionId { get; }

    /// <summary>Implements the audit scope ownership contract.</summary>
    /// <example><code>var value = scope.IsRecording;</code></example>
    public bool IsRecording => !this.Closed && this.collector.CanCapture && !this.runtime.Suppressed;

    /// <summary>Whether this handle has finished disposal.</summary>
    /// <example><code>var value = scope.Closed;</code></example>
    public bool Closed => Volatile.Read(ref this.state) >= 2;

    /// <summary>The action nesting depth.</summary>
    /// <example><code>var value = scope.Depth;</code></example>
    public int Depth { get; }

    /// <summary>The collector owned by this handle.</summary>
    /// <example><code>var value = scope.Collector;</code></example>
    public AuditCollector Collector => this.collector;

    /// <summary>Copies source facts into this occurrence without claiming a transaction commit.</summary>
    /// <example><code>var pending = scope.StageChange(sourceEvidence);</code></example>
    public AuditChangeHandle StageChange(AuditChangeEvidence evidence)
        => this.IsRecording ? this.collector.StageChange(this.ActionId, evidence) : null;

    /// <summary>Reports bounded source observation failures without changing the business outcome.</summary>
    /// <example><code>scope.ReportFailure("AuditSourceMetadataFailed");</code></example>
    public void ReportFailure(string reasonCode)
    {
        if (string.IsNullOrEmpty(reasonCode) || reasonCode.Length > 64 || reasonCode.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('.' or '_' or '-' or ':'))) reasonCode = "AuditReasonInvalid";
        this.runtime.Report(reasonCode);
        if (this.IsRecording) this.collector.CaptureNote(reasonCode);
    }

    /// <summary>Copies approved source identity while the occurrence remains recording.</summary>
    /// <example><code>scope.SetSource(new() { Feature = "Requester" });</code></example>
    public void SetSource(AuditSourceMetadata metadata)
    {
        if (!this.IsRecording) return;
        if (Interlocked.Exchange(ref this.sourceObserved, 1) == 0) this.runtime.ObserveBoundary(metadata?.Feature);
        this.collector.Source(this.ActionId, metadata);
    }

    private int sourceObserved;

    /// <summary>Copies permitted initiating context without retaining scoped services.</summary>
    /// <example><code>var link = scope.CaptureLink();</code></example>
    public AuditExecutionLink CaptureLink() => this.IsRecording ? this.collector.CaptureLink(this.ActionId) : null;

    /// <summary>Sets sanitized HTTP entry/final metadata on the owning execution.</summary>
    /// <example><code>scope.SetHttp(new() { Method = "POST", Status = 200 });</code></example>
    public void SetHttp(AuditHttpContext http)
    {
        if (this.IsRecording) this.collector.SetHttp(http);
    }

    /// <summary>Refreshes an initially anonymous HTTP actor using permitted final request identity.</summary>
    /// <example><code>scope.RefreshActor(currentUser);</code></example>
    public void RefreshActor(ICurrentUserAccessor user)
    {
        if (!this.IsRecording) return;
        try { this.collector.RefreshActor(user); }
        catch (Exception) { this.collector.CaptureNote("AuditActorUnavailable"); this.runtime.Report("AuditActorUnavailable"); }
    }

    /// <summary>Applies explicitly selected synchronous contributors before freezing.</summary>
    /// <example><code>scope.SetContributors(contributors, services);</code></example>
    public void SetContributors(IAuditContributor[] contributors, IServiceProvider services)
    {
        if (contributors.Length == 0 || !this.IsRecording) return;
        var view = new ContributionScope(this);
        Action completing = () =>
        {
            foreach (var contributor in contributors) this.runtime.Contribute(this.collector, () => contributor.OnCompleted(view, services));
        };
        if (!this.collector.RegisterCompletion(this, completing)) return;
        foreach (var contributor in contributors) this.runtime.Contribute(this.collector, () => contributor.OnStarting(view, services));
    }

    /// <summary>Sets the synchronous completion callback for this owned handle.</summary>
    /// <example><code>scope.SetCompletion(() =&gt; { });</code></example>
    public void SetCompletion(Action completing) => Interlocked.Exchange(ref this.completing, completing);

    /// <summary>Drops retained completion callbacks and scoped services.</summary>
    /// <example><code>scope.ClearCompletion();</code></example>
    public void ClearCompletion() => Interlocked.Exchange(ref this.completing, null);

    /// <summary>Returns the configured set detail.</summary>
    /// <example><code>var value = scope.SetDetail;</code></example>
    public void SetDetail(string name, AuditValue value)
    {
        if (!this.IsRecording) return;
        try { this.collector.Detail(this.ActionId, name, value); }
        catch (Exception) { this.collector.CaptureNote("AuditDetailProjectionFailed"); this.runtime.Report("AuditDetailProjectionFailed"); }
    }

    /// <summary>Returns the configured add resource.</summary>
    /// <example><code>var value = scope.AddResource;</code></example>
    public void AddResource(AuditResourceReference resource)
    {
        if (!this.IsRecording) return;
        try
        {
            if (resource?.ContextType is not null)
                resource = new AuditResourceReference(this.runtime.Options.NamingConvention.GetResourceType(resource.ContextType), resource.Id, resource.DisplayName);
            this.collector.Resource(this.ActionId, resource);
        }
        catch (Exception) { this.collector.CaptureNote("AuditResourceProjectionFailed"); this.runtime.Report("AuditResourceProjectionFailed"); }
    }

    /// <summary>Implements the audit scope ownership contract.</summary>
    /// <example><code>scope.Complete();</code></example>
    public void Complete(AuditOutcome outcome = AuditOutcome.Succeeded) { if (this.IsRecording) this.collector.Complete(this.ActionId, outcome); }

    /// <summary>Implements the audit scope ownership contract.</summary>
    /// <example><code>scope.Fail(error);</code></example>
    public void Fail(Exception exception) { if (this.IsRecording) this.collector.Fail(this.ActionId, exception); }

    /// <summary>Implements the audit scope ownership contract.</summary>
    /// <example><code>scope.Dispose();</code></example>
    public void Dispose()
    {
        if (!this.Prepare()) return;
        if (this.ActionId is null)
        {
            this.runtime.Report("AuditAsyncFinalizationRequired");
            this.runtime.Release(this.collector);
        }
    }

    /// <summary>Implements the audit scope ownership contract.</summary>
    /// <example><code>await scope.DisposeAsync();</code></example>
    public ValueTask DisposeAsync()
    {
        if (!this.Prepare() || this.ActionId.HasValue) return ValueTask.CompletedTask;
        return this.runtime.FinalizeAsync(this.collector);
    }

    private bool Prepare()
    {
        if (Interlocked.CompareExchange(ref this.state, 1, 0) != 0) return false;
        var complete = Interlocked.Exchange(ref this.completing, null);
        if (this.IsRecording) complete?.Invoke();
        Volatile.Write(ref this.state, 2);
        this.collector.Close(this.ActionId);
        this.runtime.Remove(this);
        this.correlation?.Dispose();
        return true;
    }
    private sealed class ContributionScope(AuditScope scope) : IAuditScope
    {
        /// <summary>Creates the configured auditing component with explicit ownership dependencies.</summary>
        /// <example><code>var value = scope.AuditId;</code></example>
        public Guid AuditId => scope.AuditId;

        /// <summary>Implements the audit scope ownership contract.</summary>
        /// <example><code>var value = scope.ActionId;</code></example>
        public Guid? ActionId => scope.ActionId;

        /// <summary>Implements the audit scope ownership contract.</summary>
        /// <example><code>var value = scope.IsRecording;</code></example>
        public bool IsRecording => scope.IsRecording;

        /// <summary>Returns the configured set detail.</summary>
        /// <example><code>var value = scope.SetDetail;</code></example>
        public void SetDetail(string name, AuditValue value) => scope.SetDetail(name, value);

        /// <summary>Returns the configured add resource.</summary>
        /// <example><code>var value = scope.AddResource;</code></example>
        public void AddResource(AuditResourceReference resource) => scope.AddResource(resource);

        /// <summary>Implements the audit scope ownership contract.</summary>
        /// <example><code>scope.Complete();</code></example>
        public void Complete(AuditOutcome outcome = AuditOutcome.Succeeded) { }

        /// <summary>Implements the audit scope ownership contract.</summary>
        /// <example><code>scope.Fail(error);</code></example>
        public void Fail(Exception exception) { }

        /// <summary>Implements the audit scope ownership contract.</summary>
        /// <example><code>scope.Dispose();</code></example>
        public void Dispose() { }

        /// <summary>Implements the audit scope ownership contract.</summary>
        /// <example><code>await scope.DisposeAsync();</code></example>
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
