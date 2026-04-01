// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

[UnitTest("Common")]
public class PipelineCodeGenExecutionTests
{
    [Fact]
    public async Task GeneratedContextPipeline_UsesGeneratedStepsHooksBehaviorsAndManualExtension()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<GeneratedDependency>();
        services.AddSingleton<GeneratedProbe>();
        services.AddPipelines()
            .WithPipeline<GeneratedContextPipeline>();

        var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredService<IPipelineFactory>()
            .Create<GeneratedContextPipeline, GeneratedContext>();
        var dependency = provider.GetRequiredService<GeneratedDependency>();
        var probe = provider.GetRequiredService<GeneratedProbe>();
        var context = new GeneratedContext();

        var definition = new GeneratedContextPipeline().Build();
        var result = await pipeline.ExecuteAsync(context);

        definition.Name.ShouldBe("generated-context");
        definition.Steps.Select(s => s.Name).ShouldBe(["load", "validate", "finish-generated", "manual-generated"]);
        definition.HookTypes.ShouldContain(typeof(GeneratedHook));
        definition.BehaviorTypes.ShouldContain(typeof(GeneratedBehavior));

        result.IsSuccess.ShouldBeTrue();
        result.Messages.ShouldContain("loaded");
        result.Messages.ShouldContain("finished");
        result.Messages.ShouldContain("generated-behavior");
        dependency.Calls.ShouldBe(["load", "validate", "finish"]);
        context.Validated.ShouldBeTrue();
        context.ManuallyExtended.ShouldBeTrue();
        probe.StartingCount.ShouldBe(1);
        probe.CompletedCount.ShouldBe(1);
        probe.PipelineBehaviorCount.ShouldBe(1);
        probe.StepBehaviorCount.ShouldBe(4);
    }

    [Fact]
    public async Task GeneratedNoContextPipeline_ExecutesThroughStandardFactoryRegistration()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<NoContextDependency>();
        services.AddPipelines()
            .WithPipeline<GeneratedNoContextPipeline>();

        var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredService<IPipelineFactory>()
            .Create<GeneratedNoContextPipeline>();
        var dependency = provider.GetRequiredService<NoContextDependency>();

        var definition = new GeneratedNoContextPipeline().Build();
        var result = await pipeline.ExecuteAsync();

        definition.Name.ShouldBe("generated-no-context");
        definition.Steps.Select(s => s.Name).ShouldBe(["prepare", "complete"]);
        result.IsSuccess.ShouldBeTrue();
        result.Messages.ShouldContain("done");
        dependency.ExecutionCount.ShouldBe(2);
    }

    public sealed class GeneratedContext : PipelineContextBase
    {
        public bool Validated { get; set; }

        public bool ManuallyExtended { get; set; }
    }

    public sealed class GeneratedDependency
    {
        public List<string> Calls { get; } = [];
    }

    public sealed class GeneratedProbe
    {
        public int StartingCount { get; set; }

        public int CompletedCount { get; set; }

        public int PipelineBehaviorCount { get; set; }

        public int StepBehaviorCount { get; set; }
    }

    public sealed class NoContextDependency
    {
        public int ExecutionCount { get; set; }
    }

    public sealed class GeneratedHook(GeneratedProbe probe) : PipelineHook<GeneratedContext>
    {
        public override ValueTask OnPipelineStartingAsync(GeneratedContext context, CancellationToken cancellationToken)
        {
            probe.StartingCount++;
            return ValueTask.CompletedTask;
        }

        public override ValueTask OnPipelineCompletedAsync(GeneratedContext context, Result result, CancellationToken cancellationToken)
        {
            probe.CompletedCount++;
            return ValueTask.CompletedTask;
        }
    }

    public sealed class GeneratedBehavior(GeneratedProbe probe) : IPipelineBehavior<GeneratedContext>
    {
        public async ValueTask<Result> ExecuteAsync(GeneratedContext context, Func<ValueTask<Result>> next, CancellationToken cancellationToken)
        {
            probe.PipelineBehaviorCount++;
            return (await next()).WithMessage("generated-behavior");
        }

        public async ValueTask<PipelineControl> ExecuteStepAsync(
            GeneratedContext context,
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

[Pipeline(typeof(PipelineCodeGenExecutionTests.GeneratedContext))]
[PipelineHook(typeof(PipelineCodeGenExecutionTests.GeneratedHook))]
[PipelineBehavior(typeof(PipelineCodeGenExecutionTests.GeneratedBehavior))]
public partial class GeneratedContextPipeline
{
    [PipelineStep(20)]
    public void Validate(PipelineCodeGenExecutionTests.GeneratedContext context, PipelineCodeGenExecutionTests.GeneratedDependency dependency)
    {
        dependency.Calls.Add("validate");
        context.Validated = true;
    }

    [PipelineStep(10)]
    public async Task<Result> LoadAsync(
        PipelineCodeGenExecutionTests.GeneratedContext context,
        Result result,
        PipelineCodeGenExecutionTests.GeneratedDependency dependency,
        CancellationToken cancellationToken)
    {
        dependency.Calls.Add("load");
        await Task.Yield();
        return result.WithMessage("loaded");
    }

    [PipelineStep(30, Name = "finish-generated")]
    public PipelineControl Finish(
        PipelineCodeGenExecutionTests.GeneratedContext context,
        Result result,
        PipelineCodeGenExecutionTests.GeneratedDependency dependency)
    {
        dependency.Calls.Add("finish");
        return PipelineControl.Continue(result.WithMessage("finished"));
    }

    partial void OnConfigureGenerated(IPipelineDefinitionBuilder<PipelineCodeGenExecutionTests.GeneratedContext> builder)
    {
        builder.AddStep(
            context => context.ManuallyExtended = true,
            name: "manual-generated");
    }
}

[Pipeline]
public partial class GeneratedNoContextPipeline
{
    [PipelineStep(10)]
    public void Prepare(PipelineCodeGenExecutionTests.NoContextDependency dependency)
    {
        dependency.ExecutionCount++;
    }

    [PipelineStep(20)]
    public async Task<PipelineControl> CompleteAsync(
        Result result,
        PipelineCodeGenExecutionTests.NoContextDependency dependency,
        CancellationToken cancellationToken)
    {
        dependency.ExecutionCount++;
        await Task.Yield();
        return PipelineControl.Break(result.WithMessage("done"));
    }
}
