namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using Microsoft.Extensions.Logging.Abstractions;

public class MetricsNotificationBehaviorTests
{
    [Fact]
    public async Task HandleAsync_WhenPublishSucceeds_EmitsPublishMetrics()
    {
        using var meterFactory = new TestMeterFactory();
        using var recorder = new MetricsRecorder();
        var sut = new MetricsNotificationBehavior<TestNotification, IResult>(NullLoggerFactory.Instance, meterFactory);

        var result = await sut.HandleAsync(
            new TestNotification(),
            new PublishOptions(),
            handlerType: null,
            () => Task.FromResult<IResult>(Result.Success()),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        recorder.CounterSum("notifier_publish").ShouldBe(1);
        recorder.CounterSum("notifier_publish_testnotification").ShouldBe(1);
        recorder.CounterSum("notifier_publish_current").ShouldBe(0);
        recorder.CounterSum("notifier_publish_testnotification_current").ShouldBe(0);
        recorder.HistogramCount("notifier_publish_duration").ShouldBe(1);
        recorder.HistogramCount("notifier_publish_testnotification_duration").ShouldBe(1);
    }

    [Fact]
    public async Task HandleAsync_WhenHandlerFails_EmitsHandlerFailureMetrics()
    {
        using var meterFactory = new TestMeterFactory();
        using var recorder = new MetricsRecorder();
        var sut = new MetricsNotificationHandlerBehavior<TestNotification, IResult>(NullLoggerFactory.Instance, meterFactory);

        var result = await sut.HandleAsync(
            new TestNotification(),
            new PublishOptions(),
            typeof(TestNotificationHandler),
            () => Task.FromResult<IResult>(Result.Failure()),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        sut.IsHandlerSpecific().ShouldBeTrue();
        recorder.CounterSum("notifier_handle").ShouldBe(1);
        recorder.CounterSum("notifier_handle_testnotification").ShouldBe(1);
        recorder.CounterSum("notifier_handle_failure").ShouldBe(1);
        recorder.CounterSum("notifier_handle_testnotification_failure").ShouldBe(1);
        recorder.CounterSum("notifier_handle_current").ShouldBe(0);
        recorder.CounterSum("notifier_handle_testnotification_current").ShouldBe(0);
        recorder.HistogramCount("notifier_handle_duration").ShouldBe(1);
        recorder.HistogramCount("notifier_handle_testnotification_duration").ShouldBe(1);
    }

    private sealed class TestNotification : NotificationBase;

    private sealed class TestNotificationHandler;
}