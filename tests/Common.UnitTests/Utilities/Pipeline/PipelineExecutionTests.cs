// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using System.Diagnostics;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

[UnitTest("Common")]
public class PipelineExecutionTests
{
    [Fact]
    public async Task ExecuteAsync_ClassBasedSteps_CarryResultAndUpdateContextMetrics()
    {
        var services = CreateServices();
        services.AddPipelines()
            .WithPipeline<ExecutionPipeline>();

        var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredService<IPipelineFactory>()
            .Create<ExecutionPipeline, TestContext>();
        var context = new TestContext();

        var result = await pipeline.ExecuteAsync(context);

        result.IsSuccess.ShouldBeTrue();
        result.Messages.ShouldContain("sync");
        result.Messages.ShouldContain("async");
        context.AsyncStepExecuted.ShouldBeTrue();
        context.Pipeline.Name.ShouldBe("execution");
        context.Pipeline.ExecutionId.ShouldNotBe(Guid.Empty);
        context.Pipeline.StartedUtc.ShouldNotBe(default);
        context.Pipeline.CompletedUtc.ShouldNotBeNull();
        context.Pipeline.ExecutedStepCount.ShouldBe(2);
        context.Pipeline.TotalStepCount.ShouldBe(2);
        context.Pipeline.CurrentStepName.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ThrownStepException_ReturnsFailedResultWithExceptionError()
    {
        var services = CreateServices();
        services.AddPipelines()
            .WithPipeline<ThrowingPipeline>();

        var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredService<IPipelineFactory>()
            .Create<ThrowingPipeline, TestContext>();

        var result = await pipeline.ExecuteAsync(new TestContext());

        result.IsFailure.ShouldBeTrue();
        result.Errors.OfType<ExceptionError>().Any().ShouldBeTrue();
        result.Messages.ShouldContain("boom");
    }

    [Fact]
    public async Task ExecuteAsync_Retry_ReexecutesStepUntilSuccess()
    {
        var services = CreateServices();
        services.AddPipelines()
            .WithPipeline<RetryPipeline>();

        var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredService<IPipelineFactory>()
            .Create<RetryPipeline, TestContext>();
        var context = new TestContext();

        var result = await pipeline.ExecuteAsync(context, builder => builder.MaxRetryAttemptsPerStep(3));

        result.IsSuccess.ShouldBeTrue();
        result.Messages.ShouldContain("retried");
        result.Messages.ShouldContain("retried-success");
        context.RetryAttempts.ShouldBe(2);
        context.Pipeline.ExecutedStepCount.ShouldBe(2);
    }

    [Fact]
    public async Task ExecuteAsync_Break_StopsRemainingSteps()
    {
        var services = CreateServices();
        services.AddPipelines()
            .WithPipeline<BreakPipeline>();

        var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredService<IPipelineFactory>()
            .Create<BreakPipeline, TestContext>();
        var context = new TestContext();

        var result = await pipeline.ExecuteAsync(context);

        result.IsSuccess.ShouldBeTrue();
        context.AfterBreakExecuted.ShouldBeFalse();
        context.Pipeline.ExecutedStepCount.ShouldBe(2);
    }

    [Fact]
    public async Task ExecuteAsync_InlineSteps_WorkForSyncAsyncAndContextAwareDelegates()
    {
        var services = CreateServices();
        var syncExecuted = false;
        var asyncExecuted = false;

        services.AddPipelines()
            .WithPipeline<TestContext>("inline", builder => builder
                .AddStep(() => syncExecuted = true)
                .AddAsyncStep(async () =>
                {
                    await Task.Yield();
                    asyncExecuted = true;
                })
                .AddStep(context => context.ContextStepCount++)
                .AddAsyncStep(async context =>
                {
                    await Task.Yield();
                    context.ContextStepCount++;
                })
                .AddStep(execution => execution.Continue(execution.Result.WithMessage("advanced-inline"))));

        var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredService<IPipelineFactory>()
            .Create<TestContext>("inline");
        var context = new TestContext();

        var result = await pipeline.ExecuteAsync(context);

        syncExecuted.ShouldBeTrue();
        asyncExecuted.ShouldBeTrue();
        context.ContextStepCount.ShouldBe(2);
        result.Messages.ShouldContain("advanced-inline");
        context.Pipeline.ExecutedStepCount.ShouldBe(5);
    }

    [Fact]
    public async Task ExecuteAsync_BaseContextHookAndBehavior_AreAppliedToDerivedContextPipeline()
    {
        var services = CreateServices();
        var probe = new ExecutionProbe();
        services.AddSingleton(probe);

        services.AddPipelines()
            .WithPipeline<TestContext>("hook-behavior", builder => builder
                .AddStep(() => { })
                .AddHook<ProbeHook>()
                .AddBehavior<ProbeBehavior>());

        var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredService<IPipelineFactory>()
            .Create<TestContext>("hook-behavior");

        var result = await pipeline.ExecuteAsync(new TestContext());

        result.IsSuccess.ShouldBeTrue();
        result.Messages.ShouldContain("behavior");
        probe.PipelineStartingCount.ShouldBe(1);
        probe.PipelineCompletedCount.ShouldBe(1);
        probe.BehaviorCount.ShouldBe(1);
        probe.StepBehaviorCount.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAndForgetAsync_TracksExecutionAndInvokesCompletionCallback()
    {
        var services = CreateServices();
        services.AddPipelines()
            .WithPipeline<BackgroundPipeline>();

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IPipelineFactory>();
        var tracker = provider.GetRequiredService<IPipelineExecutionTracker>();
        var pipeline = factory.Create<BackgroundPipeline, TestContext>();
        var completionSource = new TaskCompletionSource<PipelineCompletion>(TaskCreationOptions.RunContinuationsAsynchronously);

        var handle = await pipeline.ExecuteAndForgetAsync(
            new TestContext(),
            builder => builder.WhenCompleted(completion =>
            {
                completionSource.TrySetResult(completion);
                return ValueTask.CompletedTask;
            }));

        PipelineExecutionSnapshot snapshot = null;
        for (var i = 0; i < 40; i++)
        {
            snapshot = await tracker.GetAsync(handle.ExecutionId);
            if (snapshot?.Status == PipelineExecutionStatus.Completed)
            {
                break;
            }

            await Task.Delay(25);
        }

        var completion = await completionSource.Task.WaitAsync(TimeSpan.FromSeconds(5));

        snapshot.ShouldNotBeNull();
        snapshot.Status.ShouldBe(PipelineExecutionStatus.Completed);
        snapshot.ExecutionId.ShouldBe(handle.ExecutionId);
        snapshot.Result.IsSuccess.ShouldBeTrue();
        completion.ExecutionId.ShouldBe(handle.ExecutionId);
        completion.Status.ShouldBe(PipelineExecutionStatus.Completed);
    }

    [Fact]
    public async Task ExecuteAsync_TracingBehavior_CreatesPipelineAndStepActivities()
    {
        using var source = new ActivitySource("trace-pipeline");
        var startedActivities = new List<string>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = activitySource => activitySource.Name == "trace-pipeline",
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => startedActivities.Add(activity.OperationName)
        };

        ActivitySource.AddActivityListener(listener);

        var services = CreateServices();
        services.AddSingleton(source);
        services.AddPipelines()
            .WithPipeline<TestContext>("trace-pipeline", builder => builder
                .AddStep(() => { })
                .AddBehavior<PipelineTracingBehavior>());

        var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredService<IPipelineFactory>()
            .Create<TestContext>("trace-pipeline");

        var result = await pipeline.ExecuteAsync(new TestContext());

        result.IsSuccess.ShouldBeTrue();
        startedActivities.ShouldContain("PIPELINE Execute trace-pipeline");
        startedActivities.ShouldContain("PIPELINE STEP inline-step-1");
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return services;
    }

    public sealed class TestContext : PipelineContextBase
    {
        public bool AsyncStepExecuted { get; set; }

        public int RetryAttempts { get; set; }

        public bool AfterBreakExecuted { get; set; }

        public int ContextStepCount { get; set; }
    }

    public sealed class ExecutionPipeline : PipelineDefinition<TestContext>
    {
        protected override void Configure(IPipelineDefinitionBuilder<TestContext> builder)
        {
            builder.AddStep<AppendSyncStep>()
                .AddStep<AppendAsyncStep>();
        }
    }

    public sealed class ThrowingPipeline : PipelineDefinition<TestContext>
    {
        protected override void Configure(IPipelineDefinitionBuilder<TestContext> builder)
        {
            builder.AddStep<ThrowingStep>();
        }
    }

    public sealed class RetryPipeline : PipelineDefinition<TestContext>
    {
        protected override void Configure(IPipelineDefinitionBuilder<TestContext> builder)
        {
            builder.AddStep<RetryStep>();
        }
    }

    public sealed class BreakPipeline : PipelineDefinition<TestContext>
    {
        protected override void Configure(IPipelineDefinitionBuilder<TestContext> builder)
        {
            builder.AddStep<AppendSyncStep>()
                .AddStep<BreakStep>()
                .AddStep<AfterBreakStep>();
        }
    }

    public sealed class BackgroundPipeline : PipelineDefinition<TestContext>
    {
        protected override void Configure(IPipelineDefinitionBuilder<TestContext> builder)
        {
            builder.AddStep<BackgroundStep>();
        }
    }

    public sealed class AppendSyncStep : PipelineStep<TestContext>
    {
        protected override PipelineControl Execute(TestContext context, Result result, PipelineExecutionOptions options)
        {
            return PipelineControl.Continue(result.WithMessage("sync"));
        }
    }

    public sealed class AppendAsyncStep : AsyncPipelineStep<TestContext>
    {
        protected override ValueTask<PipelineControl> ExecuteAsync(TestContext context, Result result, PipelineExecutionOptions options, CancellationToken cancellationToken)
        {
            context.AsyncStepExecuted = true;
            return ValueTask.FromResult(PipelineControl.Continue(result.WithMessage("async")));
        }
    }

    public sealed class ThrowingStep : PipelineStep<TestContext>
    {
        protected override PipelineControl Execute(TestContext context, Result result, PipelineExecutionOptions options)
        {
            throw new InvalidOperationException("boom");
        }
    }

    public sealed class RetryStep : PipelineStep<TestContext>
    {
        protected override PipelineControl Execute(TestContext context, Result result, PipelineExecutionOptions options)
        {
            context.RetryAttempts++;
            return context.RetryAttempts < 2
                ? PipelineControl.Retry(result.WithMessage("retried"), "retry")
                : PipelineControl.Continue(result.WithMessage("retried-success"));
        }
    }

    public sealed class BreakStep : PipelineStep<TestContext>
    {
        protected override PipelineControl Execute(TestContext context, Result result, PipelineExecutionOptions options)
        {
            return PipelineControl.Break(result.WithMessage("break"));
        }
    }

    public sealed class AfterBreakStep : PipelineStep<TestContext>
    {
        protected override PipelineControl Execute(TestContext context, Result result, PipelineExecutionOptions options)
        {
            context.AfterBreakExecuted = true;
            return PipelineControl.Continue(result);
        }
    }

    public sealed class BackgroundStep : AsyncPipelineStep<TestContext>
    {
        protected override async ValueTask<PipelineControl> ExecuteAsync(TestContext context, Result result, PipelineExecutionOptions options, CancellationToken cancellationToken)
        {
            await Task.Delay(50, cancellationToken);
            return PipelineControl.Continue(result.WithMessage("background"));
        }
    }

    public sealed class ExecutionProbe
    {
        public int PipelineStartingCount { get; set; }

        public int PipelineCompletedCount { get; set; }

        public int BehaviorCount { get; set; }

        public int StepBehaviorCount { get; set; }
    }

    public sealed class ProbeHook(ExecutionProbe probe) : PipelineHook<PipelineContextBase>
    {
        public override ValueTask OnPipelineStartingAsync(PipelineContextBase context, CancellationToken cancellationToken)
        {
            probe.PipelineStartingCount++;
            return ValueTask.CompletedTask;
        }

        public override ValueTask OnPipelineCompletedAsync(PipelineContextBase context, Result result, CancellationToken cancellationToken)
        {
            probe.PipelineCompletedCount++;
            return ValueTask.CompletedTask;
        }
    }

    public sealed class ProbeBehavior(ExecutionProbe probe) : IPipelineBehavior<PipelineContextBase>
    {
        public async ValueTask<Result> ExecuteAsync(PipelineContextBase context, Func<ValueTask<Result>> next, CancellationToken cancellationToken)
        {
            probe.BehaviorCount++;
            return (await next()).WithMessage("behavior");
        }

        public async ValueTask<PipelineControl> ExecuteStepAsync(
            PipelineContextBase context,
            IPipelineStepDefinition step,
            Result result,
            Func<ValueTask<PipelineControl>> next,
            CancellationToken cancellationToken)
        {
            probe.StepBehaviorCount++;
            return await next();
        }
    }
}
