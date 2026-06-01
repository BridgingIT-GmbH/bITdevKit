namespace BridgingIT.DevKit.Application.UnitTests.Orchestrations;

using BridgingIT.DevKit.Application.Orchestrations;
using Microsoft.Extensions.DependencyInjection;

public class MetricsOrchestrationBehaviorTests(ITestOutputHelper output) : OrchestrationTestBase(output)
{
    [Fact]
    public async Task ExecuteAsync_WhenActivityRuns_EmitsExecutionMetrics()
    {
        using var meterFactory = new TestMeterFactory();
        using var recorder = new MetricsRecorder();
        var sut = new MetricsOrchestrationBehavior(meterFactory);
        var context = new OrchestrationActivityExecutionContext(
            Guid.NewGuid(),
            "OrderApproval",
            "corr-1",
            "Pending",
            "WaitForApproval",
            OrchestrationActivityExecutionKind.Activity,
            1,
            new ServiceCollection().BuildServiceProvider(),
            new object());

        var result = await sut.ExecuteAsync(context, CancellationToken.None, () => Task.FromResult(OrchestrationOutcome.Continue()));

        result.Kind.ShouldBe(OrchestrationOutcomeKind.Continue);
        recorder.CounterSum("orchestrations_activity_execute").ShouldBe(1);
        recorder.CounterSum("orchestrations_activity_execute_orderapproval_waitforapproval").ShouldBe(1);
        recorder.CounterSum("orchestrations_activity_execute_current").ShouldBe(0);
        recorder.CounterSum("orchestrations_activity_execute_orderapproval_waitforapproval_current").ShouldBe(0);
        recorder.HistogramCount("orchestrations_activity_execute_duration").ShouldBe(1);
        recorder.HistogramCount("orchestrations_activity_execute_orderapproval_waitforapproval_duration").ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenActivityThrows_EmitsFailureMetrics()
    {
        using var meterFactory = new TestMeterFactory();
        using var recorder = new MetricsRecorder();
        var sut = new MetricsOrchestrationBehavior(meterFactory);
        var context = new OrchestrationActivityExecutionContext(
            Guid.NewGuid(),
            "OrderApproval",
            "corr-1",
            "Pending",
            "WaitForApproval",
            OrchestrationActivityExecutionKind.SignalActivity,
            2,
            new ServiceCollection().BuildServiceProvider(),
            new object());

        await Should.ThrowAsync<InvalidOperationException>(() => sut.ExecuteAsync(context, CancellationToken.None, () => throw new InvalidOperationException("boom")));

        recorder.CounterSum("orchestrations_activity_execute_failure").ShouldBe(1);
        recorder.CounterSum("orchestrations_activity_execute_orderapproval_waitforapproval_failure").ShouldBe(1);
        recorder.CounterSum("orchestrations_activity_execute_current").ShouldBe(0);
        recorder.CounterSum("orchestrations_activity_execute_orderapproval_waitforapproval_current").ShouldBe(0);
    }
}