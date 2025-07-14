// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.Benchmarks;

using BenchmarkDotNet.Attributes;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

[MemoryDiagnoser]
public class NotifierBenchmarks
{
    private IServiceProvider serviceProvider;
    private INotifier notifier;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNotifier().AddHandlers();
        serviceProvider = services.BuildServiceProvider();
        notifier = serviceProvider.GetRequiredService<INotifier>();
    }

    [Benchmark]
    public async Task Notifier_Baseline()
    {
        var notification = new MyTestNotification();
        var result = await notifier.PublishAsync(notification);
    }
}

public class MyTestNotification : NotificationBase { }
public class MyTestNotificationHandler : NotificationHandlerBase<MyTestNotification>
{
    protected override Task<Result> HandleAsync(MyTestNotification notification, PublishOptions options, CancellationToken cancellationToken)
        => Task.FromResult(Result.Success());
}