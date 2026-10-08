// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Jobs;

using BridgingIT.DevKit.Application.Jobs;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Checks optional job profiling ownership, results, cancellation and exactly-once delegation.</summary>
/// <example>Runs through the public recorder façade and shared periodic writer.</example>
public sealed class JobProfilingBehaviorTests
{
    /// <summary>Checks absent registration directly returns the original next task and invokes it once.</summary>
    [Fact]
    public async Task HandleAsync_OmittedProfiler_PreservesTaskAndResult()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var sut = ActivatorUtilities.CreateInstance<JobProfilingBehavior>(services);
        var expected = Result<JobExecutionResult>.Success(new() { Status = JobExecutionStatus.Completed });
        var task = Task.FromResult<IResult<JobExecutionResult>>(expected);
        var calls = 0;
        var actual = sut.HandleAsync(null, () => { calls++; return task; });
        actual.ShouldBeSameAs(task);
        (await actual).ShouldBe(expected);
        calls.ShouldBe(1);
        services.GetService<IOperationProfiler>().ShouldBeNull();
    }

    /// <summary>Checks domain statuses and timeout classification without storing result payloads.</summary>
    [Theory]
    [InlineData(JobExecutionStatus.Completed, OperationProfilingOutcome.Completed)]
    [InlineData(JobExecutionStatus.Failed, OperationProfilingOutcome.Failed)]
    [InlineData(JobExecutionStatus.TimedOut, OperationProfilingOutcome.Failed)]
    [InlineData(JobExecutionStatus.Cancelled, OperationProfilingOutcome.Canceled)]
    [InlineData(JobExecutionStatus.Interrupted, OperationProfilingOutcome.Canceled)]
    [InlineData(JobExecutionStatus.Retried, OperationProfilingOutcome.Completed)]
    public async Task HandleAsync_ReturnedStatus_RecordsActualBoundary(JobExecutionStatus status, OperationProfilingOutcome expected)
    {
        using var services = await Services();
        var profiling = services.GetRequiredService<IOperationProfiler>();
        var sut = new JobProfilingBehavior(profiling);
        var context = Context(services);
        var result = Result<JobExecutionResult>.Success(new() { Status = status });
        var id = Guid.Empty;
        var calls = 0;
        var actual = await sut.HandleAsync(context, () =>
        {
            id = profiling.Current.Id;
            calls++;
            return Task.FromResult<IResult<JobExecutionResult>>(result);
        });
        actual.ShouldBe(result);
        calls.ShouldBe(1);
        var record = await Stored(services, id);
        record.Key.ShouldBe("job:Cleanup");
        record.Kind.ShouldBe("Job");
        record.CorrelationId.ShouldBe("job-correlation");
        record.Outcome.ShouldBe(expected);
        record.Segments.ShouldBeEmpty();
        if (status == JobExecutionStatus.TimedOut) { record.Failure.Code.ShouldBe("Timeout"); }

        profiling.Current.ShouldBeNull();
    }

    /// <summary>Checks a job contributes a child segment without closing its caller's root.</summary>
    [Fact]
    public async Task HandleAsync_ExistingOperation_JoinsAndPreservesOwner()
    {
        using var services = await Services();
        var profiling = services.GetRequiredService<IOperationProfiler>();
        using var owner = profiling.BeginOperation("caller");
        var sut = new JobProfilingBehavior(profiling);
        await sut.HandleAsync(Context(services), () => Task.FromResult<IResult<JobExecutionResult>>(Result<JobExecutionResult>.Success(new() { Status = JobExecutionStatus.Failed })));
        owner.IsRecording.ShouldBeTrue();
        profiling.Current.ShouldBeSameAs(owner);
        owner.Complete(); owner.Dispose();
        var record = await Stored(services, owner.Id);
        record.Outcome.ShouldBe(OperationProfilingOutcome.Completed);
        record.Segments.ShouldHaveSingleItem().Outcomes.ShouldContain(o => o.Outcome == ProfilingSegmentOutcome.Failed);
    }

    /// <summary>Checks synchronous and asynchronous errors are rethrown unchanged and scope state is restored.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandleAsync_DelegateException_PreservesFailureAndRestoresContext(bool asynchronous)
    {
        using var services = await Services();
        var profiling = services.GetRequiredService<IOperationProfiler>();
        var sut = new JobProfilingBehavior(profiling);
        var exception = new InvalidOperationException("raw message is not retained");
        var id = Guid.Empty;
        var caught = await Should.ThrowAsync<InvalidOperationException>(() => sut.HandleAsync(Context(services), () =>
        {
            id = profiling.Current.Id;
            return asynchronous ? Task.FromException<IResult<JobExecutionResult>>(exception) : throw exception;
        }));
        caught.ShouldBeSameAs(exception);
        var record = await Stored(services, id);
        record.Outcome.ShouldBe(OperationProfilingOutcome.Failed);
        record.Failure.Message.ShouldBeNull();
        profiling.Current.ShouldBeNull();
    }

    /// <summary>Checks suppression and registered-disabled recording still execute business work once.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandleAsync_SuppressedOrDisabled_RecordsNoReplacementRoot(bool disabled)
    {
        using var services = await Services(!disabled);
        var profiling = services.GetRequiredService<IOperationProfiler>();
        using var suppression = disabled ? null : profiling.Suppress();
        var sut = new JobProfilingBehavior(profiling);
        var calls = 0;
        await sut.HandleAsync(Context(services), () =>
        {
            calls++;
            profiling.Current.IsRecording.ShouldBeFalse();
            return Task.FromResult<IResult<JobExecutionResult>>(Result<JobExecutionResult>.Success(new() { Status = JobExecutionStatus.Completed }));
        });
        calls.ShouldBe(1);
        services.GetRequiredService<OperationProfilingCompletionQueue>().Snapshot().Count.ShouldBe(0);
    }

    /// <summary>Checks a failed result is recorded without reading its payload or raw messages.</summary>
    [Fact]
    public async Task HandleAsync_FailedResult_RecordsFailureWithoutChangingReturn()
    {
        using var services = await Services();
        var profiling = services.GetRequiredService<IOperationProfiler>();
        var sut = new JobProfilingBehavior(profiling);
        var expected = Result<JobExecutionResult>.Failure().WithError(new Error("raw failure detail"));
        var id = Guid.Empty;
        var actual = await sut.HandleAsync(Context(services), () => { id = profiling.Current.Id; return Task.FromResult<IResult<JobExecutionResult>>(expected); });
        actual.ShouldBe(expected);
        (await Stored(services, id)).Outcome.ShouldBe(OperationProfilingOutcome.Failed);
    }

    /// <summary>Checks application cancellation is observed without changing the supplied token or exception.</summary>
    [Fact]
    public async Task HandleAsync_CanceledDelegate_PreservesTokenAndException()
    {
        using var services = await Services();
        var profiling = services.GetRequiredService<IOperationProfiler>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var exception = new OperationCanceledException(cancellation.Token);
        var sut = new JobProfilingBehavior(profiling);
        var id = Guid.Empty;
        OperationCanceledException actual = null;
        try
        {
            await sut.HandleAsync(Context(services), () =>
            {
                id = profiling.Current.Id;
                return Task.FromException<IResult<JobExecutionResult>>(exception);
            }, cancellation.Token);
        }
        catch (OperationCanceledException caught) { actual = caught; }

        actual.ShouldBeSameAs(exception);
        (await Stored(services, id)).Outcome.ShouldBe(OperationProfilingOutcome.Canceled);
        profiling.Current.ShouldBeNull();
    }

    /// <summary>Checks concurrent joined jobs aggregate once at the same path without sharing invocation ownership.</summary>
    [Fact]
    public async Task HandleAsync_ConcurrentJobs_AggregatesDistinctInvocations()
    {
        using var services = await Services();
        var profiling = services.GetRequiredService<IOperationProfiler>();
        using var owner = profiling.BeginOperation("parallel-jobs");
        var sut = new JobProfilingBehavior(profiling);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var tasks = Enumerable.Range(0, 5).Select(_ => sut.HandleAsync(Context(services), async () =>
        {
            Interlocked.Increment(ref calls);
            await gate.Task;
            return Result<JobExecutionResult>.Success(new() { Status = JobExecutionStatus.Completed });
        })).ToArray();
        calls.ShouldBe(5);
        gate.SetResult();
        await Task.WhenAll(tasks);
        owner.Complete(); owner.Dispose();
        (await Stored(services, owner.Id)).Segments.ShouldHaveSingleItem().Statistics.Count.ShouldBe(5);
    }

    private static JobBehaviorContext Context(IServiceProvider services)
    {
        var execution = Substitute.For<IJobExecutionContext>();
        execution.JobName.Returns("Cleanup"); execution.CorrelationId.Returns("job-correlation");
        execution.ExecutionId.Returns(Guid.NewGuid()); execution.AttemptNumber.Returns(2);
        return new(services, new() { JobName = "Cleanup", JobType = typeof(JobProfilingBehaviorTests) }, new() { TriggerName = "manual", TriggerType = JobTriggerType.Manual },
            Substitute.For<IJob>(), execution, "scheduler", 1, 4);
    }

    private static async Task<ServiceProvider> Services(bool enabled = true)
    {
        var collection = new ServiceCollection();
        collection.AddLogging();
        collection.AddProfiling(o => o.Enabled(enabled)).WithOperationProfiling();
        var services = collection.BuildServiceProvider();
        if (enabled) { await services.GetRequiredService<OperationProfilingWriterService>().TickAsync(); }

        return services;
    }

    private static async Task<OperationProfilingRecord> Stored(IServiceProvider services, Guid id)
    {
        await services.GetRequiredService<OperationProfilingWriterService>().TickAsync();
        return (await services.GetRequiredService<IOperationProfilingStore>().FindAsync(id)).Value.ShouldNotBeNull();
    }
}
