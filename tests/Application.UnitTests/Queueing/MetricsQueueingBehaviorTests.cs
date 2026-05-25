namespace BridgingIT.DevKit.Application.UnitTests.Queueing;

using BridgingIT.DevKit.Application.Queueing;

public class MetricsQueueingBehaviorTests
{
    [Fact]
    public async Task Enqueue_WhenMessageSucceeds_EmitsEnqueueMetrics()
    {
        using var meterFactory = new TestMeterFactory();
        using var recorder = new MetricsRecorder();
        var sut = new MetricsQueueEnqueuerBehavior(meterFactory);

        await sut.Enqueue(new TestQueueMessage("hello"), CancellationToken.None, () => Task.CompletedTask);

        recorder.CounterSum("queueing_enqueue").ShouldBe(1);
        recorder.CounterSum("queueing_enqueue_testqueuemessage").ShouldBe(1);
        recorder.CounterSum("queueing_enqueue_current").ShouldBe(0);
        recorder.CounterSum("queueing_enqueue_testqueuemessage_current").ShouldBe(0);
        recorder.HistogramCount("queueing_enqueue_duration").ShouldBe(1);
        recorder.HistogramCount("queueing_enqueue_testqueuemessage_duration").ShouldBe(1);
    }

    [Fact]
    public async Task Handle_WhenHandlerThrows_EmitsFailureMetrics()
    {
        using var meterFactory = new TestMeterFactory();
        using var recorder = new MetricsRecorder();
        var sut = new MetricsQueueHandlerBehavior(meterFactory);

        await Should.ThrowAsync<InvalidOperationException>(() => sut.Handle(
            new TestQueueMessage("hello"),
            CancellationToken.None,
            new object(),
            () => throw new InvalidOperationException("boom")));

        recorder.CounterSum("queueing_handle").ShouldBe(1);
        recorder.CounterSum("queueing_handle_testqueuemessage").ShouldBe(1);
        recorder.CounterSum("queueing_handle_failure").ShouldBe(1);
        recorder.CounterSum("queueing_handle_testqueuemessage_failure").ShouldBe(1);
        recorder.CounterSum("queueing_handle_current").ShouldBe(0);
        recorder.CounterSum("queueing_handle_testqueuemessage_current").ShouldBe(0);
        recorder.HistogramCount("queueing_handle_duration").ShouldBe(1);
        recorder.HistogramCount("queueing_handle_testqueuemessage_duration").ShouldBe(1);
    }

    private sealed class TestQueueMessage(string value) : QueueMessageBase
    {
        public string Value { get; } = value;
    }
}