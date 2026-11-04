// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
namespace BridgingIT.DevKit.Application.UnitTests.Jobs;

using BridgingIT.DevKit.Application.Jobs;
using Microsoft.Extensions.DependencyInjection;

[Collection(ProcessWideStateTestCollection.Name)]
public class JobMetricsBehaviorTests
{
    [Theory]
    [InlineData("completed", "completed")]
    [InlineData("retried", "retried")]
    [InlineData("failed-result", "failed")]
    [InlineData("exception", "failed")]
    [InlineData("cancelled", "cancelled")]
    public async Task Handle_TerminalPaths_RecordOneOutcomeAndBalanceActiveExecutions(string path, string counter)
    {
        using var meterFactory = new TestMeterFactory();
        using var recorder = new MetricsRecorder();
        using var services = new ServiceCollection().BuildServiceProvider();
        using var cancellation = new CancellationTokenSource();
        var execution = new JobExecutionContextBuilder<Unit>().Build();
        var context = new JobBehaviorContext(services,
            new JobDefinition { JobName = "test-job", JobType = typeof(JobMetricsBehaviorTests) },
            new JobTriggerDefinition { TriggerName = "manual", TriggerType = JobTriggerType.Manual },
            Substitute.For<IJob>(), execution, "test-scheduler", 1, 1);
        var sut = new JobMetricsBehavior(TimeProvider.System, new MetricsService(meterFactory));
        JobBehaviorDelegate next;
        if (path == "exception") next = () => Task.FromException<IResult<JobExecutionResult>>(new InvalidOperationException("failure"));
        else if (path == "cancelled")
        {
            cancellation.Cancel();
            next = () => Task.FromCanceled<IResult<JobExecutionResult>>(cancellation.Token);
        }
        else if (path == "failed-result") next = () => Task.FromResult<IResult<JobExecutionResult>>(Result<JobExecutionResult>.Failure());
        else next = () => Task.FromResult<IResult<JobExecutionResult>>(Result<JobExecutionResult>.Success(new()
        {
            Status = path == "retried" ? JobExecutionStatus.Retried : JobExecutionStatus.Completed,
            StartedUtc = execution.StartedUtc,
            CompletedUtc = DateTimeOffset.UtcNow
        }));

        if (path == "exception") await Should.ThrowAsync<InvalidOperationException>(() => sut.HandleAsync(context, next));
        else if (path == "cancelled") await Should.ThrowAsync<OperationCanceledException>(() => sut.HandleAsync(context, next, cancellation.Token));
        else await sut.HandleAsync(context, next);

        recorder.CounterSum("jobs_executions_started").ShouldBe(1);
        recorder.CounterSum("jobs_executions_" + counter).ShouldBe(1);
        recorder.CounterSum("jobs_executions_active").ShouldBe(0);
        recorder.HistogramCount("jobs_execution_duration").ShouldBe(1);
    }
}
