// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

using BenchmarkDotNet.Attributes;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

namespace BridgingIT.DevKit.Common.Benchmarks;

[MemoryDiagnoser]
public class PipelineBenchmarks
{
    private ServiceProvider serviceProvider;
    private IPipeline<TestPipelineContext> pipeline;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPipelines()
            .WithPipeline<BenchmarkPipeline>();

        this.serviceProvider = services.BuildServiceProvider();
        this.pipeline = this.serviceProvider.GetRequiredService<IPipelineFactory>()
            .Create<BenchmarkPipeline, TestPipelineContext>();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.serviceProvider?.Dispose();
        this.serviceProvider = null;
        this.pipeline = null;
    }

    [Benchmark]
    public async Task Pipeline_ClassAndAsyncSteps_Completes()
    {
        var result = await this.pipeline.ExecuteAsync(new TestPipelineContext());
        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Errors.FirstOrDefault()?.Message ?? "The pipeline benchmark execution failed.");
        }
    }

    public sealed class TestPipelineContext : PipelineContextBase
    {
        public bool AsyncStepExecuted { get; set; }
    }

    public sealed class BenchmarkPipeline : PipelineDefinition<TestPipelineContext>
    {
        protected override void Configure(IPipelineDefinitionBuilder<TestPipelineContext> builder)
        {
            builder.AddStep<AppendSyncStep>()
                .AddStep<AppendAsyncStep>();
        }
    }

    public sealed class AppendSyncStep : PipelineStep<TestPipelineContext>
    {
        protected override PipelineControl Execute(TestPipelineContext context, Result result, PipelineExecutionOptions options)
        {
            return PipelineControl.Continue(result.WithMessage("sync"));
        }
    }

    public sealed class AppendAsyncStep : AsyncPipelineStep<TestPipelineContext>
    {
        protected override ValueTask<PipelineControl> ExecuteAsync(TestPipelineContext context, Result result, PipelineExecutionOptions options, CancellationToken cancellationToken)
        {
            context.AsyncStepExecuted = true;
            return ValueTask.FromResult(PipelineControl.Continue(result.WithMessage("async")));
        }
    }
}