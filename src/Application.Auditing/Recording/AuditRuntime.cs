// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Common;

using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Coordinates bounded root ownership, flow-local ambient frames and asynchronous provider admission.</summary>
/// <example><code>var component = services.GetRequiredService&lt;AuditRuntime&gt;();</code></example>
public sealed class AuditRuntime : IDisposable
{
    private readonly ConcurrentDictionary<string, long> observations = new(StringComparer.Ordinal);

    /// <summary>Process-local observation identity, independent of durable store identity and epochs.</summary>
    /// <example><code>var instance = runtime.InstanceId;</code></example>
    public Guid InstanceId { get; } = Guid.NewGuid();

    /// <summary>Returns bounded aggregate admission and append acknowledgments, without reading or flushing storage.</summary>
    /// <example><code>var observed = runtime.SnapshotObservations();</code></example>
    public IReadOnlyDictionary<string, long> SnapshotObservations() => this.observations.ToImmutableDictionary();

    /// <summary>Observes a source boundary without retaining request, resource or actor identifiers.</summary>
    /// <example><code>runtime.ObserveBoundary("Requester");</code></example>
    public void ObserveBoundary(string feature)
    {
        var safe = feature is "Http" or "Requester" or "Notifier" or "Repository" or "ActiveEntity" or "BulkInserter" or "ChangeHistory" or "Jobs" or "Messaging" or "Queueing" or "Pipeline" or "Orchestrations" or "FileStorage" or "BlobStorage" or "DocumentStorage" or "Email" or "DataPorter" or "StartupTasks" or "FileMonitoring" ? feature : "Other";
        this.Observe("BoundaryAdmitted." + safe);
    }

    private void Observe(string fact) => this.observations.AddOrUpdate(fact, 1, (_, count) => count == long.MaxValue ? count : count + 1);
    private readonly object admissionGate = new();
    private readonly AsyncLocal<ImmutableStack<AmbientEntry>> ambient = new();
    private readonly AsyncLocal<int> contributorDepth = new();
    private readonly ConcurrentDictionary<Guid, AuditCollector> active = new();
    private readonly HashSet<Guid> finalizing = [];
    private readonly ConcurrentDictionary<string, long> failures = new(StringComparer.Ordinal);
    private long? lastFailureTimestamp;
    private Guid observedDeliveryEpoch;
    private long observedDeliveryFailures;
    private readonly IAuditingStorageProvider provider;
    private readonly IAuditRecordSealer[] sealers;
    private readonly ITimer expiry;
    private int disposed;
    private int stopping;
    private TaskCompletionSource rootsIdle = CompletedIdle();

    /// <summary>Creates the configured auditing component with explicit ownership dependencies.</summary>
    /// <example><code>var runtime = new AuditRuntime(options, TimeProvider.System, storage, []);</code></example>
    public AuditRuntime(AuditingOptions options, TimeProvider time, IAuditingStorageProvider provider, IEnumerable<IAuditRecordSealer> sealers)
    {
        this.Options = options;
        this.Time = time;
        this.provider = provider;
        this.sealers = sealers.ToArray();
        if (this.sealers.Length > 1) throw new InvalidOperationException("Only one audit integrity sealer can be selected.");
        this.expiry = time.CreateTimer(_ => this.Expire(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    /// <summary>The immutable runtime configuration.</summary>
    /// <example><code>var value = runtime.Options;</code></example>
    public AuditingOptions Options { get; }

    /// <summary>The configured capture clock.</summary>
    /// <example><code>var value = runtime.Time;</code></example>
    public TimeProvider Time { get; }

    /// <summary>The current flow's immutable ambient frame stack.</summary>
    /// <example><code>var value = runtime.Stack;</code></example>
    public ImmutableStack<AmbientEntry> Stack { get => this.ambient.Value ?? ImmutableStack<AmbientEntry>.Empty; set => this.ambient.Value = value; }

    /// <summary>Whether this flow suppresses all capture.</summary>
    /// <example><code>var value = runtime.Suppressed;</code></example>
    public bool Suppressed => this.Stack.Any(e => e.Suppressed);

    /// <summary>Whether this flow is executing a metadata contributor.</summary>
    /// <example><code>var value = runtime.Enriching;</code></example>
    public bool Enriching => this.contributorDepth.Value > 0;

    /// <summary>Whether this flow owns an independent worker boundary.</summary>
    /// <example><code>var value = runtime.Worker;</code></example>
    public bool Worker => this.Stack.Any(e => e.Worker);

    /// <summary>The copied trusted initiating link for the current independent worker flow.</summary>
    /// <example><code>var link = runtime.InitiatingLink;</code></example>
    public AuditExecutionLink InitiatingLink => this.Stack.Select(entry => entry.Link).FirstOrDefault(link => link is not null);

    /// <summary>The current live scope, or null under suppression.</summary>
    /// <example><code>var value = runtime.Current;</code></example>
    public AuditScope Current => this.Suppressed ? null : this.Stack.Select(e => e.Scope).FirstOrDefault(s => s is not null && !s.Closed);

    /// <summary>Whether root ownership capacity is currently available.</summary>
    /// <example><code>var value = runtime.CanStart;</code></example>
    public bool CanStart => Volatile.Read(ref this.disposed) == 0 && this.active.Count < this.Options.Limits.MaxActiveRoots;

    /// <summary>Whether host shutdown has closed admission of new capture scopes.</summary>
    /// <example><code>var stopping = runtime.IsStopping;</code></example>
    public bool IsStopping => Volatile.Read(ref this.stopping) != 0;

    /// <summary>Stops new capture while existing owners retain bounded asynchronous finalization.</summary>
    /// <example><code>runtime.BeginShutdown();</code></example>
    public void BeginShutdown() { lock (this.admissionGate) Interlocked.Exchange(ref this.stopping, 1); }

    /// <summary>Awaits currently owned roots/finalizers after business producers have stopped.</summary>
    /// <remarks>This does not invent completion for an owning scope that never closed.</remarks>
    /// <example><code>await runtime.WaitForIdleAsync(cancellationToken);</code></example>
    public Task WaitForIdleAsync(CancellationToken cancellationToken = default)
    {
        lock (this.admissionGate) return this.rootsIdle.Task.WaitAsync(cancellationToken);
    }

    /// <summary>Adds a scope frame to the current flow.</summary>
    /// <example><code>runtime.Push(scope);</code></example>
    public void Push(AuditScope scope) => this.Stack = this.Stack.Push(new AmbientEntry(scope));

    /// <summary>Removes this flow's matching scope frame without changing parallel flows.</summary>
    /// <example><code>runtime.Remove(scope);</code></example>
    public void Remove(AuditScope scope) => this.Stack = ImmutableStack.CreateRange(this.Stack.Where(e => !ReferenceEquals(e.Scope, scope)).Reverse());

    /// <summary>Implements the audit scope ownership contract.</summary>
    /// <example><code>using var suppression = runtime.Suppress();</code></example>
    public IDisposable Suppress()
    {
        if (this.Suppressed) return NoOpAuditScope.Instance;
        var entry = new AmbientEntry(null, Suppressed: true);
        this.Stack = this.Stack.Push(entry);
        return new Restore(() => this.Stack = ImmutableStack.CreateRange(this.Stack.Where(e => !ReferenceEquals(e, entry)).Reverse()));
    }

    /// <summary>Implements the audit scope ownership contract.</summary>
    /// <example><code>using var boundary = runtime.DetachExecution();</code></example>
    public IDisposable DetachExecution() => this.DetachExecution(null);

    /// <summary>Starts an independent inbound HTTP boundary while preserving established application correlation.</summary>
    /// <example><code>using var boundary = runtime.DetachHttpExecution();</code></example>
    public IDisposable DetachHttpExecution()
    {
        var previous = this.Stack;
        var depth = this.contributorDepth.Value;
        this.Stack = ImmutableStack<AmbientEntry>.Empty;
        this.contributorDepth.Value = 0;
        return new Restore(() => { this.Stack = previous; this.contributorDepth.Value = depth; });
    }

    /// <summary>Isolates a worker from inherited capture, suppression, actor lookup, and correlation.</summary>
    /// <example><code>using var boundary = runtime.DetachExecution(trustedLink);</code></example>
    public IDisposable DetachExecution(AuditExecutionLink trustedLink)
    {
        if (trustedLink is not null && (trustedLink.OriginAuditId == Guid.Empty || trustedLink.CausationAuditId == Guid.Empty || !CorrelationId.IsValid(trustedLink.CorrelationId)))
        {
            this.Report("AuditLinkInvalid");
            trustedLink = null;
        }

        var previous = this.Stack;
        var previousDepth = this.contributorDepth.Value;
        this.contributorDepth.Value = 0;
        var correlation = CorrelationId.BeginScope(trustedLink?.CorrelationId);
        this.Stack = ImmutableStack<AmbientEntry>.Empty.Push(new AmbientEntry(null, Worker: true, Link: trustedLink));
        return new Restore(() => { this.Stack = previous; this.contributorDepth.Value = previousDepth; correlation.Dispose(); });
    }

    /// <summary>Reserves bounded ownership for a root; false means it was not admitted.</summary>
    /// <example><code>var admitted = runtime.Admit(collector);</code></example>
    public bool Admit(AuditCollector collector, bool duringFinalization = false)
    {
        lock (this.admissionGate)
        {
            if (this.IsStopping && !duringFinalization || !this.CanStart || !this.active.TryAdd(collector.Id, collector)) return false;
            if (!duringFinalization) this.Observe("RootAdmitted");
            if (this.rootsIdle.Task.IsCompleted) this.rootsIdle = new(TaskCreationOptions.RunContinuationsAsynchronously);
            return true;
        }
    }

    /// <summary>Releases tracked ownership after terminal finalization.</summary>
    /// <example><code>runtime.Release(collector);</code></example>
    public void Release(AuditCollector collector)
    {
        lock (this.admissionGate)
        {
            this.active.TryRemove(collector.Id, out _);
            this.finalizing.Remove(collector.Id);
            if (this.active.IsEmpty) this.rootsIdle.TrySetResult();
        }
    }

    /// <summary>Executes a synchronous metadata callback without recursive capture.</summary>
    /// <example><code>runtime.Contribute(collector, () =&gt; scope.SetDetail("process", AuditValue.From("customer-maintenance")));</code></example>
    public void Contribute(AuditCollector collector, Action callback)
    {
        this.contributorDepth.Value++;
        try { callback(); }
        catch (Exception) { collector.CaptureNote("AuditContributorFailed"); this.Report("AuditContributorFailed"); }
        finally { this.contributorDepth.Value--; }
    }

    /// <summary>Aggregates a safe reason code without retaining an exception or payload.</summary>
    /// <example><code>runtime.Report("AuditCaptureExpired");</code></example>
    public void Report(string code)
    {
        if (string.IsNullOrEmpty(code) || code.Length > 64 || code.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('.' or '_' or '-' or ':'))) code = "AuditReasonInvalid";
        lock (this.failures)
        {
            this.lastFailureTimestamp = this.Time.GetTimestamp();
            if (!this.failures.ContainsKey(code) && this.failures.Count >= 99) code = "AuditOtherFailures";
            this.failures.AddOrUpdate(code, 1, (_, count) => count == long.MaxValue ? count : count + 1);
        }
    }

    /// <summary>Copies safe instance-local failure counters.</summary>
    /// <example><code>var failures = runtime.SnapshotFailures();</code></example>
    public IReadOnlyDictionary<string, long> SnapshotFailures() => this.failures.ToImmutableDictionary();

    /// <summary>Observes cumulative delivery failures and reports whether a failure is inside the health recovery window.</summary>
    /// <remarks>Delivery-only failures are timed from first observation. Repeated probes preserve that timestamp and all lifetime counters.</remarks>
    /// <example><code>var degraded = runtime.HasRecentFailures(provider.DeliveryStatus);</code></example>
    public bool HasRecentFailures(AuditFlushResult delivery = null)
    {
        lock (this.failures)
        {
            if (delivery is not null)
            {
                if (delivery.Failed > 0 && (delivery.ReceiptEpoch != this.observedDeliveryEpoch || delivery.Failed > this.observedDeliveryFailures))
                    this.lastFailureTimestamp = this.Time.GetTimestamp();
                this.observedDeliveryFailures = delivery.ReceiptEpoch == this.observedDeliveryEpoch
                    ? Math.Max(this.observedDeliveryFailures, delivery.Failed) : delivery.Failed;
                this.observedDeliveryEpoch = delivery.ReceiptEpoch;
            }

            return this.lastFailureTimestamp is { } timestamp && this.Time.GetElapsedTime(timestamp) < this.Options.HealthFailureWindow;
        }
    }

    /// <summary>Awaits sealing and provider admission for an already closed collector.</summary>
    /// <example><code>await runtime.FinalizeAsync(collector);</code></example>
    public async ValueTask FinalizeAsync(AuditCollector collector)
    {
        // Expired owners can close later. Re-admit that finalization under the
        // same finite capacity instead of starting unbounded sealing work.
        lock (this.admissionGate)
        {
            if (!this.active.ContainsKey(collector.Id) && !this.Admit(collector, duringFinalization: true))
            {
                this.Report("AuditFinalizationLimit");
                return;
            }

            if (!this.finalizing.Add(collector.Id)) return;
        }

        using var suppression = this.Suppress();
        CancellationTokenSource deadline = null;
        Task operation = null;
        try
        {
            var frozen = AuditRecordCodec.Freeze(collector.Snapshot());
            if (frozen.IsFailure) { this.Report("AuditFreezeFailed"); return; }

            deadline = new CancellationTokenSource(this.Options.FinalizationTimeout, this.Time);
            foreach (var sealer in this.sealers)
            {
                var sealing = sealer.SealAsync(frozen.Value, deadline.Token);
                operation = sealing;
                frozen = await sealing.WaitAsync(deadline.Token).ConfigureAwait(false);
                if (frozen.IsFailure) { this.Report("AuditSealFailed"); return; }
            }

            if (AuditRecordCodec.Encode(frozen.Value).Length > this.Options.Limits.MaxRecordBytes) { this.Report("AuditRecordTooLarge"); return; }

            deadline.Token.ThrowIfCancellationRequested();
            var appending = this.provider.AppendAsync(frozen.Value, deadline.Token);
            operation = appending;
            var append = await appending.WaitAsync(deadline.Token).ConfigureAwait(false);
            if (append.IsFailure) this.Report("AuditAdmissionFailed");
            else this.Observe(append.Value.State == "Queued" ? "AppendQueued" : "AppendPresenceConfirmed");
        }
        catch (Exception) { this.Report("AuditFinalizationFailed"); }
        finally
        {
            if (operation is { IsCompleted: false })
            {
                // Retain bounded ownership until uncooperative external work actually ends.
                // A late sealer is observed here and never advances to provider admission.
                _ = operation.ContinueWith(task =>
                {
                    _ = task.Exception;
                    deadline?.Dispose();
                    this.Release(collector);
                }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
            else
            {
                _ = operation?.Exception;
                deadline?.Dispose();
                this.Release(collector);
            }
        }
    }

    private void Expire()
    {
        var now = this.Time.GetUtcNow();
        lock (this.admissionGate)
        {
            foreach (var collector in this.active.Values)
            {
                if (now - collector.Started >= this.Options.Limits.MaxLifetime && collector.Stop("AuditCaptureExpired"))
                {
                    this.Release(collector);
                    this.Report("AuditCaptureExpired");
                }
            }
        }
    }

    /// <summary>Implements the audit scope ownership contract.</summary>
    /// <example><code>scope.Dispose();</code></example>
    public void Dispose()
    {
        AuditCollector[] remaining;
        lock (this.admissionGate)
        {
            if (Interlocked.Exchange(ref this.disposed, 1) != 0) return;
            remaining = this.active.Values.ToArray();
            foreach (var collector in remaining.Where(c => !this.finalizing.Contains(c.Id))) this.active.TryRemove(collector.Id, out _);
            if (this.active.IsEmpty) this.rootsIdle.TrySetResult();
        }

        this.expiry.Dispose();
        foreach (var collector in remaining) collector.Stop("AuditRuntimeStopped");
    }

    /// <summary>One immutable ambient frame for scope, suppression or worker ownership.</summary>
    /// <example><code>var component = services.GetRequiredService&lt;AmbientEntry&gt;();</code></example>
    public sealed record AmbientEntry(AuditScope Scope, bool Suppressed = false, bool Worker = false, AuditExecutionLink Link = null);

    private static TaskCompletionSource CompletedIdle()
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        completed.SetResult();
        return completed;
    }
    private sealed class Restore(Action restore) : IDisposable
    {
        private int disposed;

        /// <summary>Implements the audit scope ownership contract.</summary>
        /// <example><code>scope.Dispose();</code></example>
        public void Dispose() { if (Interlocked.Exchange(ref this.disposed, 1) == 0) restore(); }
    }
}
