// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Benchmarks;

using BenchmarkDotNet.Attributes;
using BridgingIT.DevKit.Application.Orchestrations;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

[MemoryDiagnoser]
public class OrchestrationsBenchmarks
{
    private ServiceProvider serviceProvider;
    private IOrchestrationService service;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOrchestrations()
            .WithOrchestration<CompletedBenchmarkOrchestration>();

        this.serviceProvider = services.BuildServiceProvider();
        this.service = this.serviceProvider.GetRequiredService<IOrchestrationService>();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.serviceProvider?.Dispose();
        this.serviceProvider = null;
        this.service = null;
    }

    [Benchmark]
    public async Task ExecuteInline_Completes()
    {
        var result = await this.service.ExecuteAsync<CompletedBenchmarkOrchestration, CompletedBenchmarkData>(new CompletedBenchmarkData());
        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Errors.FirstOrDefault()?.Message ?? "The benchmark orchestration execution failed.");
        }
    }

    [Benchmark]
    public async Task DispatchAndWait_Completes()
    {
        var result = await this.service.DispatchAndWaitAsync<CompletedBenchmarkOrchestration, CompletedBenchmarkData>(new CompletedBenchmarkData());
        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Errors.FirstOrDefault()?.Message ?? "The benchmark orchestration dispatch failed.");
        }
    }

    private sealed class CompletedBenchmarkData : IOrchestrationData
    {
        public int Counter { get; set; }
    }

    private sealed class CompletedBenchmarkOrchestration : Orchestration<CompletedBenchmarkData>
    {
        protected override void Define(IOrchestrationBuilder<CompletedBenchmarkData> builder)
        {
            builder
                .State("Start", state => state
                    .Activity((context, cancellationToken) =>
                    {
                        context.Data.Counter++;
                        return Task.FromResult(OrchestrationOutcome.Continue());
                    })
                    .TransitionTo("Done"))
                .State("Done", state => state
                    .Activity((context, cancellationToken) =>
                    {
                        context.Data.Counter++;
                        return Task.FromResult(OrchestrationOutcome.Continue());
                    })
                    .Complete());
        }
    }
}