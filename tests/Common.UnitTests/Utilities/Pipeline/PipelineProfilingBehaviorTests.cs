// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks.Sources;

/// <summary>Checks pipeline and step ownership, carried results, control outcomes and repeated invocations.</summary>
/// <example>Executes the existing ValueTask behavior contracts using the public optional recorder.</example>
public sealed class PipelineProfilingBehaviorTests
{
    /// <summary>Checks absence of profiling registers no dependencies and delegates once.</summary>
    [Fact]
    public async Task ExecuteAsync_OmittedProfiler_PreservesBusinessResult()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var sut = ActivatorUtilities.CreateInstance<PipelineProfilingBehavior>(provider);
        var expected = Result.Success().WithMessage("preserved");
        var calls = 0;
        var actual = await sut.ExecuteAsync(null, () => { calls++; return ValueTask.FromResult(expected); }, default);
        actual.ShouldBe(expected);
        calls.ShouldBe(1);
        provider.GetService<IOperationProfiler>().ShouldBeNull();
    }

    /// <summary>Checks every executed control outcome is measured and never alone implies failure.</summary>
    [Theory]
    [InlineData(PipelineControlOutcome.Continue)]
    [InlineData(PipelineControlOutcome.Skip)]
    [InlineData(PipelineControlOutcome.Retry)]
    [InlineData(PipelineControlOutcome.Break)]
    [InlineData(PipelineControlOutcome.Terminate)]
    public async Task ExecuteStepAsync_ControlOutcome_PreservesControlAndAggregatesAttempts(PipelineControlOutcome outcome)
    {
        using var provider = await Services();
        var profiling = provider.GetRequiredService<IOperationProfiler>();
        var sut = new PipelineProfilingBehavior(profiling);
        var context = Context();
        var carried = Result.Success().WithMessage("carried");
        var expected = Control(outcome, carried);
        var id = Guid.Empty;
        var calls = 0;
        var actual = await sut.ExecuteAsync(context, async () =>
        {
            id = profiling.Current.Id;
            for (var i = 0; i < 3; i++)
            {
                var control = await sut.ExecuteStepAsync(context, Step("Load"), carried, () => { calls++; return ValueTask.FromResult(expected); }, default);
                control.ShouldBeSameAs(expected);
                control.Result.ShouldBe(carried);
            }

            return carried;
        }, default);
        actual.ShouldBe(carried);
        calls.ShouldBe(3);
        var record = await Stored(provider, id);
        record.Key.ShouldBe("pipeline:Import");
        record.Kind.ShouldBe("Pipeline");
        record.CorrelationId.ShouldBe("pipeline-correlation");
        var segment = record.Segments.ShouldHaveSingleItem();
        segment.Key.ShouldBe("step:Load");
        segment.Statistics.Count.ShouldBe(3);
        segment.Outcomes.ShouldHaveSingleItem().Outcome.ShouldBe(ProfilingSegmentOutcome.Completed);
    }

    /// <summary>Checks nested pipeline paths and failed carried results do not close the caller's root.</summary>
    [Fact]
    public async Task ExecuteAsync_NestedWithinCaller_RetainsParentAndFailureStatistics()
    {
        using var provider = await Services();
        var profiling = provider.GetRequiredService<IOperationProfiler>();
        using var owner = profiling.BeginOperation("caller");
        var sut = new PipelineProfilingBehavior(profiling);
        var result = await sut.ExecuteAsync(Context(), async () =>
        {
            var control = await sut.ExecuteStepAsync(Context(), Step("Work"), Result.Success(),
                () => ValueTask.FromResult(PipelineControl.Continue(Result.Failure().WithError(new Error("raw result data")))), default);
            return control.Result;
        }, default);
        result.IsFailure.ShouldBeTrue();
        owner.IsRecording.ShouldBeTrue();
        owner.Complete(); owner.Dispose();
        var record = await Stored(provider, owner.Id);
        record.Outcome.ShouldBe(OperationProfilingOutcome.Completed);
        record.Segments.Count.ShouldBe(2);
        record.Segments.Single(s => s.Key == "step:Work").Path.Components.ShouldBe(new[] { "pipeline:Import", "step:Work" });
    }

    /// <summary>Checks normal registered-disabled and suppressed calls have no completion payload.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_SuppressedOrDisabled_ExecutesWithoutRecording(bool disabled)
    {
        using var provider = await Services(!disabled);
        var profiling = provider.GetRequiredService<IOperationProfiler>();
        using var suppression = disabled ? null : profiling.Suppress();
        var sut = new PipelineProfilingBehavior(profiling);
        var calls = 0;
        await sut.ExecuteAsync(Context(), () => { calls++; return ValueTask.FromResult(Result.Success()); }, default);
        calls.ShouldBe(1);
        provider.GetRequiredService<OperationProfilingCompletionQueue>().Snapshot().Count.ShouldBe(0);
    }

    /// <summary>Checks synchronous and asynchronous step exceptions are rethrown unchanged and classified once.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteStepAsync_ThrownException_PreservesFailureAndScopeOwnership(bool asynchronous)
    {
        using var provider = await Services();
        var profiling = provider.GetRequiredService<IOperationProfiler>();
        var sut = new PipelineProfilingBehavior(profiling);
        var exception = new InvalidOperationException("raw detail");
        var id = Guid.Empty;
        var caught = await Should.ThrowAsync<InvalidOperationException>(async () => await sut.ExecuteAsync(Context(), async () =>
        {
            id = profiling.Current.Id;
            await sut.ExecuteStepAsync(Context(), Step("Throw"), Result.Success(),
                () => asynchronous ? new ValueTask<PipelineControl>(Task.FromException<PipelineControl>(exception)) : throw exception, default);
            return Result.Success();
        }, default));
        caught.ShouldBeSameAs(exception);
        var record = await Stored(provider, id);
        record.Outcome.ShouldBe(OperationProfilingOutcome.Failed);
        record.Segments.ShouldHaveSingleItem().Outcomes.ShouldHaveSingleItem().Outcome.ShouldBe(ProfilingSegmentOutcome.Failed);
        profiling.Current.ShouldBeNull();
    }

    /// <summary>Checks concurrent pipeline runs under one owner retain separate step parents and additive counts.</summary>
    [Fact]
    public async Task ExecuteAsync_ConcurrentRuns_AggregatesMatchingPathsWithoutOrphans()
    {
        using var provider = await Services();
        var profiling = provider.GetRequiredService<IOperationProfiler>();
        using var owner = profiling.BeginOperation("parallel-pipelines");
        var sut = new PipelineProfilingBehavior(profiling);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = 0;
        var tasks = Enumerable.Range(0, 4).Select(_ => sut.ExecuteAsync(Context(), async () =>
        {
            var control = await sut.ExecuteStepAsync(Context(), Step("Work"), Result.Success(), async () =>
            {
                Interlocked.Increment(ref entered);
                await gate.Task;
                return PipelineControl.Continue(Result.Success());
            }, default);
            return control.Result;
        }, default).AsTask()).ToArray();
        entered.ShouldBe(4);
        gate.SetResult();
        await Task.WhenAll(tasks);
        owner.Complete(); owner.Dispose();
        var record = await Stored(provider, owner.Id);
        record.Segments.Count.ShouldBe(2);
        record.Segments.ShouldAllBe(s => s.Statistics.Count == 4);
    }

    /// <summary>Checks the real engine records retry attempts and omits disabled and never-reached steps.</summary>
    [Fact]
    public async Task PipelineEngine_RetriesAndBreak_RecordsOnlyInvokedSteps()
    {
        var collection = new ServiceCollection();
        collection.AddLogging();
        collection.AddProfiling(o => o.Enabled()).WithOperationProfiling();
        var attempts = 0;
        collection.AddPipelines().WithPipeline<TestContext>("Import", builder => builder
            .AddStep((IPipelineInlineStepExecution<TestContext> execution) => ++attempts == 1
                ? execution.Retry() : execution.Continue(), "Retry")
            .AddStep(() => throw new InvalidOperationException("disabled"), "Disabled", enabled: false)
            .AddStep((IPipelineInlineStepExecution<TestContext> execution) => execution.Break(), "Break")
            .AddStep(() => throw new InvalidOperationException("unreached"), "Unreached")
            .AddBehavior<PipelineProfilingBehavior>());
        using var provider = collection.BuildServiceProvider();
        await provider.GetRequiredService<OperationProfilingWriterService>().TickAsync();
        var pipeline = provider.GetRequiredService<IPipelineFactory>().Create<TestContext>("Import");
        var result = await pipeline.ExecuteAsync(new TestContext());
        result.IsSuccess.ShouldBeTrue();
        attempts.ShouldBe(2);
        await provider.GetRequiredService<OperationProfilingWriterService>().TickAsync();
        var page = await provider.GetRequiredService<IOperationProfilingStore>().QueryAsync(new()
        {
            FromUtc = DateTimeOffset.UtcNow.AddHours(-1), ToUtc = DateTimeOffset.UtcNow.AddHours(1),
        });
        var record = page.Value.Records.ShouldHaveSingleItem();
        record.Segments.Count.ShouldBe(2);
        record.Segments.Single(s => s.Key == "step:Retry").Statistics.Count.ShouldBe(2);
        record.Segments.Single(s => s.Key == "step:Break").Statistics.Count.ShouldBe(1);
    }

    /// <summary>Checks the adapter consumes native ValueTask sources exactly once.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_NativeValueTaskSource_IsConsumedOnce(bool capture)
    {
        using var provider = await Services(capture);
        var sut = new PipelineProfilingBehavior(capture ? provider.GetRequiredService<IOperationProfiler>() : null);
        var source = new SingleConsumptionSource();
        var result = await sut.ExecuteAsync(Context(), () => new ValueTask<Result>(source, 0), default);
        result.IsSuccess.ShouldBeTrue();
        source.ReadCount.ShouldBe(1);
    }

    /// <summary>Checks independent background pipelines clear inherited suppression before starting their own operation.</summary>
    [Fact]
    public async Task PipelineEngine_BackgroundExecution_DoesNotInheritSuppression()
    {
        var collection = new ServiceCollection();
        collection.AddLogging();
        collection.AddProfiling(o => o.Enabled()).WithOperationProfiling();
        collection.AddPipelines().WithPipeline<TestContext>("Background", builder => builder
            .AddStep(() => { }, "Work").AddBehavior<PipelineProfilingBehavior>());
        using var provider = collection.BuildServiceProvider();
        await provider.GetRequiredService<OperationProfilingWriterService>().TickAsync();
        var profiling = provider.GetRequiredService<IOperationProfiler>();
        var pipeline = provider.GetRequiredService<IPipelineFactory>().Create<TestContext>("Background");
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using (profiling.Suppress())
        {
            await pipeline.ExecuteAndForgetAsync(new TestContext(), builder => builder.WhenCompleted(_ =>
            {
                completion.SetResult();
                return ValueTask.CompletedTask;
            }));
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        await provider.GetRequiredService<OperationProfilingWriterService>().TickAsync();
        var page = await provider.GetRequiredService<IOperationProfilingStore>().QueryAsync(new()
        {
            FromUtc = DateTimeOffset.UtcNow.AddHours(-1), ToUtc = DateTimeOffset.UtcNow.AddHours(1),
        });
        var record = page.Value.Records.ShouldHaveSingleItem();
        record.Key.ShouldBe("pipeline:Background");
        record.Segments.ShouldHaveSingleItem().Key.ShouldBe("step:Work");
        profiling.Current.ShouldBeNull();
    }

    private sealed class SingleConsumptionSource : IValueTaskSource<Result>
    {
        /// <summary>Gets the number of consumed results.</summary>
        public int ReadCount { get; private set; }
        /// <inheritdoc />
        public Result GetResult(short token)
        {
            if (++this.ReadCount != 1) { throw new InvalidOperationException("Consumed twice"); }

            return Result.Success();
        }
        /// <inheritdoc />
        public ValueTaskSourceStatus GetStatus(short token) => ValueTaskSourceStatus.Succeeded;
        /// <inheritdoc />
        public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags) =>
            throw new InvalidOperationException("Already completed");
    }

    private static PipelineControl Control(PipelineControlOutcome outcome, Result result) => outcome switch
    {
        PipelineControlOutcome.Skip => PipelineControl.Skip(result),
        PipelineControlOutcome.Retry => PipelineControl.Retry(result),
        PipelineControlOutcome.Break => PipelineControl.Break(result),
        PipelineControlOutcome.Terminate => PipelineControl.Terminate(result),
        _ => PipelineControl.Continue(result),
    };
    private static TestContext Context()
    {
        var context = new TestContext();
        context.Pipeline.Name = "Import";
        context.Pipeline.ExecutionId = Guid.NewGuid();
        context.Pipeline.CorrelationId = "pipeline-correlation";
        return context;
    }
    private static IPipelineStepDefinition Step(string key) => new PipelineStepDefinitionModel(key, PipelineStepSourceKind.Type, null, null, null, null);
    private static async Task<ServiceProvider> Services(bool enabled = true)
    {
        var collection = new ServiceCollection();
        collection.AddLogging();
        collection.AddProfiling(o => o.Enabled(enabled)).WithOperationProfiling();
        var provider = collection.BuildServiceProvider();
        if (enabled) { await provider.GetRequiredService<OperationProfilingWriterService>().TickAsync(); }

        return provider;
    }
    private static async Task<OperationProfilingRecord> Stored(IServiceProvider provider, Guid id)
    {
        await provider.GetRequiredService<OperationProfilingWriterService>().TickAsync();
        return (await provider.GetRequiredService<IOperationProfilingStore>().FindAsync(id)).Value.ShouldNotBeNull();
    }
    private sealed class TestContext : PipelineContextBase;
}
