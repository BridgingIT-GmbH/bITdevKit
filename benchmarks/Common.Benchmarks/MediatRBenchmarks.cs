// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.Benchmarks;

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using MediatR;

[MemoryDiagnoser]
public class MediatRBenchmarks
{
    private IServiceProvider serviceProvider;
    private IMediator mediator;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<MediatRTestRequest>());
        this.serviceProvider = services.BuildServiceProvider();
        this.mediator = this.serviceProvider.GetRequiredService<IMediator>();
    }

    [Benchmark]
    public async Task MediatR_Baseline()
    {
        var request = new MediatRTestRequest();
        var result = await this.mediator.Send(request);
    }
}
