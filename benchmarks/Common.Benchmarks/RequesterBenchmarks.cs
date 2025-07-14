// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.Benchmarks;

using BenchmarkDotNet.Attributes;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

[MemoryDiagnoser]
public class RequesterBenchmarks
{
    private IServiceProvider serviceProvider;
    private IRequester requester;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRequester().AddHandlers();
        serviceProvider = services.BuildServiceProvider();
        requester = serviceProvider.GetRequiredService<IRequester>();
    }

    [Benchmark]
    public async Task Requester_Baseline()
    {
        var request = new MyTestRequest();
        var result = await requester.SendAsync(request);
    }
}

public class MyTestRequest : RequestBase<string> { }
public class MyTestRequestHandler : RequestHandlerBase<MyTestRequest, string>
{
    protected override Task<Result<string>> HandleAsync(MyTestRequest request, SendOptions options, CancellationToken cancellationToken)
        => Task.FromResult(Result<string>.Success("Test"));
}

public class MediatRTestRequest : MediatR.IRequest<string> { }
public class MediatRTestRequestHandler : MediatR.IRequestHandler<MediatRTestRequest, string>
{
    public Task<string> Handle(MediatRTestRequest request, CancellationToken cancellationToken)
        => Task.FromResult("Test");
}