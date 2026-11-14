// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using System.Security.Cryptography;
using System.Text;

/// <summary>Collects fixed-cardinality execution facts for one startup-declared collection or named provider.</summary>
/// <param name="collection">The declared collection alias, or null for provider-wide observations.</param>
/// <param name="provider">The declared provider alias.</param>
/// <param name="strategy">The controlled coordination strategy.</param>
/// <param name="options">The validated finite diagnostics policy.</param>
/// <param name="timeProvider">The process observation clock.</param>
/// <param name="metrics">The bounded standard-metrics bridge.</param>
/// <param name="availability">Whether optional bookkeeping capacity was admitted.</param>
/// <remarks>Keep the slot on the engine instance. Disabled Record calls allocate no objects or label collections.</remarks>
/// <example><code>slot.Record(CacheObservationKind.PositiveHit);</code></example>
public sealed class CacheObservationSlot(string collection, string provider, string strategy, CacheDiagnosticsOptions options,
    TimeProvider timeProvider, CacheMetrics metrics, CacheObservationAvailability availability = CacheObservationAvailability.Available)
{
    private readonly long[] counters = new long[Enum.GetValues<CacheObservationKind>().Length];
    private readonly long[] active = new long[Enum.GetValues<CacheActiveKind>().Length];
    private readonly DurationSamples[] durations = new DurationSamples[Enum.GetValues<CacheLatencyKind>().Length];
    private readonly Lock durationGate = new();
    private readonly MetricTag[] tags = [new("cache.collection", collection ?? "[provider]"), new("cache.provider", provider), new("cache.strategy", strategy)];
    private readonly DateTimeOffset started = timeProvider.GetUtcNow();
    private readonly Lock keyHistoryGate = new();
    private readonly CacheKeyObservation[] keyHistory = options.DetailedKeyHistory && options.Enabled && availability == CacheObservationAvailability.Available ? new CacheKeyObservation[options.LatencySampleCapacity] : null;
    private readonly byte[] fingerprintSecret = options.DetailedKeyHistory && options.Enabled && availability == CacheObservationAvailability.Available ? RandomNumberGenerator.GetBytes(32) : null;
    private int keyPosition;
    private int keyCount;

    /// <summary>Gets whether recording is supported. A disabled slot has a synchronous allocation-free forwarding path.</summary>
    public bool Enabled { get; } = options.Enabled && availability == CacheObservationAvailability.Available;

    /// <summary>Gets optional lifecycle-based representation capacity accounting; disabled observations retain no counters.</summary>
    public CacheHotFootprint HotFootprint { get; } = options.Enabled && availability == CacheObservationAvailability.Available ? new() : null;

    /// <summary>Records one bounded execution fact without retaining an identity, key, payload or request principal.</summary>
    /// <param name="kind">The controlled event family.</param>
    /// <param name="value">The nonnegative event or successfully processed entry count.</param>
    public void Record(CacheObservationKind kind, long value = 1)
    {
        if (!this.Enabled || value < 0 || (uint)kind >= (uint)this.counters.Length) { return; }

        AddSaturated(ref this.counters[(int)kind], value);
        metrics.Record(kind, value, this.tags);
    }

    /// <summary>Records an explicitly enabled public key operation using a process-local keyed fingerprint.</summary>
    /// <remarks>Raw keys never enter observation state, metric labels or trace tags. Oversized invalid keys are omitted before hashing.</remarks>
    /// <param name="key">The exact logical key, retained only as a keyed SHA-256 fingerprint.</param>
    /// <param name="operation">The controlled public operation category.</param>
    /// <example><code>slot.RecordKey("product:42", CacheOperationKind.Get);</code></example>
    public void RecordKey(string key, CacheOperationKind operation)
    {
        if (this.keyHistory is null || key is null || key.Length > 4096 || !Enum.IsDefined(operation)) { return; }

        try
        {
            Span<byte> encoded = stackalloc byte[4096];
            if (!Encoding.UTF8.TryGetBytes(key.AsSpan(), encoded, out var written)) { return; }

            Span<byte> digest = stackalloc byte[32];
            HMACSHA256.HashData(this.fingerprintSecret, encoded[..written], digest);
            var observation = new CacheKeyObservation(timeProvider.GetUtcNow(), operation, Convert.ToHexStringLower(digest));
            lock (this.keyHistoryGate)
            {
                this.keyHistory[this.keyPosition] = observation;
                this.keyPosition = (this.keyPosition + 1) % this.keyHistory.Length;
                this.keyCount = Math.Min(this.keyCount + 1, this.keyHistory.Length);
            }
        }
        catch { /* Explicit diagnostic detail cannot reject an actual cache operation. */ }
    }

    /// <summary>Reads bounded recent key-operation fingerprints without accessing provider metadata or payloads.</summary>
    /// <param name="maximum">The positive returned event limit, at most 4,096.</param>
    /// <returns>Actual newest events within configured retention, or an empty list when detailed history is disabled.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The requested result bound is outside one through 4,096.</exception>
    /// <example><code>var recent = slot.GetKeyHistory(32);</code></example>
    public IReadOnlyList<CacheKeyObservation> GetKeyHistory(int maximum = 128)
    {
        if (maximum is <= 0 or > 4096) { throw new ArgumentOutOfRangeException(nameof(maximum)); }

        if (this.keyHistory is null) { return []; }

        var cutoff = timeProvider.GetUtcNow() - options.HistoryRetention;
        lock (this.keyHistoryGate)
        {
            var result = new List<CacheKeyObservation>(Math.Min(maximum, this.keyCount));
            for (var index = 0; index < this.keyCount && result.Count < maximum; index++)
            {
                var position = (this.keyPosition - 1 - index + this.keyHistory.Length) % this.keyHistory.Length;
                var observation = this.keyHistory[position];
                if (observation.At >= cutoff) { result.Add(observation); }
            }

            return result;
        }
    }

    /// <summary>Adjusts current process-local work without creating per-owner or per-waiter label state.</summary>
    /// <param name="kind">The controlled active-work category.</param>
    /// <param name="delta">The admitted or completed work count.</param>
    public void ChangeActive(CacheActiveKind kind, int delta)
    {
        if (!this.Enabled || (uint)kind >= (uint)this.active.Length) { return; }

        var index = (int)kind;
        while (true)
        {
            var before = Interlocked.Read(ref this.active[index]);
            var after = delta >= 0 ? Math.Min(long.MaxValue - delta, before) + delta : Math.Max(0, before + delta);
            if (Interlocked.CompareExchange(ref this.active[index], after, before) == before) { return; }
        }
    }

    /// <summary>Retains a finite actual duration sample and emits the corresponding standard histogram measurement.</summary>
    /// <param name="kind">The controlled latency family.</param>
    /// <param name="elapsed">The measured nonnegative duration.</param>
    public void RecordDuration(CacheLatencyKind kind, TimeSpan elapsed)
    {
        if (!this.Enabled || elapsed < TimeSpan.Zero || (uint)kind >= (uint)this.durations.Length) { return; }

        try
        {
            var now = timeProvider.GetUtcNow();
            lock (this.durationGate)
            {
                this.durations[(int)kind] ??= new(options.LatencySampleCapacity);
                this.durations[(int)kind].Record(now, elapsed.TotalSeconds);
            }

            metrics.RecordDuration(kind, elapsed.TotalSeconds, this.tags);
        }
        catch { /* Optional duration sampling cannot replace a cache result. */ }
    }

    /// <summary>Creates a bounded immutable observation snapshot; unavailable counters and quantiles remain null.</summary>
    /// <returns>Node-local observations with actual sample timestamps and accuracy.</returns>
    public CacheObservationSnapshot Snapshot()
    {
        var now = timeProvider.GetUtcNow();
        var current = this.Enabled ? CacheObservationAvailability.Available : options.Enabled ? availability : CacheObservationAvailability.Disabled;
        var counts = Enum.GetValues<CacheObservationKind>().ToDictionary(kind => kind, kind => this.Enabled ? (long?)Interlocked.Read(ref this.counters[(int)kind]) : null);
        var occupied = Enum.GetValues<CacheActiveKind>().ToDictionary(kind => kind, kind => this.Enabled ? (long?)Interlocked.Read(ref this.active[(int)kind]) : null);
        Dictionary<CacheLatencyKind, CacheLatencySnapshot> latencies;
        lock (this.durationGate)
        {
            latencies = Enum.GetValues<CacheLatencyKind>().ToDictionary(kind => kind, kind => this.Enabled && this.durations[(int)kind] is { } samples ?
                samples.Snapshot(now, options.LatencySampleWindow) : Empty(this.Enabled ? CacheObservationAvailability.NoSamples : current));
        }

        return new(collection, provider, "NodeLocal", current, this.started, now, counts, occupied, latencies, this.HotFootprint?.Snapshot());
    }

    private static CacheLatencySnapshot Empty(CacheObservationAvailability availability) => new(availability, CacheObservationAccuracy.Unavailable, 0, 0, null, null, null, null, null);

    private static void AddSaturated(ref long location, long value)
    {
        while (true)
        {
            var before = Interlocked.Read(ref location);
            var after = before > long.MaxValue - value ? long.MaxValue : before + value;
            if (Interlocked.CompareExchange(ref location, after, before) == before) { return; }
        }
    }

    private sealed class DurationSamples(int capacity)
    {
        private readonly BoundedTimeSeries<(DateTimeOffset At, double Seconds)> samples = new(capacity, sample => sample.At);
        private long observed;

        /// <summary>Appends an actual finite sample and evicts the oldest retained sample when the ring is full.</summary>
        public void Record(DateTimeOffset at, double seconds)
        {
            this.samples.Add((at, seconds));
            this.observed = this.observed == long.MaxValue ? long.MaxValue : this.observed + 1;
        }

        /// <summary>Computes nearest-rank quantiles only from actual samples still inside the requested observation window.</summary>
        public CacheLatencySnapshot Snapshot(DateTimeOffset now, TimeSpan window)
        {
            var values = this.samples.Query(now - window, now, this.samples.Capacity);
            if (values.Count == 0) { return Empty(CacheObservationAvailability.NoSamples) with { SamplesObserved = this.observed }; }

            var sorted = values.Select(sample => sample.Seconds).OrderBy(value => value).ToArray();
            return new(CacheObservationAvailability.Available, CacheObservationAccuracy.Sampled, this.observed, sorted.Length,
                values.Min(sample => sample.At), values.Max(sample => sample.At), SampleStatistics.NearestRank(sorted, 0.5),
                SampleStatistics.NearestRank(sorted, 0.95), SampleStatistics.NearestRank(sorted, 0.99));
        }
    }
}