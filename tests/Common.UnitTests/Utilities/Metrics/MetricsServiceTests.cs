namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

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
}