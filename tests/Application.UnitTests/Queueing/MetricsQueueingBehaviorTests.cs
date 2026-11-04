namespace BridgingIT.DevKit.Application.UnitTests.Queueing;

using BridgingIT.DevKit.Application.Queueing;

[Collection(ProcessWideStateTestCollection.Name)]
public class MetricsQueueingBehaviorTests
{
    [Fact]
    public async Task Enqueue_WhenMessageSucceeds_EmitsEnqueueMetrics()
    {
        using var meterFactory = new TestMeterFactory();
        using var recorder = new MetricsRecorder();
        var sut = new MetricsQueueEnqueuerBehavior(new MetricsService(meterFactory));

        await sut.Enqueue(new TestQueueMessage(), CancellationToken.None, () => Task.CompletedTask);

        recorder.CounterSum("queueing_enqueue").ShouldBe(1);
        recorder.CounterSum("queueing_enqueue.completed").ShouldBe(1);
        recorder.CounterSum("queueing_enqueue_testqueuemessage.completed").ShouldBe(1);
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
        var sut = new MetricsQueueHandlerBehavior(new MetricsService(meterFactory));

        await Should.ThrowAsync<InvalidOperationException>(() => sut.Handle(
            new TestQueueMessage(),
            CancellationToken.None,
            new object(),
            () => throw new InvalidOperationException("boom")));

        recorder.CounterSum("queueing_handle").ShouldBe(1);
        recorder.CounterSum("queueing_handle_testqueuemessage").ShouldBe(1);
        recorder.CounterSum("queueing_handle_failure").ShouldBe(1);
        recorder.CounterSum("queueing_handle.completed").ShouldBe(0);
        recorder.CounterSum("queueing_handle_testqueuemessage_failure").ShouldBe(1);
        recorder.CounterSum("queueing_handle_current").ShouldBe(0);
        recorder.CounterSum("queueing_handle_testqueuemessage_current").ShouldBe(0);
        recorder.HistogramCount("queueing_handle_duration").ShouldBe(1);
        recorder.HistogramCount("queueing_handle_testqueuemessage_duration").ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Enqueue_Outcome_RecordsSuccessOnlyAfterSuccessfulReturn(bool fails)
    {
        using var meterFactory = new TestMeterFactory();
        using var recorder = new MetricsRecorder();
        var sut = new MetricsQueueEnqueuerBehavior(new MetricsService(meterFactory));
        Task Run() => sut.Enqueue(new TestQueueMessage(), CancellationToken.None,
            () => fails ? Task.FromException(new InvalidOperationException("failure")) : Task.CompletedTask);
        if (fails) await Should.ThrowAsync<InvalidOperationException>(Run);
        else await Run();
        recorder.CounterSum("queueing_enqueue").ShouldBe(1);
        recorder.CounterSum("queueing_enqueue.completed").ShouldBe(fails ? 0 : 1);
        recorder.CounterSum("queueing_enqueue_failure").ShouldBe(fails ? 1 : 0);
        recorder.CounterSum("queueing_enqueue_current").ShouldBe(0);
        recorder.HistogramCount("queueing_enqueue_duration").ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Handle_FanOutOrCancellation_CountsHandlerCompletionsAccurately(bool cancel)
    {
        using var meterFactory = new TestMeterFactory();
        using var recorder = new MetricsRecorder();
        using var cancellation = new CancellationTokenSource();
        var sut = new MetricsQueueHandlerBehavior(new MetricsService(meterFactory));
        var message = new TestQueueMessage();
        if (cancel)
        {
            await Should.ThrowAsync<OperationCanceledException>(() => sut.Handle(message, cancellation.Token, new object(), () =>
            {
                cancellation.Cancel();
                return Task.FromCanceled(cancellation.Token);
            }));
        }
        else
        {
            await sut.Handle(message, CancellationToken.None, new object(), () => Task.CompletedTask);
            await sut.Handle(message, CancellationToken.None, new object(), () => Task.CompletedTask);
        }

        recorder.CounterSum("queueing_handle").ShouldBe(cancel ? 1 : 2);
        recorder.CounterSum("queueing_handle.completed").ShouldBe(cancel ? 0 : 2);
        recorder.CounterSum("queueing_handle_failure").ShouldBe(cancel ? 1 : 0);
        recorder.CounterSum("queueing_handle_current").ShouldBe(0);
        recorder.HistogramCount("queueing_handle_duration").ShouldBe(cancel ? 1 : 2);
    }

    private sealed class TestQueueMessage : QueueMessageBase;
}
