// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

/// <summary>
/// Provides a base class for unit tests that includes common setup, configuration,
/// and utility functions such as logging and benchmarking.
/// </summary>
public abstract class TestsBase : IDisposable
{
    /// <summary>
    /// An abstract base class for test classes, utilized to provide common functionality
    /// and setup for unit tests. This includes configuring dependency injection and logging.
    /// </summary>
    protected TestsBase(ITestOutputHelper output, Action<IServiceCollection> services = null)
    {
        this.Output = output;
        this.Services.AddLogging(c => c.AddProvider(new XunitLoggerProvider(output)));
        services?.Invoke(this.Services);
        this.ServiceProvider = this.Services.BuildServiceProvider();
    }

    /// <summary>
    /// Gets the output helper for writing test results to the test output.
    /// </summary>
    protected ITestOutputHelper Output { get; }

    /// Gets the IServiceCollection instance used to configure dependency injection services.
    /// This collection is used to register and configure dependencies for the test cases.
    /// By default, it includes a logging provider that integrates with xUnit's output.
    /// Additional services can be registered by providing an Action<IServiceCollection> instance
    /// to the TestsBase constructor.
    protected IServiceCollection Services { get; } = new ServiceCollection();

    /// <summary>
    /// Provides access to the <see cref="ServiceProvider"/> instance used for resolving dependencies
    /// during integration tests. This property facilitates dependency injection of required services
    /// into test classes, enabling the testing of application's behavior in an environment that closely
    /// resembles the real runtime scenario.
    /// </summary>
    protected ServiceProvider ServiceProvider { get; }

    /// <summary>
    /// Disposes the resources used by the <see cref="TestsBase"/> class.
    /// </summary>
    /// <remarks>
    /// This method is responsible for disposing the <see cref="ServiceProvider"/> to release
    /// the resources and services that were instantiated during the test lifecycle.
    /// It implements the <see cref="IDisposable.Dispose"/> method.
    /// </remarks>
    public void Dispose()
    {
        this.ServiceProvider.Dispose();
    }

    /// <summary>
    /// Creates a logger with the category name "Tests".
    /// </summary>
    /// <returns>
    /// An instance of <see cref="ILogger"/> configured with the "Tests" category name.
    /// </returns>
    protected ILogger CreateLogger()
    {
        return this.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Tests");
    }

    /// <summary>
    /// Creates a logger instance with the category name "Tests".
    /// </summary>
    /// <returns>
    /// An <see cref="ILogger"/> instance configured to log with the category name "Tests".
    /// </returns>
    public ILogger<T> CreateLogger<T>()
    {
        return this.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger<T>();
    }

    /// <summary>
    /// Benchmarks the execution time of a given action.
    /// </summary>
    /// <param name="action">The action to be benchmarked.</param>
    /// <param name="iterations">The number of times to execute the action. Defaults to 1.</param>
    /// <returns>The total elapsed time in milliseconds for the specified number of iterations.</returns>
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