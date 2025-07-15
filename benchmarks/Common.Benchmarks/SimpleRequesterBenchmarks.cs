// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.Benchmarks;

using BenchmarkDotNet.Attributes;
using BridgingIT.DevKit.Common.Utilities;

[MemoryDiagnoser]
public class SimpleRequesterBenchmarks
{
    private SimpleRequester requester;

    [GlobalSetup]
    public void Setup()
    {
        this.requester = new SimpleRequesterBuilder().Build();
        this.requester.RegisterHandler<MyTestSimpleRequest, string>(async (r, ct) => await Task.FromResult("Processed"));
    }

    [Benchmark]
    public async Task SimpleRequester_Baseline()
    {
        var request = new MyTestSimpleRequest();
        var response = await this.requester.SendAsync<MyTestSimpleRequest, string>(request);
    }
}

public class MyTestSimpleRequest : ISimpleRequest<string> { }