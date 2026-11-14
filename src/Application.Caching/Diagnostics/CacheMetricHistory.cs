// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using System.Collections.Frozen;

/// <summary>Retains a finite optional node-local series history without provider reads or payload enumeration.</summary>
/// <remarks>Counter downsampling uses duration-weighted rates. Sampled quantiles retain their latest actual value; they are never averaged into new quantiles.</remarks>
/// <example><code>var result = await history.QueryAsync(new("cards"), token);</code></example>
public sealed class CacheMetricHistory : IDisposable
{
    private static readonly CacheObservationKind[] DefaultCounters = [CacheObservationKind.Request, CacheObservationKind.PositiveHit,
        CacheObservationKind.NegativeHit, CacheObservationKind.Miss, CacheObservationKind.ReadSuccess, CacheObservationKind.WriteSuccess,
        CacheObservationKind.InvalidatedEntries, CacheObservationKind.SourceAttempt, CacheObservationKind.Join, CacheObservationKind.ProviderFailure,
        CacheObservationKind.HotBinaryReuse, CacheObservationKind.HotDecodedReuse, CacheObservationKind.TransferCompleted, CacheObservationKind.TransferFailed, CacheObservationKind.SourceFailure, CacheObservationKind.WaitTimeout,
        CacheObservationKind.SourceTimeout, CacheObservationKind.HttpDestinationFailed, CacheObservationKind.BreakerOpened, CacheObservationKind.BreakerRejected];
    private readonly Lock gate = new();
    private readonly CacheCatalog catalog;
    private readonly CacheDiagnostics diagnostics;
    private readonly CacheRuntimeResources resources;
    private readonly CacheDiagnosticQueryGate queryGate;
    private readonly TimeProvider time;
    private readonly FrozenDictionary<string, SeriesState> series;
    private readonly ResourceReservation reservation;
    private readonly CacheObservationAvailability availability;
    private DateTimeOffset lastSample;
    private bool disposed;

    /// <summary>Declares bounded series and reserves optional storage before allocating sample rings.</summary>
    /// <param name="catalog">The immutable validated configuration.</param>
    /// <param name="diagnostics">The intrinsic process-local observations.</param>
    /// <param name="resources">The existing resource hierarchy.</param>
    /// <param name="queryGate">The shared bounded chart-query allowance.</param>
    /// <param name="timeProvider">The clock used for actual sample windows.</param>
    public CacheMetricHistory(CacheCatalog catalog, CacheDiagnostics diagnostics, CacheRuntimeResources resources,
        CacheDiagnosticQueryGate queryGate, TimeProvider timeProvider)
    {
        this.catalog = catalog;
        this.diagnostics = diagnostics;
        this.resources = resources;
        this.queryGate = queryGate;
        this.time = timeProvider;
        var options = catalog.Options.Diagnostics;
        var declared = new List<SeriesDefinition>();
        AddDefinitions(null, declared, options.MaximumHistorySeries);
        foreach (var collection in catalog.Collections.Keys.OrderBy(name => name, StringComparer.Ordinal))
        {
            if (declared.Count == options.MaximumHistorySeries) { break; }

            AddDefinitions(collection, declared, options.MaximumHistorySeries);
        }

        var chosen = declared.ToArray();
        var capacity = (int)Math.Ceiling(options.HistoryRetention / options.SamplingInterval) + 2;
        var bytes = checked(chosen.Length * (2048L + capacity * 48L));
        this.availability = !options.Enabled || !options.HistoryEnabled ? CacheObservationAvailability.Disabled :
            resources.Resident.TryReserve(bytes, out this.reservation) ? CacheObservationAvailability.Available : CacheObservationAvailability.Capacity;
        this.lastSample = this.time.GetUtcNow();
        this.series = chosen.ToFrozenDictionary(definition => definition.Descriptor.Id,
            definition => new SeriesState(definition, this.availability == CacheObservationAvailability.Available ? capacity : 0, this.lastSample), StringComparer.Ordinal);
    }

    /// <summary>Gets whether the optional sampler has admitted storage and is enabled.</summary>
    public bool Enabled => this.availability == CacheObservationAvailability.Available && !this.disposed;

    /// <summary>Lists actual configured series for a selected authorized scope.</summary>
    /// <param name="collection">A declared collection alias, or null for node aggregates.</param>
    /// <returns>Only the finite predeclared descriptors retained by this process.</returns>
    public IReadOnlyList<CacheMetricSeriesDescriptor> GetSeries(string collection = null)
    {
        var name = collection is null ? null : CacheIdentityNormalizer.Name(collection);
        return this.series.Values.Where(state => state.Definition.Descriptor.Collection == name).Select(state => state.Definition.Descriptor).ToArray();
    }

    /// <summary>Samples intrinsic observations once per configured interval without native provider access.</summary>
    /// <remarks>Calls closer than the configured interval are ignored. The host sampler owns scheduling.</remarks>
    public void Sample()
    {
        if (!this.Enabled) { return; }

        var now = this.time.GetUtcNow();
        lock (this.gate)
        {
            if (this.disposed || now - this.lastSample < this.catalog.Options.Diagnostics.SamplingInterval) { return; }

            var snapshots = this.diagnostics.SnapshotCollections();
            foreach (var state in this.series.Values) { state.Record(snapshots, this.resources, now); }

            this.lastSample = now;
        }
    }

    /// <summary>Returns an actual bounded history window, preserving missing sources and unavailable quantiles.</summary>
    /// <param name="query">The authorized scope, declared series and finite display limits.</param>
    /// <param name="cancellationToken">The query cancellation token.</param>
    /// <returns>A bounded history or an explicit validation/admission failure.</returns>
    public ValueTask<Result<CacheMetricHistoryResult>> QueryAsync(CacheMetricHistoryQuery query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(query);
        var options = this.catalog.Options.Diagnostics;
        var window = query.Window ?? options.ChartWindow;
        if (window <= TimeSpan.Zero || window > options.HistoryRetention || query.MaximumPoints <= 0 ||
            query.MaximumPoints > options.MaximumChartPoints || query.Series?.Count > 16 || query.Series?.Any(string.IsNullOrWhiteSpace) == true)
        {
            return Failure("The history window, series or returned-point limit exceeds its configured bound.");
        }

        string collection;
        try { collection = query.Collection is null ? null : CacheIdentityNormalizer.Name(query.Collection); }
        catch (ArgumentException) { return Failure("The diagnostic collection alias is invalid."); }

        if (collection is not null && !this.catalog.Collections.ContainsKey(collection)) { return Failure("The diagnostic collection was not declared."); }

        if (!this.queryGate.TryEnter(out var queryLease))
        {
            return ValueTask.FromResult(Result<CacheMetricHistoryResult>.Failure(new CacheError(CacheErrorCode.Capacity, "The bounded chart-query allowance is occupied.")));
        }

        using (queryLease)
        {
            var selected = query.Series is null ? this.series.Values.Where(state => state.Definition.Descriptor.Collection == collection).Take(16).ToArray() :
                query.Series.Distinct(StringComparer.Ordinal).Select(id => this.series.GetValueOrDefault(id)).ToArray();
            if (selected.Any(state => state is null || state.Definition.Descriptor.Collection != collection)) { return Failure("A chart series is not configured for the authorized scope."); }

            var bytes = checked(Math.Max(1, selected.Length) * (4096L + options.MaximumChartPoints * 96L));
            if (!this.resources.Transient.TryReserve(bytes, out var memory))
            {
                return ValueTask.FromResult(Result<CacheMetricHistoryResult>.Failure(new CacheError(CacheErrorCode.Capacity, "The bounded chart-query memory allowance is exhausted.")));
            }

            using (memory)
            {
                var now = this.time.GetUtcNow();
                lock (this.gate)
                {
                    var results = selected.Select(state =>
                    {
                        var history = state.Query(now - window, now, query.MaximumPoints);
                        var status = this.availability != CacheObservationAvailability.Available ? this.availability :
                            history.Count == 0 ? CacheObservationAvailability.NoSamples : history.Any(point => point.Value.HasValue) ?
                                CacheObservationAvailability.Available : history[^1].Availability;
                        return new CacheMetricSeriesResult(state.Definition.Descriptor, history, status,
                            status == CacheObservationAvailability.Available ? null : "History" + status);
                    }).ToArray();
                    var points = results.SelectMany(result => result.Points).ToArray();
                    var availability = this.availability != CacheObservationAvailability.Available ? this.availability : selected.Length == 0 ?
                        CacheObservationAvailability.Capacity : points.Length == 0 ? CacheObservationAvailability.NoSamples : CacheObservationAvailability.Available;
                    return ValueTask.FromResult(Result<CacheMetricHistoryResult>.Success(new(availability, now,
                        points.Length == 0 ? null : points.Min(point => point.At), points.Length == 0 ? null : points.Max(point => point.At), "NodeLocal", results)));
                }
            }
        }
    }

    /// <summary>Stops new sampling and releases optional history storage charges.</summary>
    public void Dispose()
    {
        lock (this.gate)
        {
            if (this.disposed) { return; }

            this.disposed = true;
            this.reservation?.Dispose();
        }
    }

    private static void AddDefinitions(string collection, List<SeriesDefinition> definitions, int maximum)
    {
        var prefix = collection ?? "[node]";
        void Add(SeriesDefinition definition) { if (definitions.Count < maximum) { definitions.Add(definition); } }

        foreach (var metric in collection is null ? new[] { "ResidentBytes", "HotResidentBytes", "TransientBytes", "ProcessWorkingSetBytes", "HotBinaryBytes", "HotDecodedBytes", "HotPinnedBytes" } :
            new[] { "ResidentBytes", "HotResidentBytes", "HotBinaryBytes", "HotDecodedBytes", "HotPinnedBytes" })
        {
            Add(new(new(prefix + ":" + metric, collection, metric, "bytes", "Last", "NodeLocal", metric == "ProcessWorkingSetBytes" ? CacheObservationAccuracy.Sampled :
                metric is "HotDecodedBytes" or "HotPinnedBytes" ? CacheObservationAccuracy.Estimated : CacheObservationAccuracy.Exact), null, null, null, 0, metric));
        }

        Add(new(new(prefix + ":LookupHitRatio", collection, "LookupHitRatio", "%", "LastSinceProcessStart", "NodeLocal"), null, null, null, 0, "LookupHitRatio"));
        foreach (var counter in DefaultCounters)
        {
            var unit = counter is CacheObservationKind.ReadSuccess or CacheObservationKind.WriteSuccess or CacheObservationKind.InvalidatedEntries ? "entries/s" : "events/s";
            Add(new(new(prefix + ":" + counter, collection, counter.ToString(), unit, "DurationWeightedRate", "NodeLocal"), counter, null, null, 0));
        }

        foreach (var gauge in new[] { CacheActiveKind.Owners, CacheActiveKind.Waiters })
        {
            Add(new(new(prefix + ":Active" + gauge, collection, "Active" + gauge, "work", "Last", "NodeLocal"), null, gauge, null, 0));
        }

        foreach (var latency in Enum.GetValues<CacheLatencyKind>())
        {
            Add(new(new(prefix + ":" + latency + "P95", collection, latency + "P95", "s", "Last", "NodeLocal", CacheObservationAccuracy.Sampled), null, null, latency, 0.95));
        }

        Add(new(new(prefix + ":OperationP50", collection, "OperationP50", "s", "Last", "NodeLocal", CacheObservationAccuracy.Sampled), null, null, CacheLatencyKind.Operation, 0.5));
        Add(new(new(prefix + ":OperationP99", collection, "OperationP99", "s", "Last", "NodeLocal", CacheObservationAccuracy.Sampled), null, null, CacheLatencyKind.Operation, 0.99));
    }

    private static ValueTask<Result<CacheMetricHistoryResult>> Failure(string message) => ValueTask.FromResult(Result<CacheMetricHistoryResult>.Failure(new CacheError(CacheErrorCode.Validation, message)));

    private sealed record SeriesDefinition(CacheMetricSeriesDescriptor Descriptor, CacheObservationKind? Counter, CacheActiveKind? Gauge, CacheLatencyKind? Latency, double Quantile, string Special = null);

    private sealed class SeriesState(SeriesDefinition definition, int capacity, DateTimeOffset started)
    {
        private readonly BoundedTimeSeries<CacheMetricPoint> points = new(capacity, point => point.At);
        private double previous;
        private DateTimeOffset previousAt = started;

        /// <summary>Gets the finite startup-declared metric and aggregation contract for this ring.</summary>
        public SeriesDefinition Definition => definition;

        /// <summary>Appends one actual source observation, preserving missing quantiles and unsupported resource categories.</summary>
        public void Record(IReadOnlyList<CacheObservationSnapshot> all, CacheRuntimeResources resources, DateTimeOffset at)
        {
            var selected = definition.Descriptor.Collection is null ? all : all.Where(snapshot => snapshot.Collection == definition.Descriptor.Collection).ToArray();
            double? value = null;
            var availability = selected.Count == 0 ? CacheObservationAvailability.NoSamples : selected.Any(snapshot => snapshot.Availability != CacheObservationAvailability.Available) ?
                CacheObservationAvailability.Unavailable : CacheObservationAvailability.Available;
            var elapsed = (at - this.previousAt).TotalSeconds;
            if (definition.Special is { } special && special != "LookupHitRatio")
            {
                var collection = definition.Descriptor.Collection;
                value = special switch
                {
                    "ResidentBytes" => collection is null ? resources.Resident.Used : resources.ForCollection(collection).Resident.Used,
                    "HotResidentBytes" => collection is null ? resources.HotResident.Used : resources.ForCollection(collection).HotResident.Used,
                    "TransientBytes" when collection is null => resources.Transient.Used,
                    "ProcessWorkingSetBytes" when collection is null => Environment.WorkingSet,
                    "HotBinaryBytes" when selected.Count > 0 && selected.All(snapshot => snapshot.HotFootprint is not null) => selected.Sum(snapshot => snapshot.HotFootprint.BinaryBytes),
                    "HotDecodedBytes" when selected.Count > 0 && selected.All(snapshot => snapshot.HotFootprint is not null) => selected.Sum(snapshot => snapshot.HotFootprint.DecodedBytes),
                    "HotPinnedBytes" when selected.Count > 0 && selected.All(snapshot => snapshot.HotFootprint is not null) => selected.Sum(snapshot => snapshot.HotFootprint.PinnedBytes),
                    _ => null
                };
                availability = value.HasValue ? CacheObservationAvailability.Available : CacheObservationAvailability.Unavailable;
            }
            else if (availability == CacheObservationAvailability.Available && definition.Special == "LookupHitRatio")
            {
                var positive = selected.Sum(snapshot => (double)snapshot.Counters[CacheObservationKind.PositiveHit].GetValueOrDefault());
                var negative = selected.Sum(snapshot => (double)snapshot.Counters[CacheObservationKind.NegativeHit].GetValueOrDefault());
                var miss = selected.Sum(snapshot => (double)snapshot.Counters[CacheObservationKind.Miss].GetValueOrDefault());
                var total = positive + negative + miss;
                value = total > 0 ? 100 * ((positive + negative) / total) : null;
            }
            else if (availability == CacheObservationAvailability.Available && definition.Counter is { } counter)
            {
                var cumulative = selected.Sum(snapshot => (double)snapshot.Counters[counter].GetValueOrDefault());
                value = elapsed > 0 ? Math.Max(0, cumulative - this.previous) / elapsed : null;
                this.previous = cumulative;
            }
            else if (availability == CacheObservationAvailability.Available && definition.Gauge is { } gauge)
            {
                value = selected.Sum(snapshot => (double)snapshot.Active[gauge].GetValueOrDefault());
            }
            else if (availability == CacheObservationAvailability.Available && definition.Latency is { } latency)
            {
                // A node aggregate cannot be manufactured by averaging collection quantiles.
                if (selected.Count == 1)
                {
                    var sample = selected[0].Latencies[latency];
                    value = definition.Quantile == 0.5 ? sample.P50Seconds : definition.Quantile == 0.99 ? sample.P99Seconds : sample.P95Seconds;
                    availability = sample.Availability;
                }
                else { availability = CacheObservationAvailability.Unavailable; }
            }

            this.points.Add(new(at, value, value.HasValue ? availability : availability == CacheObservationAvailability.Available ? CacheObservationAvailability.NoSamples : availability, elapsed));
            this.previousAt = at;
        }

        /// <summary>Selects the real retained time window and applies the declared bounded downsampling rule.</summary>
        public IReadOnlyList<CacheMetricPoint> Query(DateTimeOffset from, DateTimeOffset to, int maximum) => this.points.Query(from, to, maximum, this.Aggregate);

        private CacheMetricPoint Aggregate(IReadOnlyList<CacheMetricPoint> values)
        {
            var last = values[^1];
            if (definition.Descriptor.Aggregation != "DurationWeightedRate") { return last; }

            var seconds = values.Sum(point => point.Seconds);
            if (values.Any(point => !point.Value.HasValue) || seconds <= 0) { return new(last.At, null, CacheObservationAvailability.Unavailable, seconds); }

            return new(last.At, values.Sum(point => point.Value.Value * point.Seconds) / seconds, CacheObservationAvailability.Available, seconds);
        }
    }
}