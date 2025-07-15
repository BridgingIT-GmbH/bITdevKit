// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.Benchmarks;

using BenchmarkDotNet.Attributes;
using BridgingIT.DevKit.Common.Utilities;

[MemoryDiagnoser]
public class SimpleNotifierBenchmarks
{
    private SimpleNotifier notifier;

    [GlobalSetup]
    public void Setup()
    {
        this.notifier = new SimpleNotifierBuilder().Build();
        this.notifier.Subscribe<MyTestSimpleNotification>(async (n, ct) => await Task.CompletedTask);
    }

    [Benchmark]
    public async Task SimpleNotifier_Baseline()
    {
        var notification = new MyTestSimpleNotification();
        await this.notifier.PublishAsync(notification);
    }
}

public class MyTestSimpleNotification : ISimpleNotification { }