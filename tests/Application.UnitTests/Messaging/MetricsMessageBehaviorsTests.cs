namespace BridgingIT.DevKit.Application.UnitTests.Messaging;

using BridgingIT.DevKit.Application.Messaging;
using Microsoft.Extensions.Logging.Abstractions;

public class MetricsMessageBehaviorsTests
{
    [Fact]
    public async Task Publish_WhenMeterFactoryIsMissing_RemainsPassThrough()
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
        var sut = new MetricsMessageHandlerBehavior(NullLoggerFactory.Instance, meterFactory);

        await Should.ThrowAsync<InvalidOperationException>(() => sut.Handle(
            new TestMessage(),
            CancellationToken.None,
            new object(),
            () => throw new InvalidOperationException("boom")));

        recorder.CounterSum("messaging_handle").ShouldBe(1);
        recorder.CounterSum("messaging_handle_testmessage").ShouldBe(1);
        recorder.CounterSum("messaging_handle_failure").ShouldBe(1);
        recorder.CounterSum("messaging_handle_testmessage_failure").ShouldBe(1);
        recorder.CounterSum("messaging_handle_current").ShouldBe(0);
        recorder.CounterSum("messaging_handle_testmessage_current").ShouldBe(0);
    }

    [Fact]
    public async Task Handle_WhileHandlerIsRunning_TracksCurrentMetrics()
    {
        using var meterFactory = new TestMeterFactory();
        using var recorder = new MetricsRecorder();
        var sut = new MetricsMessageHandlerBehavior(NullLoggerFactory.Instance, meterFactory);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var task = sut.Handle(
            new TestMessage(),
            CancellationToken.None,
            new object(),
            async () =>
            {
                recorder.CounterSum("messaging_handle_current").ShouldBe(1);
                recorder.CounterSum("messaging_handle_testmessage_current").ShouldBe(1);
                await release.Task;
            });

        await Task.Yield();
        release.SetResult();
        await task;

        recorder.CounterSum("messaging_handle_current").ShouldBe(0);
        recorder.CounterSum("messaging_handle_testmessage_current").ShouldBe(0);
    }

    private sealed class TestMessage : MessageBase;
}