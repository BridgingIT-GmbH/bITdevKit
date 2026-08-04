namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;

public class MetricsServiceTests
{
    [Fact]
    public void Track_WhenDisposed_EmitsTotalCurrentAndDurationMetrics()
    {
        using var meterFactory = new TestMeterFactory();
        using var recorder = new MetricsRecorder();
        var sut = new MetricsService(meterFactory);

        using (sut.Track("inventory_refresh", "warehouse_a"))
        {
            recorder.CounterSum("inventory_refresh_warehouse_a").ShouldBe(1);
            recorder.CounterSum("inventory_refresh_warehouse_a_current").ShouldBe(1);
        }

        recorder.CounterSum("inventory_refresh_warehouse_a_current").ShouldBe(0);
        recorder.HistogramCount("inventory_refresh_warehouse_a_duration").ShouldBe(1);
    }

    [Fact]
    public void IncrementFailure_WhenCalled_EmitsFailureMetric()
    {
        using var meterFactory = new TestMeterFactory();
        using var recorder = new MetricsRecorder();
        var sut = new MetricsService(meterFactory);

        sut.IncrementFailure("inventory_refresh", "warehouse_a");

        recorder.CounterSum("inventory_refresh_warehouse_a_failure").ShouldBe(1);
    }

    [Fact]
    public void AddCounter_WithValueAndTags_EmitsTaggedLongCounter()
    {
        using var meterFactory = new TestMeterFactory();
        using var recorder = new MetricsRecorder();
        var sut = new MetricsService(meterFactory);
        MetricTag[] tags =
        [
            new("storage.operation", "upload"),
            new("storage.outcome", "success"),
        ];

        sut.AddCounter("storage_operations", 7, tags);

        recorder.CounterSum("storage_operations").ShouldBe(7);
        recorder.LastTags("storage_operations")["storage.operation"].ShouldBe("upload");
        recorder.LastTags("storage_operations")["storage.outcome"].ShouldBe("success");
    }

    [Fact]
    public void AddUpDownCounter_WithPositiveAndNegativeValues_ReturnsToZero()
    {
        using var meterFactory = new TestMeterFactory();
        using var recorder = new MetricsRecorder();
        var sut = new MetricsService(meterFactory);

        sut.AddUpDownCounter("operations_current", 3);
        sut.AddUpDownCounter("operations_current", -3);

        recorder.CounterSum("operations_current").ShouldBe(0);
        recorder.PublicationCount("operations_current").ShouldBe(1);
    }

    [Fact]
    public void RecordHistogram_WithLongValue_PreservesValueUnitAndTags()
    {
        using var meterFactory = new TestMeterFactory();
        using var recorder = new MetricsRecorder();
        var sut = new MetricsService(meterFactory);
        MetricTag[] tags = [new("store", "reports")];

        sut.RecordHistogram("storage_bytes", 4096L, "By", tags);

        recorder.LongHistogramValues("storage_bytes").ShouldBe([4096L]);
        recorder.Unit("storage_bytes").ShouldBe("By");
        recorder.LastTags("storage_bytes")["store"].ShouldBe("reports");
    }

    [Fact]
    public void RecordHistogram_WithDoubleValue_PreservesValueAndUnit()
    {
        using var meterFactory = new TestMeterFactory();
        using var recorder = new MetricsRecorder();
        var sut = new MetricsService(meterFactory);

        sut.RecordHistogram("operation_duration", 12.5D, "ms");

        recorder.DoubleHistogramValues("operation_duration").ShouldBe([12.5D]);
        recorder.Unit("operation_duration").ShouldBe("ms");
    }

    [Fact]
    public void RecordHistogramDuration_WithTimestamp_EmitsMilliseconds()
    {
        using var meterFactory = new TestMeterFactory();
        using var recorder = new MetricsRecorder();
        var sut = new MetricsService(meterFactory);
        var started = sut.StartTimestamp();

        sut.RecordHistogramDuration("operation_duration", started);

        recorder.HistogramCount("operation_duration").ShouldBe(1);
        recorder.DoubleHistogramValues("operation_duration").Single().ShouldBeGreaterThanOrEqualTo(0D);
        recorder.Unit("operation_duration").ShouldBe("ms");
    }

    [Fact]
    public void AddCounter_WhenCalledConcurrently_ReusesOneInstrument()
    {
        using var meterFactory = new TestMeterFactory();
        using var recorder = new MetricsRecorder();
        var sut = new MetricsService(meterFactory);

        Parallel.For(0, 1000, _ => sut.AddCounter("concurrent_operations"));

        recorder.CounterSum("concurrent_operations").ShouldBe(1000);
        recorder.PublicationCount("concurrent_operations").ShouldBe(1);
    }

    [Fact]
    public void RecordHistogram_WhenNameIsAlreadyCounter_IgnoresConflictingInstrument()
    {
        using var meterFactory = new TestMeterFactory();
        using var recorder = new MetricsRecorder();
        var sut = new MetricsService(meterFactory);

        sut.AddCounter("conflicting_series");
        sut.RecordHistogram("conflicting_series", 42D, "ms");

        recorder.CounterSum("conflicting_series").ShouldBe(1);
        recorder.HistogramCount("conflicting_series").ShouldBe(0);
        recorder.PublicationCount("conflicting_series").ShouldBe(1);
    }

    [Fact]
    public void AddCounter_WhenListenerThrows_DoesNotThrow()
    {
        using var meterFactory = new TestMeterFactory();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, currentListener) =>
        {
            if (instrument.Meter.Name == Metrics.MeterName)
            {
                currentListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, _, _, _) => throw new InvalidOperationException("listener failure"));
        listener.Start();
        var sut = new MetricsService(meterFactory);

        var exception = Record.Exception(() => sut.AddCounter("listener_failure"));

        exception.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WhenMeterFactoryThrows_CreatesNoOpService()
    {
        var sut = new MetricsService(new ThrowingMeterFactory());

        var exception = Record.Exception(() =>
        {
            sut.AddCounter("ignored");
            sut.AddUpDownCounter("ignored_current", 1);
            sut.RecordHistogram("ignored_duration", 1D, "ms");
        });

        exception.ShouldBeNull();
    }

    [Fact]
    public void Track_WhenDisposedTwice_EmitsCompletionOnce()
    {
        using var meterFactory = new TestMeterFactory();
        using var recorder = new MetricsRecorder();
        var sut = new MetricsService(meterFactory);
        var scope = sut.Track("idempotent_operation");

        scope.Dispose();
        scope.Dispose();

        recorder.CounterSum("idempotent_operation").ShouldBe(1);
        recorder.CounterSum("idempotent_operation_current").ShouldBe(0);
        recorder.HistogramCount("idempotent_operation_duration").ShouldBe(1);
    }

    [Fact]
    public void SetGauge_WhenValueChanges_ExposesLatestValueFromOneInstrument()
    {
        using var meterFactory = new TestMeterFactory();
        using var recorder = new MetricsRecorder();
        var sut = new MetricsService(meterFactory);

        sut.SetGauge("queue_depth", 1);
        sut.SetGauge("queue_depth", 4);
        recorder.CollectObservableInstruments();

        recorder.CounterValues("queue_depth").ShouldBe([4L]);
        recorder.PublicationCount("queue_depth").ShouldBe(1);
    }

    [Fact]
    public void AddCounter_WithBlankTagName_IgnoresInvalidTag()
    {
        using var meterFactory = new TestMeterFactory();
        using var recorder = new MetricsRecorder();
        var sut = new MetricsService(meterFactory);
        MetricTag[] tags = [new(" ", "ignored"), new("operation", "read")];

        sut.AddCounter("tag_validation", tags: tags);

        recorder.LastTags("tag_validation").ShouldBe(
            new Dictionary<string, object> { ["operation"] = "read" });
    }

    [Fact]
    public void Dispose_WhenCalledRepeatedly_MakesOwnedServiceNoOp()
    {
        using var recorder = new MetricsRecorder();
        var sut = new MetricsService();

        sut.AddCounter("before_dispose");
        sut.Dispose();
        sut.Dispose();

        var exception = Record.Exception(() => sut.AddCounter("after_dispose"));

        exception.ShouldBeNull();
        recorder.CounterSum("before_dispose").ShouldBe(1);
        recorder.CounterSum("after_dispose").ShouldBe(0);
    }

    [Fact]
    public void AddMetrics_WhenEnabled_RegistersSingletonMetricsService()
    {
        var services = new ServiceCollection();
        MetricsServiceCollectionExtensions.AddMetrics(services, options => options.Enabled());
        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<IMetricsService>();
        var second = provider.GetRequiredService<IMetricsService>();

        first.ShouldBeOfType<MetricsService>();
        second.ShouldBeSameAs(first);
    }

    [Fact]
    public void AddMetrics_WhenDisabled_DoesNotRegisterMetricsService()
    {
        var services = new ServiceCollection();
        MetricsServiceCollectionExtensions.AddMetrics(services, options => options.Enabled(false));
        using var provider = services.BuildServiceProvider();

        provider.GetService<IMetricsService>().ShouldBeNull();
    }

    private sealed class ThrowingMeterFactory : IMeterFactory
    {
        public Meter Create(MeterOptions options) => throw new InvalidOperationException("factory failure");

        public void Dispose()
        {
        }
    }
}
