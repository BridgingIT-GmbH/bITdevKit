// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

public abstract class TestsBase : IDisposable
{
    protected TestsBase(ITestOutputHelper output)
        : this(output, null) { }

    protected TestsBase(ITestOutputHelper output, Action<IServiceCollection> services)
    {
        this.Output = output;
        this.Services.AddLogging(c => c.AddProvider(new XunitLoggerProvider(output)));
        services?.Invoke(this.Services);
        this.ServiceProvider = this.Services.BuildServiceProvider();
    }

    protected ITestOutputHelper Output { get; }

    protected IServiceCollection Services { get; } = new ServiceCollection();

    protected ServiceProvider ServiceProvider { get; }

    public void Dispose()
    {
        this.ServiceProvider.Dispose();
    }

    public ILogger CreateLogger()
    {
        return this.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Tests");
    }

    public ILogger<T> CreateLogger<T>()
    {
        return this.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger<T>();
    }

    protected long Benchmark(Action action, int iterations = 1)
    {
        GC.Collect();
        var sw = new Stopwatch();
        action(); // trigger jit before execution

        sw.Start();
        for (var i = 1; i <= iterations; i++)
        {
            action();
        }

        sw.Stop();
        this.Output?.WriteLine(
            $"Benchmark: Execution with #{iterations} iterations took: {sw.Elapsed.TotalMilliseconds}ms\r\n  - Gen-0: {GC.CollectionCount(0)}, Gen-1: {GC.CollectionCount(1)}, Gen-2: {GC.CollectionCount(2)}",
            sw.ElapsedMilliseconds);

        return sw.ElapsedMilliseconds;
    }
}