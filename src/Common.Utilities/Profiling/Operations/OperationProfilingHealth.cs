// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Reads node-local recording and persistence health without flushing pending data.</summary>
/// <example><code>var health = services.GetRequiredService&lt;IOperationProfilingHealthSource&gt;().GetSnapshot();</code></example>
public interface IOperationProfilingHealthSource
{
    /// <summary>Returns a point-in-time local health observation, distinct from shared retained counts.</summary>
    /// <example><code>var queued = healthSource.GetSnapshot().QueueRecords;</code></example>
    OperationProfilingHealth GetSnapshot();
}

/// <summary>Stores process-local health counters independently from shared retained history.</summary>
/// <example>Used by the shared profiling writer and its node-local health observations.</example>
public sealed class OperationProfilingHealthState : IOperationProfilingHealthSource
{
    private readonly OperationProfilingCompletionQueue queue;
    private readonly IOperationProfiler profiler;
    private readonly IProfilingStorageProvider provider;
    private readonly IProfilingNodeIdentityProvider nodes;
    private readonly bool enabled;
    /// <summary>Exposes the Persisted profiling observation or lifecycle value.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public long Persisted;
    /// <summary>Exposes the Lost profiling observation or lifecycle value.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public long Lost;
    /// <summary>Exposes the Administrative profiling observation or lifecycle value.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public long Administrative;
    /// <summary>Exposes the Unknown profiling observation or lifecycle value.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public long Unknown;
    /// <summary>Exposes the Retries profiling observation or lifecycle value.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public long Retries;
    /// <summary>Exposes the Faults profiling observation or lifecycle value.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public long Faults;
    /// <summary>Exposes the LastFlushTicks profiling observation or lifecycle value.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public long LastFlushTicks;
    /// <summary>Exposes the LastAttemptTicks profiling observation or lifecycle value.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public long LastAttemptTicks;
    /// <summary>Exposes the PreparingClears profiling observation or lifecycle value.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public long PreparingClears;
    /// <summary>Exposes the ApplyingClears profiling observation or lifecycle value.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public long ApplyingClears;
    /// <summary>Exposes the RetentionRemovals profiling observation or lifecycle value.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public long RetentionRemovals;
    /// <summary>Exposes the EligibleRequests profiling observation or lifecycle value.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public long EligibleRequests;
    /// <summary>Exposes the SelectedRequests profiling observation or lifecycle value.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public long SelectedRequests;
    /// <summary>Exposes the SamplingErrors profiling observation or lifecycle value.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public long SamplingErrors;
    /// <summary>Exposes the Stalled profiling observation or lifecycle value.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public volatile bool Stalled;
    /// <summary>Exposes the MaximumClears profiling observation or lifecycle value.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public int MaximumClears { get; }

    /// <summary>Stores process-local health counters independently from shared retained history.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public OperationProfilingHealthState(OperationProfilingCompletionQueue queue, IProfilingStorageProvider provider,
        IProfilingNodeIdentityProvider nodes, ProfilingOptions options, IOperationProfiler profiler = null)
    {
        this.queue = queue;
        this.provider = provider;
        this.nodes = nodes;
        this.profiler = profiler;
        this.enabled = options.OperationEnabled;
        this.MaximumClears = options.Storage.MaximumClearFences;
    }

    /// <summary>Reads local capture, buffering, sampling and persistence health without flushing.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public OperationProfilingHealth GetSnapshot()
    {
        var queued = this.queue.Snapshot();
        var recorder = this.profiler as OperationProfiler;
        var counters = recorder?.Counters;
        return new()
        {
            AdmittedOperations = counters is null ? 0 : Interlocked.Read(ref counters.Admitted),
            RejectedOperations = counters is null ? 0 : Interlocked.Read(ref counters.Rejected),
            CompletedOperations = counters is null ? 0 : Interlocked.Read(ref counters.Completed),
            ActiveOperations = recorder?.ActiveCount ?? 0, ActivePayloadBytes = recorder?.ActiveBytes ?? 0,
            QueueRecords = queued.Count, QueuePayloadBytes = queued.Bytes, InFlightRecords = queued.InFlightCount, InFlightPayloadBytes = queued.InFlightBytes, QueueOldestAge = queued.OldestAge,
            PersistedOperations = Interlocked.Read(ref this.Persisted),
            DroppedOperations = Interlocked.Read(ref this.Lost) + Interlocked.Read(ref this.queue.CapacityDiscards)
                + Interlocked.Read(ref this.queue.WriterUnavailableDiscards) + Interlocked.Read(ref this.queue.ExpiredLeaseDiscards),
            AdministrativeDiscards = Interlocked.Read(ref this.Administrative), UnknownCommits = Interlocked.Read(ref this.Unknown),
            WriterUnavailableDiscards = Interlocked.Read(ref this.queue.WriterUnavailableDiscards), ExpiredLeaseDiscards = Interlocked.Read(ref this.queue.ExpiredLeaseDiscards),
            CaptureFaults = counters is null ? 0 : Interlocked.Read(ref counters.CaptureFaults) + Interlocked.Read(ref counters.LoggingFaults),
            LastSuccessfulFlushUtc = new(Interlocked.Read(ref this.LastFlushTicks), TimeSpan.Zero),
            LastAttemptUtc = new(Interlocked.Read(ref this.LastAttemptTicks), TimeSpan.Zero),
            ProviderScope = this.provider.Capabilities.Scope, ProviderShared = this.provider.Capabilities.Shared,
            CountersNodeId = this.nodes.GetNode().Identity.Id, CaptureEnabled = this.enabled,
            WriterActive = queued.Active, WriterStalled = this.Stalled,
            PreparingClears = Interlocked.Read(ref this.PreparingClears), ApplyingClears = Interlocked.Read(ref this.ApplyingClears),
            PersistenceFaults = Interlocked.Read(ref this.Faults), RetryAttempts = Interlocked.Read(ref this.Retries), RetentionRemovals = Interlocked.Read(ref this.RetentionRemovals),
            EligibleRequests = Interlocked.Read(ref this.EligibleRequests), SelectedRequests = Interlocked.Read(ref this.SelectedRequests), SamplingErrors = Interlocked.Read(ref this.SamplingErrors),
        };
    }
}
