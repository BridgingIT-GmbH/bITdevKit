namespace BridgingIT.DevKit.Common.Resilience;

using System.Threading;
using BridgingIT.DevKit.Common.Resiliancy;
using Microsoft.Extensions.Logging;

/// <summary>
/// Enforces a timeout on asynchronous operations, canceling them if they exceed the specified duration.
/// </summary>
public class TimeoutHandler
{
    private readonly TimeSpan timeout;
    private readonly bool handleErrors;
    private readonly ILogger logger;
    private readonly IProgress<ResiliencyProgress> progress;

    /// <summary>
    /// Initializes a new instance of the TimeoutHandler class with the specified timeout duration.
    /// </summary>
    /// <param name="timeout">The maximum duration for the operation before cancellation.</param>
    /// <param name="handleErrors">If true, catches and logs exceptions from the action; otherwise, throws them. Defaults to false.</param>
    /// <param name="logger">An optional logger to log errors if handleErrors is true. Defaults to null.</param>
    /// <param name="progress">An optional progress reporter for timeout operations. Defaults to null.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if timeout is negative.</exception>
    /// <example>
    /// <code>
    /// var timeoutHandler = new TimeoutHandler(TimeSpan.FromSeconds(5), progress: new Progress<ResiliencyProgress>(p => Console.WriteLine(p.Status)));
    /// await timeoutHandler.ExecuteAsync(async ct => await Task.Delay(3000, ct), CancellationToken.None);
    /// </code>
    /// </example>
    public TimeoutHandler(
        TimeSpan timeout,
        bool handleErrors = false,
        ILogger logger = null,
        IProgress<ResiliencyProgress> progress = null)
    {
        if (timeout < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout cannot be negative.");

        this.timeout = timeout;
        this.handleErrors = handleErrors;
        this.logger = logger;
        this.progress = progress;
    }

    /// <summary>
    /// Executes the specified action with a timeout, canceling it if it exceeds the duration.
    /// </summary>
    /// <param name="action">The asynchronous action to execute, accepting a CancellationToken.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <param name="progress">An optional progress reporter for timeout operations. Defaults to null.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="TimeoutException">Thrown if the operation exceeds the timeout duration and handleErrors is false.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
    /// <example>
    /// <code>
    /// var cts = new CancellationTokenSource();
    /// var progress = new Progress<ResiliencyProgress>(p => Console.WriteLine($"Progress: {p.Status}"));
    /// var timeoutHandler = new TimeoutHandler(TimeSpan.FromSeconds(2));
    /// await timeoutHandler.ExecuteAsync(async ct =>
    /// {
    ///     await Task.Delay(3000, ct); // This will timeout
    ///     Console.WriteLine("Operation completed");
    /// }, cts.Token, progress);
    /// </code>
    /// </example>
    public async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default, IProgress<ResiliencyProgress> progress = null)
    {
        progress ??= this.progress; // Use instance-level progress if provided
        using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            var timeoutTask = Task.Delay(this.timeout, cts.Token);
            var operationTask = action(cts.Token);

            // Simulate progress reporting by polling remaining time
            var startTime = DateTime.UtcNow;
            while (!timeoutTask.IsCompleted && !operationTask.IsCompleted)
            {
                await Task.Delay(100, cts.Token); // Poll every 100ms to avoid excessive overhead
                var remaining = this.timeout - (DateTime.UtcNow - startTime);
                if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
                progress?.Report(new TimeoutHandlerProgress(remaining, $"Operation running, {remaining.TotalSeconds} seconds remaining"));
            }

            var completedTask = await Task.WhenAny(timeoutTask, operationTask);

            if (completedTask == timeoutTask)
            {
                cts.Cancel();
                progress?.Report(new TimeoutHandlerProgress(TimeSpan.Zero, "Operation timed out"));
                if (this.handleErrors)
                {
                    this.logger?.LogWarning("Operation timed out.");
                    return;
                }
                throw new TimeoutException($"Operation exceeded timeout of {this.timeout.TotalSeconds} seconds.");
            }

            try
            {
                await operationTask;
                progress?.Report(new TimeoutHandlerProgress(TimeSpan.Zero, "Operation completed within timeout"));
            }
            catch (OperationCanceledException)
            {
                // Handle cancellation
                throw;
            }
            catch (Exception ex) when (this.handleErrors)
            {
                this.logger?.LogError(ex, "Operation failed.");
            }
        }
    }

    /// <summary>
    /// Executes the specified action with a timeout, canceling it if it exceeds the duration, and returns a result.
    /// </summary>
    /// <typeparam name="T">The type of the result returned by the action.</typeparam>
    /// <param name="action">The asynchronous action to execute, accepting a CancellationToken.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <param name="progress">An optional progress reporter for timeout operations. Defaults to null.</param>
    /// <returns>A task representing the asynchronous operation, returning the result of the action.</returns>
    /// <exception cref="TimeoutException">Thrown if the operation exceeds the timeout duration and handleErrors is false.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
    /// <example>
    /// <code>
    /// var cts = new CancellationTokenSource();
    /// var progress = new Progress<ResiliencyProgress>(p => Console.WriteLine($"Progress: {p.Status}"));
    /// var timeoutHandler = new TimeoutHandler(TimeSpan.FromSeconds(2));
    /// int result = await timeoutHandler.ExecuteAsync(async ct =>
    /// {
    ///     await Task.Delay(3000, ct); // This will timeout
    ///     return 42;
    /// }, cts.Token, progress);
    /// </code>
    /// </example>
    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default, IProgress<ResiliencyProgress> progress = null)
    {
        progress ??= this.progress; // Use instance-level progress if provided
        using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            var timeoutTask = Task.Delay(this.timeout, cts.Token);
            var operationTask = action(cts.Token);

            // Simulate progress reporting by polling remaining time
            var startTime = DateTime.UtcNow;
            while (!timeoutTask.IsCompleted && !operationTask.IsCompleted)
            {
                await Task.Delay(100, cts.Token); // Poll every 100ms to avoid excessive overhead
                var remaining = this.timeout - (DateTime.UtcNow - startTime);
                if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
                progress?.Report(new TimeoutHandlerProgress(remaining, $"Operation running, {remaining.TotalSeconds} seconds remaining"));
            }

            var completedTask = await Task.WhenAny(timeoutTask, operationTask);

            if (completedTask == timeoutTask)
            {
                cts.Cancel();
                progress?.Report(new TimeoutHandlerProgress(TimeSpan.Zero, "Operation timed out"));
                if (this.handleErrors)
                {
                    this.logger?.LogWarning("Operation timed out.");
                    return default;
                }
                throw new TimeoutException($"Operation exceeded timeout of {this.timeout.TotalSeconds} seconds.");
            }

            try
            {
                var result = await operationTask;
                progress?.Report(new TimeoutHandlerProgress(TimeSpan.Zero, "Operation completed within timeout"));
                return result;
            }
            catch (OperationCanceledException)
            {
                // Handle cancellation
                throw;
            }
            catch (Exception ex) when (this.handleErrors)
            {
                this.logger?.LogError(ex, "Operation failed.");
                return default;
            }
        }
    }
}

/// <summary>
/// A fluent builder for configuring and creating a TimeoutHandler instance.
/// </summary>
/// <remarks>
/// Initializes a new instance of the TimeoutHandlerBuilder with the specified timeout duration.
/// </remarks>
/// <param name="timeout">The maximum duration for the operation before cancellation.</param>
public class TimeoutHandlerBuilder(TimeSpan timeout)
{
    private bool handleErrors = false;
    private ILogger logger = null;
    private IProgress<ResiliencyProgress> progress = null;

    /// <summary>
    /// Configures the timeout handler to handle errors by logging them instead of throwing.
    /// </summary>
    /// <param name="logger">The logger to use for error logging. If null, errors are silently ignored.</param>
    /// <returns>The TimeoutHandlerBuilder instance for chaining.</returns>
    public TimeoutHandlerBuilder HandleErrors(ILogger logger = null)
    {
        this.handleErrors = true;
        this.logger = logger;
        return this;
    }

    /// <summary>
    /// Configures the timeout handler to report progress using the specified progress reporter.
    /// </summary>
    /// <param name="progress">The progress reporter to use for timeout operations.</param>
    /// <returns>The TimeoutHandlerBuilder instance for chaining.</returns>
    public TimeoutHandlerBuilder WithProgress(IProgress<ResiliencyProgress> progress)
    {
        this.progress = progress;
        return this;
    }

    /// <summary>
    /// Builds and returns a configured TimeoutHandler instance.
    /// </summary>
    /// <returns>A configured TimeoutHandler instance.</returns>
    public TimeoutHandler Build()
    {
        return new TimeoutHandler(
            timeout,
            this.handleErrors,
            this.logger,
            this.progress);
    }
}