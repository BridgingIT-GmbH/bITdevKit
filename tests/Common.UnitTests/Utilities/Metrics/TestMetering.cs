namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

internal sealed class TestMeterFactory : IMeterFactory, IDisposable
{
    private readonly List<Meter> meters = [];

    public Meter Create(MeterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return this.Create(options.Name, options.Version, options.Tags);
    }

    public Meter Create(string name, string version = null, IEnumerable<KeyValuePair<string, object>> tags = null)
    {
        var meter = new Meter(name, version, tags);
        lock (this.meters)
        {
            this.meters.Add(meter);
        }

        return meter;
    }

    public void Dispose()
    {
        lock (this.meters)
        {
            foreach (var meter in this.meters)
            {
                meter.Dispose();
            }

            this.meters.Clear();
        }
    }
}

internal sealed class MetricsRecorder : IDisposable
{
    private readonly MeterListener listener = new();
    private readonly ConcurrentDictionary<string, ConcurrentBag<long>> counters = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ConcurrentBag<long>> longHistograms = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ConcurrentBag<double>> doubleHistograms = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ConcurrentBag<IReadOnlyDictionary<string, object>>> tags = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> units = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int> publications = new(StringComparer.Ordinal);

    public MetricsRecorder()
    {
        this.listener.InstrumentPublished = (instrument, listener) =>
        {
            if (string.Equals(instrument.Meter.Name, Metrics.MeterName, StringComparison.Ordinal))
            {
                this.publications.AddOrUpdate(instrument.Name, 1, (_, count) => count + 1);
                this.units[instrument.Name] = instrument.Unit ?? string.Empty;
                listener.EnableMeasurementEvents(instrument);
            }
        };

        this.listener.SetMeasurementEventCallback<int>((instrument, measurement, tags, state) =>
        {
            this.counters.GetOrAdd(instrument.Name, _ => []).Add(measurement);
            this.RecordTags(instrument.Name, tags);
        });

        this.listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            if (instrument is Histogram<long>)
            {
                this.longHistograms.GetOrAdd(instrument.Name, _ => []).Add(measurement);
            }
            else
            {
                this.counters.GetOrAdd(instrument.Name, _ => []).Add(measurement);
            }

            this.RecordTags(instrument.Name, tags);
        });

        this.listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
        {
            this.doubleHistograms.GetOrAdd(instrument.Name, _ => []).Add(measurement);
            this.RecordTags(instrument.Name, tags);
        });

        this.listener.Start();
    }

    public long CounterSum(string series)
    {
        return this.counters.TryGetValue(series, out var values) ? values.Sum() : 0;
    }

    public IReadOnlyCollection<long> CounterValues(string series)
    {
        return this.counters.TryGetValue(series, out var values) ? values.ToArray() : [];
    }

    public int HistogramCount(string series)
    {
        return this.longHistograms.TryGetValue(series, out var longValues)
            ? longValues.Count
            : this.doubleHistograms.TryGetValue(series, out var doubleValues)
                ? doubleValues.Count
                : 0;
    }

    public IReadOnlyCollection<long> LongHistogramValues(string series)
    {
        return this.longHistograms.TryGetValue(series, out var values) ? values.ToArray() : [];
    }

    public IReadOnlyCollection<double> DoubleHistogramValues(string series)
    {
        return this.doubleHistograms.TryGetValue(series, out var values) ? values.ToArray() : [];
    }

    public IReadOnlyDictionary<string, object> LastTags(string series)
    {
        return this.tags.TryGetValue(series, out var values)
            ? values.LastOrDefault() ?? new Dictionary<string, object>()
            : new Dictionary<string, object>();
    }

    public string Unit(string series)
    {
        return this.units.GetValueOrDefault(series);
    }

    public int PublicationCount(string series)
    {
        return this.publications.GetValueOrDefault(series);
    }

    public void CollectObservableInstruments()
    {
        this.listener.RecordObservableInstruments();
    }

    public void Dispose()
    {
        this.listener.Dispose();
    }

    private void RecordTags(string series, ReadOnlySpan<KeyValuePair<string, object>> tags)
    {
        this.tags.GetOrAdd(series, _ => []).Add(tags.ToArray().ToDictionary(x => x.Key, x => x.Value));
    }
}
