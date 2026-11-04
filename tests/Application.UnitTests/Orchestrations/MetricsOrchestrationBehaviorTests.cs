namespace BridgingIT.DevKit.Application.UnitTests.Orchestrations;

using BridgingIT.DevKit.Application.Orchestrations;
using Microsoft.Extensions.DependencyInjection;

[Collection(ProcessWideStateTestCollection.Name)]
public class MetricsOrchestrationBehaviorTests(ITestOutputHelper output) : OrchestrationTestBase(output)
{
    [Fact]
    public async Task ExecuteAsync_WhenActivityRuns_EmitsExecutionMetrics()
    {
        using var meterFactory = new TestMeterFactory();
        using var recorder = new MetricsRecorder();
        var sut = new MetricsOrchestrationBehavior(new MetricsService(meterFactory));
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
        recorder.CounterSum("orchestrations_activity_execute.completed").ShouldBe(1);
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
        var sut = new MetricsOrchestrationBehavior(new MetricsService(meterFactory));
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
        recorder.CounterSum("orchestrations_activity_execute.completed").ShouldBe(0);
        recorder.HistogramCount("orchestrations_activity_execute_duration").ShouldBe(1);
        recorder.CounterSum("orchestrations_activity_execute_orderapproval_waitforapproval_failure").ShouldBe(1);
        recorder.CounterSum("orchestrations_activity_execute_current").ShouldBe(0);
        recorder.CounterSum("orchestrations_activity_execute_orderapproval_waitforapproval_current").ShouldBe(0);
    }
    [Theory]
    [InlineData((OrchestrationOutcomeKind)(-1), "failed")]
    [InlineData(OrchestrationOutcomeKind.Complete, "completed")]
    [InlineData(OrchestrationOutcomeKind.Cancel, "cancelled")]
    [InlineData(OrchestrationOutcomeKind.Terminate, "terminated")]
    public async Task ExecuteAsync_TerminalOutcomes_RecordOneFinishAndDuration(OrchestrationOutcomeKind outcome, string status)
    {
        using var meterFactory = new TestMeterFactory();
        using var recorder = new MetricsRecorder();
        await using var sut = this.CreateHarnessBuilder()
            .ConfigureServices(services => services.AddSingleton<IMetricsService>(new MetricsService(meterFactory)))
            .WithBehavior<MetricsOrchestrationBehavior>()
            .WithOrchestration<MeasuredOrchestration>()
            .Build();

        await sut.ExecuteAsync<MeasuredOrchestration, MeasuredData>(new MeasuredData { Outcome = outcome });

        recorder.CounterSum("orchestrations_start").ShouldBe(1);
        recorder.CounterSum("orchestrations_finish").ShouldBe(1);
        recorder.CounterSum("orchestrations_finish." + status).ShouldBe(1);
        recorder.CounterSum("orchestrations_activity_execute.completed").ShouldBe(status == "failed" ? 0 : 1);
        recorder.HistogramCount("orchestrations_instance_duration").ShouldBe(1);
    }

    private sealed class MeasuredData : IOrchestrationData
    {
        public OrchestrationOutcomeKind Outcome { get; set; }
    }

    private sealed class MeasuredOrchestration : Orchestration<MeasuredData>
    {
        protected override void Define(IOrchestrationBuilder<MeasuredData> builder) =>
            builder.State("Run", state => state.Activity((context, token) =>
                context.Data.Outcome == (OrchestrationOutcomeKind)(-1)
                    ? Task.FromException<OrchestrationOutcome>(new InvalidOperationException("measured failure"))
                    : Task.FromResult(new OrchestrationOutcome(context.Data.Outcome)), name: "Measured").Complete());
    }

}
