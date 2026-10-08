// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

// All accepted payload, including selected and retrying batches, stays owned here until settlement.
/// <summary>Owns bounded immutable completions through queueing, retries and in-flight persistence.</summary>
/// <example>Used by the shared profiling writer and its node-local health observations.</example>
public sealed class OperationProfilingCompletionQueue : IOperationProfilingCompletionSink
{
    private readonly object sync = new();
    private readonly LinkedList<Entry> pending = new();
    private readonly OperationProfilingOptions options;
    private readonly TimeProvider clock;
    private ProfilingWriterLease lease;
    private long sequence;
    private long bytes;
    /// <summary>Exposes the CapacityDiscards profiling observation or lifecycle value.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public long CapacityDiscards;
    /// <summary>Exposes the WriterUnavailableDiscards profiling observation or lifecycle value.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public long WriterUnavailableDiscards;
    /// <summary>Exposes the ExpiredLeaseDiscards profiling observation or lifecycle value.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public long ExpiredLeaseDiscards;

    /// <summary>Owns bounded immutable completions through queueing, retries and in-flight persistence.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public OperationProfilingCompletionQueue(ProfilingOptions options, TimeProvider clock = null)
    {
        this.options = options.Operations.Snapshot();
        this.clock = clock ?? TimeProvider.System;
    }

    /// <summary>Attempts immediate publication under the active lease and returns false when unavailable or full.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public bool TryEnqueue(OperationProfilingRecord record)
    {
        lock (this.sync)
        {
            if (this.lease is null)
            {
                this.WriterUnavailableDiscards++;
                return false;
            }

            var issued = checked(++this.sequence);
            if (record is null || record.EstimatedPayloadBytes <= 0 || record.EstimatedPayloadBytes > this.options.BatchBytes
                || this.pending.Count >= this.options.QueueCapacity || record.EstimatedPayloadBytes > this.options.QueueBytes - this.bytes)
            {
                this.CapacityDiscards++;
                return false;
            }

            this.pending.AddLast(new Entry(new() { Lease = this.lease, CompletionSequence = issued, Record = record }, this.clock.GetTimestamp()));
            this.bytes += record.EstimatedPayloadBytes;
            return true;
        }
    }

    /// <summary>Activates or renews an original lease without relabeling pending envelopes.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public void Activate(ProfilingWriterLease value)
    {
        lock (this.sync)
        {
            if (this.lease is not null && this.lease.Token != value.Token || this.lease is null && this.pending.Count > 0)
            {
                throw new InvalidOperationException("Retire the original queue before replacing its writer lease.");
            }

            if (this.lease is null)
            {
                this.sequence = value.SettledThrough;
            }

            this.lease = value;
        }
    }

    /// <summary>Discards envelopes under an expired original lease and ends its sequence domain.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public void Retire(bool expired = true)
    {
        lock (this.sync)
        {
            this.ExpiredLeaseDiscards += expired ? this.pending.Count : 0;
            this.pending.Clear();
            this.bytes = 0;
            this.lease = null;
        }
    }

    /// <summary>Reads queue and in-flight payload accounting without triggering persistence.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public (ProfilingWriterLease Lease, long Sequence) Watermark()
    {
        lock (this.sync) { return (this.lease, this.sequence); }
    }

    /// <summary>Exposes the Member profiling observation or lifecycle value.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public long Settlement
    {
        get { lock (this.sync) { return this.pending.First is { } first ? first.Value.Envelope.CompletionSequence - 1 : this.sequence; } }
    }

    /// <summary>Selects one bounded eligible batch below a fixed lease-qualified tick watermark.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public IReadOnlyList<Entry> Select((ProfilingWriterLease Lease, long Sequence) watermark)
    {
        lock (this.sync)
        {
            if (this.lease?.Token != watermark.Lease?.Token)
            {
                return [];
            }

            var selected = new List<Entry>();
            long selectedBytes = 0;
            var now = this.clock.GetTimestamp();
            foreach (var entry in this.pending)
            {
                if (entry.Envelope.CompletionSequence > watermark.Sequence)
                {
                    break;
                }

                if (entry.RetryAt is { } retry && now < retry)
                {
                    continue;
                }

                if (selected.Count == this.options.BatchSize || entry.Envelope.Record.EstimatedPayloadBytes > this.options.BatchBytes - selectedBytes)
                {
                    break;
                }

                selected.Add(entry);
                selectedBytes += entry.Envelope.Record.EstimatedPayloadBytes;
            }

            return selected.AsReadOnly();
        }
    }

    /// <summary>Releases an accepted or terminal entry exactly once from payload accounting.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public void Remove(Entry entry)
    {
        lock (this.sync)
        {
            if (this.pending.Remove(entry))
            {
                this.bytes -= entry.Envelope.Record.EstimatedPayloadBytes;
            }
        }
    }

    /// <summary>Reads queue and in-flight payload accounting without triggering persistence.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public (int Count, long Bytes, TimeSpan OldestAge, bool Active, int InFlightCount, long InFlightBytes) Snapshot()
    {
        lock (this.sync)
        {
            return (this.pending.Count, this.bytes, this.pending.First is { } first ? this.clock.GetElapsedTime(first.Value.EnqueuedAt) : TimeSpan.Zero, this.lease is not null, this.pending.Count(e => e.InFlight), this.pending.Where(e => e.InFlight).Sum(e => e.Envelope.Record.EstimatedPayloadBytes));
        }
    }

    /// <summary>Updates in-flight observation while retaining the same bounded payload ownership.</summary>
    /// <example><code>queue.ObserveInFlight(batch, true);</code></example>
    public void ObserveInFlight(IReadOnlyList<Entry> entries, bool active)
    {
        lock (this.sync)
        {
            foreach (var entry in entries) { entry.InFlight = active; }
        }
    }

    /// <summary>Retains an original immutable write envelope and bounded retry state.</summary>
    /// <param name="envelope">The original lease-qualified completion.</param>
    /// <param name="enqueuedAt">The monotonic publication timestamp.</param>
    /// <example><code>var sequence = entry.Envelope.CompletionSequence;</code></example>
    public sealed class Entry(ProfilingWriteEnvelope envelope, long enqueuedAt)
    {
        /// <summary>Exposes the Envelope profiling observation or lifecycle value.</summary>
        /// <example>Used by the shared profiling writer and its node-local health observations.</example>
        public ProfilingWriteEnvelope Envelope { get; } = envelope;
        /// <summary>Exposes the EnqueuedAt profiling observation or lifecycle value.</summary>
        /// <example>Used by the shared profiling writer and its node-local health observations.</example>
        public long EnqueuedAt { get; } = enqueuedAt;
        /// <summary>Exposes the Retries profiling observation or lifecycle value.</summary>
        /// <example>Used by the shared profiling writer and its node-local health observations.</example>
        public int Retries { get; set; }
        /// <summary>Exposes the Uncertain profiling observation or lifecycle value.</summary>
        /// <example>Used by the shared profiling writer and its node-local health observations.</example>
        public bool Uncertain { get; set; }
        /// <summary>Gets or sets the active provider-call observation for this entry.</summary>
        /// <example><code>var active = entry.InFlight;</code></example>
        public bool InFlight { get; set; }
        /// <summary>Exposes the RetryAt profiling observation or lifecycle value.</summary>
        /// <example>Used by the shared profiling writer and its node-local health observations.</example>
        public long? RetryAt { get; set; }
    }
}
