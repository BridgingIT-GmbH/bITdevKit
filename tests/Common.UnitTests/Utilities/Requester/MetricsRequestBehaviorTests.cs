namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using Microsoft.Extensions.Logging.Abstractions;

public class MetricsRequestBehaviorTests
{
    [Fact]
    public async Task HandleAsync_WhenRequestSucceeds_EmitsSendAndHandleMetrics()
    {
        using var meterFactory = new TestMeterFactory();
        using var recorder = new MetricsRecorder();
        var sut = new MetricsRequestBehavior<TestRequest, IResult<string>>(NullLoggerFactory.Instance, meterFactory);

        var result = await sut.HandleAsync(
            new TestRequest(),
            new SendOptions(),
            typeof(TestRequestHandler),
            () => Task.FromResult<IResult<string>>(Result<string>.Success("ok")),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        recorder.CounterSum("requester_send").ShouldBe(1);
        recorder.CounterSum("requester_send_testrequest").ShouldBe(1);
        recorder.CounterSum("requester_handle").ShouldBe(1);
        recorder.CounterSum("requester_handle_testrequest").ShouldBe(1);
        recorder.CounterSum("requester_send_current").ShouldBe(0);
        recorder.CounterSum("requester_send_testrequest_current").ShouldBe(0);
        recorder.CounterSum("requester_handle_current").ShouldBe(0);
        recorder.CounterSum("requester_handle_testrequest_current").ShouldBe(0);
        recorder.HistogramCount("requester_send_duration").ShouldBe(1);
        recorder.HistogramCount("requester_send_testrequest_duration").ShouldBe(1);
        recorder.HistogramCount("requester_handle_duration").ShouldBe(1);
        recorder.HistogramCount("requester_handle_testrequest_duration").ShouldBe(1);
    }

    [Fact]
    public async Task HandleAsync_WhileRequestIsRunning_TracksCurrentMetrics()
    {
        using var meterFactory = new TestMeterFactory();
        using var recorder = new MetricsRecorder();
        var sut = new MetricsRequestBehavior<TestRequest, IResult<string>>(NullLoggerFactory.Instance, meterFactory);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var task = sut.HandleAsync(
            new TestRequest(),
            new SendOptions(),
            typeof(TestRequestHandler),
            async () =>
            {
                recorder.CounterSum("requester_send_current").ShouldBe(1);
                recorder.CounterSum("requester_send_testrequest_current").ShouldBe(1);
                recorder.CounterSum("requester_handle_current").ShouldBe(1);
                recorder.CounterSum("requester_handle_testrequest_current").ShouldBe(1);
                await release.Task;
                return Result<string>.Success("ok");
            },
            CancellationToken.None);

        await Task.Yield();
        release.SetResult();

        var result = await task;

        result.IsSuccess.ShouldBeTrue();
        recorder.CounterSum("requester_send_current").ShouldBe(0);
        recorder.CounterSum("requester_send_testrequest_current").ShouldBe(0);
        recorder.CounterSum("requester_handle_current").ShouldBe(0);
        recorder.CounterSum("requester_handle_testrequest_current").ShouldBe(0);
    }

    [Fact]
    public async Task HandleAsync_WhenRequestFails_EmitsFailureMetrics()
    {
        using var meterFactory = new TestMeterFactory();
        using var recorder = new MetricsRecorder();
        var sut = new MetricsRequestBehavior<TestRequest, IResult<string>>(NullLoggerFactory.Instance, meterFactory);

        var result = await sut.HandleAsync(
            new TestRequest(),
            new SendOptions(),
            typeof(TestRequestHandler),
            () => Task.FromResult<IResult<string>>(Result<string>.Failure()),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        recorder.CounterSum("requester_send_failure").ShouldBe(1);
        recorder.CounterSum("requester_send_testrequest_failure").ShouldBe(1);
        recorder.CounterSum("requester_handle_failure").ShouldBe(1);
        recorder.CounterSum("requester_handle_testrequest_failure").ShouldBe(1);
        recorder.CounterSum("requester_send_current").ShouldBe(0);
        recorder.CounterSum("requester_send_testrequest_current").ShouldBe(0);
        recorder.CounterSum("requester_handle_current").ShouldBe(0);
        recorder.CounterSum("requester_handle_testrequest_current").ShouldBe(0);
    }

    [Fact]
    public async Task HandleAsync_WhenMeterFactoryIsMissing_RemainsPassThrough()
    {
        var sut = new MetricsRequestBehavior<TestRequest, IResult<string>>(NullLoggerFactory.Instance);
        var invoked = false;

        var result = await sut.HandleAsync(
            new TestRequest(),
            new SendOptions(),
            typeof(TestRequestHandler),
            () =>
            {
                invoked = true;
                return Task.FromResult<IResult<string>>(Result<string>.Success("ok"));
            },
            CancellationToken.None);

        invoked.ShouldBeTrue();
        result.IsSuccess.ShouldBeTrue();
    }

    private sealed class TestRequest : RequestBase<string>;

    private sealed class TestRequestHandler;
}