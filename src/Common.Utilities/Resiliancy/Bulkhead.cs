namespace BridgingIT.DevKit.Common.Utilities;

using BridgingIT.DevKit.Common.Resiliancy;
using Microsoft.Extensions.Logging;

/// <summary>
/// Implements the bulkhead pattern to isolate failures by limiting the number of concurrent operations.
/// </summary>
public class Bulkhead
{
    private readonly SemaphoreSlim semaphore;
    private readonly int maxConcurrency;
    private readonly bool handleErrors;
    private readonly ILogger logger;
    private readonly IProgress<BulkheadProgress> progress;
    private readonly Queue<Task> queuedTasks = [];
    private readonly Lock lockObject = new();

    /// <summary>
    /// Initializes a new instance of the Bulkhead class with the specified concurrency limit.
    /// </summary>
    /// <param name="maxConcurrency">The maximum number of concurrent operations allowed.</param>
    /// <param name="handleErrors">If true, catches and logs exceptions from the action; otherwise, throws them. Defaults to false.</param>
    /// <param name="logger">An optional logger to log errors if handleErrors is true. Defaults to null.</param>
    /// <param name="progress">An optional progress reporter for bulkhead operations. Defaults to null.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if maxConcurrency is less than 1.</exception>
    /// <example>
    /// <code>
    /// var bulkhead = new Bulkhead(5, progress: new Progress<BulkheadProgress>(p => Console.WriteLine($"Progress: {p.Status}, Concurrency: {p.CurrentConcurrency}/{p.MaxConcurrency}, Queued: {p.QueuedTasks}")));
    /// await bulkhead.ExecuteAsync(async ct => await Task.Delay(100, ct), CancellationToken.None);
    /// </code>
    /// </example>
    public Bulkhead(
        int maxConcurrency,
        bool handleErrors = false,
        ILogger logger = null,
        IProgress<BulkheadProgress> progress = null)
    {
        if (maxConcurrency < 1)
            throw new ArgumentOutOfRangeException(nameof(maxConcurrency), "Maximum concurrency must be at least 1.");

        this.maxConcurrency = maxConcurrency;
        this.semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        this.handleErrors = handleErrors;
        this.logger = logger;
        this.progress = progress;
    }

    /// <summary>
    /// Executes the specified action within the bulkhead limit, queuing if the limit is reached.
    /// </summary>
    /// <param name="action">The asynchronous action to execute, accepting a CancellationToken.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <param name="progress">An optional progress reporter for bulkhead operations. Defaults to null.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
    /// <example>
    /// <code>
    /// var cts = new CancellationTokenSource();
    /// var progress = new Progress<BulkheadProgress>(p => Console.WriteLine($"Progress: {p.Status}, Concurrency: {p.CurrentConcurrency}/{p.MaxConcurrency}, Queued: {p.QueuedTasks}"));
    /// var bulkhead = new Bulkhead(2);
    /// await bulkhead.ExecuteAsync(async ct =>
    /// {
    ///     await Task.Delay(1000, ct); // Simulate work
    ///     Console.WriteLine("Operation completed");
    /// }, cts.Token, progress);
    /// </code>
    /// </example>
    public async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default, IProgress<BulkheadProgress> progress = null)
    {
        progress ??= this.progress; // Use instance-level progress if provided
        var task = Task.Run(async () =>
        {
            await this.semaphore.WaitAsync(cancellationToken);
            try
            {
                progress?.Report(new BulkheadProgress(this.maxConcurrency - this.semaphore.CurrentCount, this.maxConcurrency, this.queuedTasks.Count, "Executing operation within bulkhead"));
                await action(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Handle cancellation
                throw;
            }
            catch (Exception ex) when (this.handleErrors)
            {
                this.logger?.LogError(ex, "Operation failed within bulkhead.");
            }
            finally
            {
                this.semaphore.Release();
                lock (this.lockObject)
                {
                    this.queuedTasks.Dequeue();
                }
                progress?.Report(new BulkheadProgress(this.maxConcurrency - this.semaphore.CurrentCount, this.maxConcurrency, this.queuedTasks.Count, "Operation completed, semaphore released"));
            }
        }, cancellationToken);

        lock (this.lockObject)
        {
            this.queuedTasks.Enqueue(task);
            progress?.Report(new BulkheadProgress(this.maxConcurrency - this.semaphore.CurrentCount, this.maxConcurrency, this.queuedTasks.Count, $"Task queued, {this.queuedTasks.Count} tasks in queue"));
        }

        await task;
    }

    /// <summary>
    /// Executes the specified action within the bulkhead limit, queuing if the limit is reached, and returns a result.
    /// </summary>
    /// <typeparam name="T">The type of the result returned by the action.</typeparam>
    /// <param name="action">The asynchronous action to execute, accepting a CancellationToken.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <param name="progress">An optional progress reporter for bulkhead operations. Defaults to null.</param>
    /// <returns>A task representing the asynchronous operation, returning the result of the action.</returns>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
    /// <example>
    /// <code>
    /// var cts = new CancellationTokenSource();
    /// var progress = new Progress<BulkheadProgress>(p => Console.WriteLine($"Progress: {p.Status}, Concurrency: {p.CurrentConcurrency}/{p.MaxConcurrency}, Queued: {p.QueuedTasks}"));
    /// var bulkhead = new Bulkhead(2);
    /// int result = await bulkhead.ExecuteAsync(async ct =>
    /// {
    ///     await Task.Delay(1000, ct); // Simulate work
    ///     return 42;
    /// }, cts.Token, progress);
    /// Console.WriteLine($"Result: {result}");
    /// </code>
    /// </example>
    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default, IProgress<BulkheadProgress> progress = null)
    {
        progress ??= this.progress; // Use instance-level progress if provided
        var task = Task.Run(async () =>
        {
            await this.semaphore.WaitAsync(cancellationToken);
            try
            {
                progress?.Report(new BulkheadProgress(this.maxConcurrency - this.semaphore.CurrentCount, this.maxConcurrency, this.queuedTasks.Count, "Executing operation within bulkhead"));
                return await action(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Handle cancellation
                throw;
            }
            catch (Exception ex) when (this.handleErrors)
            {
                this.logger?.LogError(ex, "Operation failed within bulkhead.");
                return default;
            }
            finally
            {
                this.semaphore.Release();
                lock (this.lockObject)
                {
                    this.queuedTasks.Dequeue();
                }
                progress?.Report(new BulkheadProgress(this.maxConcurrency - this.semaphore.CurrentCount, this.maxConcurrency, this.queuedTasks.Count, "Operation completed, semaphore released"));
            }
        }, cancellationToken);

        lock (this.lockObject)
        {
            this.queuedTasks.Enqueue(task);
            progress?.Report(new BulkheadProgress(this.maxConcurrency - this.semaphore.CurrentCount, this.maxConcurrency, this.queuedTasks.Count, $"Task queued, {this.queuedTasks.Count} tasks in queue"));
        }

        var result = await task;
        return result;
    }
}

/// <summary>
/// A fluent builder for configuring and creating a Bulkhead instance.
/// </summary>
/// <remarks>
/// Initializes a new instance of the BulkheadBuilder with the specified concurrency limit.
/// </remarks>
/// <param name="maxConcurrency">The maximum number of concurrent operations allowed.</param>
public class BulkheadBuilder(int maxConcurrency)
{
    private bool handleErrors = false;
    private ILogger logger = null;
    private IProgress<BulkheadProgress> progress = null;

    /// <summary>
    /// Configures the bulkhead to handle errors by logging them instead of throwing.
    /// </summary>
    /// <param name="logger">The logger to use for error logging. If null, errors are silently ignored.</param>
    /// <returns>The BulkheadBuilder instance for chaining.</returns>
    public BulkheadBuilder HandleErrors(ILogger logger = null)
    {
        this.handleErrors = true;
        this.logger = logger;
        return this;
    }

    /// <summary>
    /// Configures the bulkhead to report progress using the specified progress reporter.
    /// </summary>
    /// <param name="progress">The progress reporter to use for bulkhead operations.</param>
    /// <returns>The BulkheadBuilder instance for chaining.</returns>
    public BulkheadBuilder WithProgress(IProgress<BulkheadProgress> progress)
    {
        this.progress = progress;
        return this;
    }

    /// <summary>
    /// Builds and returns a configured Bulkhead instance.
    /// </summary>
    /// <returns>A configured Bulkhead instance.</returns>
    public Bulkhead Build()
    {
        return new Bulkhead(
            maxConcurrency,
            this.handleErrors,
            this.logger,
            this.progress);
    }
}