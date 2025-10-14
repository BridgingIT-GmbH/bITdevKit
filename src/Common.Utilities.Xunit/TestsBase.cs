// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using Xunit.Abstractions;

/// <summary>
/// Provides a base class for unit tests that includes common setup, configuration,
/// and utility functions such as logging, benchmarking, and service management.
/// </summary>
public abstract class TestsBase : IDisposable
{
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestsBase"/> class.
    /// </summary>
    /// <param name="output">The test output helper for writing test results.</param>
    /// <param name="services">An optional action to configure initial services.</param>
    protected TestsBase(ITestOutputHelper output, Action<IServiceCollection> services = null)
    {
        this.Output = output ?? throw new ArgumentNullException(nameof(output));
        this.TestContext = new TestContext
        {
            TestName = output.GetType().GetField("_test", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(output)?.ToString() ?? "Unknown"
        };

        this.ConfigureLogging(this.Services);

        services?.Invoke(this.Services);
        this.EnsureRequiredServices();
        this.ServiceProvider = this.Services.BuildServiceProvider();
    }

    /// <summary>
    /// Gets the output helper for writing test results to the test output.
    /// </summary>
    protected ITestOutputHelper Output { get; }

    /// <summary>
    /// Gets the test context metadata, such as test name or category.
    /// </summary>
    protected TestContext TestContext { get; }

    /// <summary>
    /// Gets the IServiceCollection instance used to configure dependency injection services.
    /// By default, it includes a logging provider that integrates with xUnit's output.
    /// Additional services can be registered via the constructor or AddServices method.
    /// </summary>
    protected IServiceCollection Services { get; } = new ServiceCollection();

    /// <summary>
    /// Provides access to the <see cref="ServiceProvider"/> instance used for resolving dependencies
    /// during tests. This property facilitates dependency injection of required services.
    /// </summary>
    protected ServiceProvider ServiceProvider { get; private set; }

    /// <summary>
    /// Adds additional services to the IServiceCollection and rebuilds the ServiceProvider.
    /// </summary>
    /// <param name="services">An action to configure additional services.</param>
    protected void RegisterServices(Action<IServiceCollection> services)
    {
        services?.Invoke(this.Services);
        this.EnsureRequiredServices();
        this.RebuildServiceProvider();
    }

    /// <summary>
    /// Adds additional services asynchronously to the IServiceCollection and rebuilds the ServiceProvider.
    /// </summary>
    /// <param name="services">A function to configure additional services asynchronously.</param>
    protected async Task RegisterServicesAsync(Func<IServiceCollection, Task> services)
    {
        if (services != null)
        {
            await services(this.Services).ConfigureAwait(false);
        }
        this.EnsureRequiredServices();
        this.RebuildServiceProvider();
    }

    /// <summary>
    /// Resets the IServiceCollection to a new instance and rebuilds the ServiceProvider with optional services.
    /// </summary>
    /// <param name="services">An optional action to configure new services after reset.</param>
    protected void ResetServices(Action<IServiceCollection> services = null)
    {
        this.ServiceProvider?.Dispose();
        this.Services.Clear();
        this.ConfigureLogging(this.Services);
        services?.Invoke(this.Services);
        this.EnsureRequiredServices();
        this.ServiceProvider = this.Services.BuildServiceProvider();
    }

    /// <summary>
    /// Creates a new service scope for resolving scoped dependencies.
    /// </summary>
    /// <returns>A new <see cref="IServiceScope"/> instance.</returns>
    protected IServiceScope CreateScope()
    {
        return this.ServiceProvider.CreateScope();
    }

    /// <summary>
    /// Virtual method for test-specific setup, called before each test.
    /// </summary>
    protected virtual void SetUp()
    {
    }

    /// <summary>
    /// Virtual method for test-specific cleanup, called after each test.
    /// </summary>
    protected virtual void TearDown()
    {
    }

    /// <summary>
    /// Disposes the resources used by the <see cref="TestsBase"/> class.
    /// </summary>
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the resources used by the <see cref="TestsBase"/> class.
    /// </summary>
    /// <param name="disposing">Indicates whether to dispose managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!this.disposed)
        {
            if (disposing)
            {
                this.TearDown();
                this.ServiceProvider?.Dispose();
            }

            this.disposed = true;
        }
    }

    /// <summary>
    /// Creates a logger with the category name "Tests".
    /// </summary>
    /// <returns>An instance of <see cref="ILogger"/> configured with the "Tests" category name.</returns>
    protected ILogger CreateLogger()
    {
        return this.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Tests");
    }

    /// <summary>
    /// Creates a logger instance with the specified category name.
    /// </summary>
    /// <returns>An <see cref="ILogger"/> instance for the specified type.</returns>
    protected ILogger<T> CreateLogger<T>()
    {
        return this.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger<T>();
    }

    /// <summary>
    /// Benchmarks the execution time of a synchronous action.
    /// </summary>
    /// <param name="action">The action to be benchmarked.</param>
    /// <param name="iterations">The number of times to execute the action. Defaults to 1.</param>
    /// <returns>The total elapsed time in milliseconds for the specified number of iterations.</returns>
    protected long Benchmark(Action action, int iterations = 1)
    {
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        if (iterations < 1)
        {
            throw new ArgumentException("Iterations must be at least 1.", nameof(iterations));
        }

        GC.Collect();
        var sw = new Stopwatch();
        action(); // Trigger JIT compilation

        sw.Start();
        for (var i = 1; i <= iterations; i++)
        {
            action();
        }
        sw.Stop();

        this.LogBenchmarkResults("Action", iterations, sw.Elapsed);

        return (long)sw.Elapsed.TotalMilliseconds;
    }

    /// <summary>
    /// Benchmarks the execution time of an asynchronous action.
    /// </summary>
    /// <param name="action">The asynchronous action to be benchmarked.</param>
    /// <param name="iterations">The number of times to execute the action. Defaults to 1.</param>
    /// <returns>The total elapsed time in milliseconds for the specified number of iterations.</returns>
    protected async Task<long> BenchmarkAsync(Func<Task> action, int iterations = 1)
    {
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        if (iterations < 1)
        {
            throw new ArgumentException("Iterations must be at least 1.", nameof(iterations));
        }

        GC.Collect();
        var sw = new Stopwatch();
        await action().ConfigureAwait(false); // Trigger JIT compilation

        sw.Start();
        for (var i = 1; i <= iterations; i++)
        {
            await action().ConfigureAwait(false);
        }
        sw.Stop();

        this.LogBenchmarkResults("Async Action", iterations, sw.Elapsed);

        return (long)sw.Elapsed.TotalMilliseconds;
    }

    private void ConfigureLogging(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddProvider(new XunitLoggerProvider(this.Output));
        });
    }

    private void EnsureRequiredServices()
    {
        if (!this.Services.Any(s => s.ServiceType == typeof(ILoggerFactory)))
        {
            this.ConfigureLogging(this.Services);
        }
    }

    private void RebuildServiceProvider()
    {
        this.ServiceProvider?.Dispose();
        this.ServiceProvider = this.Services.BuildServiceProvider();
    }

    private void LogBenchmarkResults(string actionType, int iterations, TimeSpan elapsed)
    {
        var memory = GC.GetTotalMemory(false) / 1024.0 / 1024.0; // MB
        this.Output?.WriteLine(
            $"Benchmark: {actionType} with #{iterations} iterations took: {elapsed.TotalMilliseconds:F2}ms\r\n" +
            $"  - Gen-0: {GC.CollectionCount(0)}, Gen-1: {GC.CollectionCount(1)}, Gen-2: {GC.CollectionCount(2)}\r\n" +
            $"  - Memory: {memory:F2} MB");
    }
}

/// <summary>
/// Represents metadata about the current test context.
/// </summary>
public class TestContext
{
    public string TestName { get; set; }

    public Dictionary<string, string> Metadata { get; } = [];
}