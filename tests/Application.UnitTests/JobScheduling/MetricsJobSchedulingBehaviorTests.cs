namespace BridgingIT.DevKit.Application.UnitTests.JobScheduling;

using BridgingIT.DevKit.Application.JobScheduling;
using Quartz;

public class MetricsJobSchedulingBehaviorTests
{
    [Fact]
    public async Task Execute_WhenJobRuns_EmitsExecutionMetrics()
    {
        using var meterFactory = new TestMeterFactory();
        using var recorder = new MetricsRecorder();
        var sut = new MetricsJobSchedulingBehavior(meterFactory);
        var context = Substitute.For<IJobExecutionContext>();
        context.CancellationToken.Returns(CancellationToken.None);
        context.JobDetail.Returns(JobBuilder.Create<TestQuartzJob>().WithIdentity("job-1").Build());

        await sut.Execute(context, () => Task.CompletedTask);

        recorder.CounterSum("jobscheduling_execute").ShouldBe(1);
        recorder.CounterSum("jobscheduling_execute_testquartzjob").ShouldBe(1);
        recorder.CounterSum("jobscheduling_execute_current").ShouldBe(0);
        recorder.CounterSum("jobscheduling_execute_testquartzjob_current").ShouldBe(0);
        recorder.HistogramCount("jobscheduling_execute_duration").ShouldBe(1);
        recorder.HistogramCount("jobscheduling_execute_testquartzjob_duration").ShouldBe(1);
    }

    [Fact]
    public async Task Execute_WhenJobThrows_EmitsFailureMetrics()
    {
        using var meterFactory = new TestMeterFactory();
        using var recorder = new MetricsRecorder();
        var sut = new MetricsJobSchedulingBehavior(meterFactory);
        var context = Substitute.For<IJobExecutionContext>();
        context.CancellationToken.Returns(CancellationToken.None);
        context.JobDetail.Returns(JobBuilder.Create<TestQuartzJob>().WithIdentity("job-1").Build());

        await Should.ThrowAsync<InvalidOperationException>(() => sut.Execute(context, () => throw new InvalidOperationException("boom")));

        recorder.CounterSum("jobscheduling_execute_failure").ShouldBe(1);
        recorder.CounterSum("jobscheduling_execute_testquartzjob_failure").ShouldBe(1);
        recorder.CounterSum("jobscheduling_execute_current").ShouldBe(0);
        recorder.CounterSum("jobscheduling_execute_testquartzjob_current").ShouldBe(0);
    }

    private sealed class TestQuartzJob : IJob
    {
        public Task Execute(IJobExecutionContext context)
        {
            return Task.CompletedTask;
        }
    }
}