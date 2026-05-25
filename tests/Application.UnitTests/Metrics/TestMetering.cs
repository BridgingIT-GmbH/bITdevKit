namespace BridgingIT.DevKit.Application.UnitTests;

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
    private readonly ConcurrentDictionary<string, ConcurrentBag<double>> histograms = new(StringComparer.Ordinal);

    public MetricsRecorder()
    {
        this.listener.InstrumentPublished = (instrument, listener) =>
        {
            if (string.Equals(instrument.Meter.Name, Metrics.MeterName, StringComparison.Ordinal))
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        this.listener.SetMeasurementEventCallback<int>((instrument, measurement, tags, state) =>
        {
            this.counters.GetOrAdd(instrument.Name, _ => []).Add(measurement);
        });

        this.listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
        {
            this.histograms.GetOrAdd(instrument.Name, _ => []).Add(measurement);
        });

        this.listener.Start();
    }

    public long CounterSum(string series)
    {
        return this.counters.TryGetValue(series, out var values) ? values.Sum() : 0;
    }

    public int HistogramCount(string series)
    {
        return this.histograms.TryGetValue(series, out var values) ? values.Count : 0;
    }

    public void Dispose()
    {
        this.listener.Dispose();
    }
}