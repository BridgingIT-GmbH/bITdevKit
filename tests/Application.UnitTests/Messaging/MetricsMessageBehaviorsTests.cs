namespace BridgingIT.DevKit.Application.UnitTests.Messaging;

using BridgingIT.DevKit.Application.Messaging;
using Microsoft.Extensions.Logging.Abstractions;

[Collection(ProcessWideStateTestCollection.Name)]
public class MetricsMessageBehaviorsTests
{
    [Fact]
    public async Task Publish_WhenMetricsServiceIsMissing_RemainsPassThrough()
    {
        var sut = new MetricsMessagePublisherBehavior(NullLoggerFactory.Instance);
        var invoked = false;

        await sut.Publish(new TestMessage(), CancellationToken.None, () =>
        {
            invoked = true;
            return Task.CompletedTask;
        });

        invoked.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_WhenHandlerThrows_EmitsFailureMetrics()
    {
        using var meterFactory = new TestMeterFactory();
        using var recorder = new MetricsRecorder();
        var sut = new MetricsMessageHandlerBehavior(NullLoggerFactory.Instance, new MetricsService(meterFactory));

        await Should.ThrowAsync<InvalidOperationException>(() => sut.Handle(
            new TestMessage(),
            CancellationToken.None,
            new object(),
            () => throw new InvalidOperationException("boom")));

        recorder.CounterSum("messaging_handle").ShouldBe(1);
        recorder.CounterSum("messaging_handle_testmessage").ShouldBe(1);
        recorder.CounterSum("messaging_handle_failure").ShouldBe(1);
        recorder.CounterSum("messaging_handle.completed").ShouldBe(0);
        recorder.CounterSum("messaging_handle_testmessage_failure").ShouldBe(1);
        recorder.CounterSum("messaging_handle_current").ShouldBe(0);
        recorder.CounterSum("messaging_handle_testmessage_current").ShouldBe(0);
        recorder.HistogramCount("messaging_handle_duration").ShouldBe(1);
    }

    [Fact]
    public async Task Handle_WhileHandlerIsRunning_TracksCurrentMetrics()
    {
        using var meterFactory = new TestMeterFactory();
        using var recorder = new MetricsRecorder();
        var sut = new MetricsMessageHandlerBehavior(NullLoggerFactory.Instance, new MetricsService(meterFactory));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var task = sut.Handle(
            new TestMessage(),
            CancellationToken.None,
            new object(),
            async () =>
            {
                entered.SetResult();
                await release.Task;
            });

        await entered.Task;
        recorder.CounterSum("messaging_handle_current").ShouldBe(1);
        recorder.CounterSum("messaging_handle_testmessage_current").ShouldBe(1);
        recorder.CounterSum("messaging_handle.completed").ShouldBe(0);
        release.SetResult();
        await task;
        recorder.CounterSum("messaging_handle.completed").ShouldBe(1);
        recorder.CounterSum("messaging_handle_testmessage.completed").ShouldBe(1);

        recorder.CounterSum("messaging_handle_current").ShouldBe(0);
        recorder.CounterSum("messaging_handle_testmessage_current").ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Publish_Outcome_RecordsSuccessOnlyAfterSuccessfulReturn(bool fails)
    {
        using var meterFactory = new TestMeterFactory();
        using var recorder = new MetricsRecorder();
        var sut = new MetricsMessagePublisherBehavior(NullLoggerFactory.Instance, new MetricsService(meterFactory));
        Task Run() => sut.Publish(new TestMessage(), CancellationToken.None,
            () => fails ? Task.FromException(new InvalidOperationException("failure")) : Task.CompletedTask);
        if (fails) await Should.ThrowAsync<InvalidOperationException>(Run);
        else await Run();
        recorder.CounterSum("messaging_publish").ShouldBe(1);
        recorder.CounterSum("messaging_publish.completed").ShouldBe(fails ? 0 : 1);
        recorder.CounterSum("messaging_publish_failure").ShouldBe(fails ? 1 : 0);
        recorder.CounterSum("messaging_publish_current").ShouldBe(0);
        recorder.HistogramCount("messaging_publish_duration").ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Handle_FanOutOrCancellation_CountsHandlerCompletionsAccurately(bool cancel)
    {
        using var meterFactory = new TestMeterFactory();
        using var recorder = new MetricsRecorder();
        using var cancellation = new CancellationTokenSource();
        var sut = new MetricsMessageHandlerBehavior(NullLoggerFactory.Instance, new MetricsService(meterFactory));
        var message = new TestMessage();
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

        recorder.CounterSum("messaging_handle").ShouldBe(cancel ? 1 : 2);
        recorder.CounterSum("messaging_handle.completed").ShouldBe(cancel ? 0 : 2);
        recorder.CounterSum("messaging_handle_failure").ShouldBe(cancel ? 1 : 0);
        recorder.CounterSum("messaging_handle_current").ShouldBe(0);
        recorder.HistogramCount("messaging_handle_duration").ShouldBe(cancel ? 1 : 2);
    }

    private sealed class TestMessage : MessageBase;
}
