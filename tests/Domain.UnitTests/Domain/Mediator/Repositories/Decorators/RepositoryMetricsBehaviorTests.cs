namespace BridgingIT.DevKit.Domain.UnitTests.Domain.Mediator.Repositories.Decorators;

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using BridgingIT.DevKit.Common;
using DevKit.Domain.Repositories;

[UnitTest("Domain")]
public class RepositoryMetricsBehaviorTests
{
    [Fact]
    public async Task FindOneAsync_TracksReadTotalsCurrentAndDuration()
    {
        using var meterFactory = new TestMeterFactory();
        using var recorder = new MetricsRecorder();
        var inner = Substitute.For<IGenericRepository<PersonStub>>();
        var release = new TaskCompletionSource<PersonStub>(TaskCreationOptions.RunContinuationsAsynchronously);
        inner.FindOneAsync(Arg.Any<object>(), Arg.Any<IFindOptions<PersonStub>>(), Arg.Any<CancellationToken>())
            .Returns(_ => release.Task);
        var sut = new RepositoryMetricsBehavior<PersonStub>(meterFactory, inner);

        var task = sut.FindOneAsync(Guid.NewGuid(), cancellationToken: CancellationToken.None);
        await Task.Yield();

        recorder.CounterSum("repositories_read_current").ShouldBe(1);
        recorder.CounterSum("repositories_read_personstub_find_one_current").ShouldBe(1);

        release.SetResult(new PersonStub());
        await task;

        recorder.CounterSum("repositories_read").ShouldBe(1);
        recorder.CounterSum("repositories_read_personstub_find_one").ShouldBe(1);
        recorder.CounterSum("repositories_read_current").ShouldBe(0);
        recorder.CounterSum("repositories_read_personstub_find_one_current").ShouldBe(0);
        recorder.HistogramCount("repositories_read_duration").ShouldBe(1);
        recorder.HistogramCount("repositories_read_personstub_find_one_duration").ShouldBe(1);
    }

    [Fact]
    public async Task InsertAsync_WhenInnerThrows_TracksWriteFailureMetrics()
    {
        using var meterFactory = new TestMeterFactory();
        using var recorder = new MetricsRecorder();
        var inner = Substitute.For<IGenericRepository<PersonStub>>();
        inner.InsertAsync(Arg.Any<PersonStub>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<PersonStub>(new InvalidOperationException("boom")));
        var sut = new RepositoryMetricsBehavior<PersonStub>(meterFactory, inner);

        await Should.ThrowAsync<InvalidOperationException>(() => sut.InsertAsync(new PersonStub(), CancellationToken.None));

        recorder.CounterSum("repositories_write").ShouldBe(1);
        recorder.CounterSum("repositories_write_personstub_insert").ShouldBe(1);
        recorder.CounterSum("repositories_write_failure").ShouldBe(1);
        recorder.CounterSum("repositories_write_personstub_insert_failure").ShouldBe(1);
        recorder.CounterSum("repositories_write_current").ShouldBe(0);
        recorder.CounterSum("repositories_write_personstub_insert_current").ShouldBe(0);
    }

    private sealed class TestMeterFactory : IMeterFactory, IDisposable
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

    private sealed class MetricsRecorder : IDisposable
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
}