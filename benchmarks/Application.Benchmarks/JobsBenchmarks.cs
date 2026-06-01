// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Benchmarks;

using BenchmarkDotNet.Attributes;
using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

[MemoryDiagnoser]
public class JobsBenchmarks
{
    private ServiceProvider serviceProvider;
    private IJobSchedulerService scheduler;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddJobScheduler()
            .WithBackgroundExecution(options => options.EnableBackgroundExecution = false)
            .WithJob<CompletedBenchmarkJob>("completed-job", job => job
                .Description("Completes immediately for benchmark measurements.")
                .AddTrigger("manual", trigger => trigger.Manual()))
            .WithJob("completed-inline-job", job => job
                .WithDescription("Completes immediately through the inline job path.")
                .Execute((context, cancellationToken) => Task.FromResult(Result.Success()))
                .AddTrigger("manual", trigger => trigger.Manual()));

        this.serviceProvider = services.BuildServiceProvider();
        this.scheduler = this.serviceProvider.GetRequiredService<IJobSchedulerService>();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.serviceProvider?.Dispose();
        this.serviceProvider = null;
        this.scheduler = null;
    }

    [Benchmark]
    public async Task DispatchAndWait_ClassJob_Completes()
    {
        var result = await this.scheduler.DispatchAndWaitAsync<CompletedBenchmarkJob>();
        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Errors.FirstOrDefault()?.Message ?? "The benchmark job execution failed.");
        }
    }

    [Benchmark]
    public async Task DispatchAndWait_InlineJob_Completes()
    {
        var result = await this.scheduler.DispatchAndWaitAsync("completed-inline-job");
        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Errors.FirstOrDefault()?.Message ?? "The inline benchmark job execution failed.");
        }
    }

    private sealed class CompletedBenchmarkJob : JobBase
    {
        public override Task<Result> ExecuteAsync(
            IJobExecutionContext<Unit> context,
            CancellationToken cancellationToken = default)
        {
            context.Messages.Add("completed");
            return Task.FromResult(Result.Success());
        }
    }
}