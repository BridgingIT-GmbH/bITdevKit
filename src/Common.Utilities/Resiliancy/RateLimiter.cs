namespace BridgingIT.DevKit.Common.Utilities;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common.Resiliancy;
using Microsoft.Extensions.Logging;

/// <summary>
/// Enforces a maximum rate of operations within a specified time window using a sliding window algorithm.
/// </summary>
public class RateLimiter
{
    private readonly int maxOperations;
    private readonly TimeSpan window;
    private readonly bool handleErrors;
    private readonly ILogger logger;
    private readonly IProgress<ResiliencyProgress> progress;
    private readonly Queue<DateTime> operationTimestamps;
    private readonly Lock lockObject = new();

    /// <summary>
    /// Initializes a new instance of the RateLimiter class with the specified rate limit settings.
    /// </summary>
    /// <param name="maxOperations">The maximum number of operations allowed within the time window.</param>
    /// <param name="window">The time window in which the maximum number of operations is enforced.</param>
    /// <param name="handleErrors">If true, catches and logs exceptions from the action; otherwise, throws them. Defaults to false.</param>
    /// <param name="logger">An optional logger to log errors if handleErrors is true. Defaults to null.</param>
    /// <param name="progress">An optional progress reporter for rate-limiting operations. Defaults to null.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if maxOperations is less than 1 or window is negative.</exception>
    /// <example>
    /// <code>
    /// var rateLimiter = new RateLimiter(5, TimeSpan.FromSeconds(10), progress: new Progress<ResiliencyProgress>(p => Console.WriteLine(p.Status)));
    /// await rateLimiter.ExecuteAsync(async ct => await SomeOperation(ct), CancellationToken.None);
    /// </code>
    /// </example>
    public RateLimiter(
        int maxOperations,
        TimeSpan window,
        bool handleErrors = false,
        ILogger logger = null,
        IProgress<ResiliencyProgress> progress = null)
    {
        if (maxOperations < 1)
            throw new ArgumentOutOfRangeException(nameof(maxOperations), "Maximum operations must be at least 1.");
        if (window < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(window), "Time window cannot be negative.");

        this.maxOperations = maxOperations;
        this.window = window;
        this.handleErrors = handleErrors;
        this.logger = logger;
        this.progress = progress;
        this.operationTimestamps = [];
    }

    /// <summary>
    /// Attempts to execute the specified action if the rate limit allows, otherwise throws a RateLimitExceededException.
    /// </summary>
    /// <param name="action">The asynchronous action to execute, accepting a CancellationToken.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <param name="progress">An optional progress reporter for rate-limiting operations. Defaults to null.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="RateLimitExceededException">Thrown if the rate limit is exceeded and handleErrors is false.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
    /// <example>
    /// <code>
    /// var cts = new CancellationTokenSource();
    /// var progress = new Progress<ResiliencyProgress>(p => Console.WriteLine($"Progress: {p.Status}"));
    /// var rateLimiter = new RateLimiter(5, TimeSpan.FromSeconds(10));
    /// await rateLimiter.ExecuteAsync(async ct =>
    /// {
    ///     await Task.Delay(100, ct); // Simulate work
    ///     Console.WriteLine("Operation executed");
    /// }, cts.Token, progress);
    /// </code>
    /// </example>
    public async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default, IProgress<ResiliencyProgress> progress = null)
    {
        progress ??= this.progress; // Use instance-level progress if provided
        if (!this.AllowOperation(progress))
        {
            progress?.Report(new RateLimiterProgress(this.operationTimestamps.Count, this.maxOperations, this.window, "Rate limit exceeded, operation skipped"));
            if (this.handleErrors)
            {
                this.logger?.LogWarning("Rate limit exceeded, operation skipped.");
                return;
            }
            throw new RateLimitExceededException("Rate limit exceeded.");
        }

        try
        {
            await action(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // If the operation was canceled, don't count it as a failure
            throw;
        }
        catch (Exception ex) when (this.handleErrors)
        {
            this.logger?.LogError(ex, "Operation failed.");
        }
    }

    /// <summary>
    /// Attempts to execute the specified action with a return value if the rate limit allows, otherwise throws a RateLimitExceededException.
    /// </summary>
    /// <typeparam name="T">The type of the result returned by the action.</typeparam>
    /// <param name="action">The asynchronous action to execute, accepting a CancellationToken.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <param name="progress">An optional progress reporter for rate-limiting operations. Defaults to null.</param>
    /// <returns>A task representing the asynchronous operation, returning the result of the action.</returns>
    /// <exception cref="RateLimitExceededException">Thrown if the rate limit is exceeded and handleErrors is false.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
    /// <example>
    /// <code>
    /// var cts = new CancellationTokenSource();
    /// var progress = new Progress<ResiliencyProgress>(p => Console.WriteLine($"Progress: {p.Status}"));
    /// var rateLimiter = new RateLimiter(5, TimeSpan.FromSeconds(10));
    /// int result = await rateLimiter.ExecuteAsync(async ct =>
    /// {
    ///     await Task.Delay(100, ct); // Simulate work
    ///     return 42;
    /// }, cts.Token, progress);
    /// Console.WriteLine($"Result: {result}");
    /// </code>
    /// </example>
    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default, IProgress<ResiliencyProgress> progress = null)
    {
        progress ??= this.progress; // Use instance-level progress if provided
        if (!this.AllowOperation(progress))
        {
            progress?.Report(new RateLimiterProgress(this.operationTimestamps.Count, this.maxOperations, this.window, "Rate limit exceeded, operation skipped"));
            if (this.handleErrors)
            {
                this.logger?.LogWarning("Rate limit exceeded, operation skipped.");
                return default;
            }
            throw new RateLimitExceededException("Rate limit exceeded.");
        }

        try
        {
            return await action(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // If the operation was canceled, don't count it as a failure
            throw;
        }
        catch (Exception ex) when (this.handleErrors)
        {
            this.logger?.LogError(ex, "Operation failed.");
            return default;
        }
    }

    private bool AllowOperation(IProgress<ResiliencyProgress> progress)
    {
        lock (this.lockObject)
        {
            var now = DateTime.UtcNow;
            while (this.operationTimestamps.Count > 0 && (now - this.operationTimestamps.Peek()) > this.window)
            {
                this.operationTimestamps.Dequeue();
            }

            if (this.operationTimestamps.Count >= this.maxOperations)
            {
                return false;
            }

            this.operationTimestamps.Enqueue(now);
            progress?.Report(new RateLimiterProgress(this.operationTimestamps.Count, this.maxOperations, this.window, $"Operation allowed, {this.operationTimestamps.Count}/{this.maxOperations} in window"));
            return true;
        }
    }
}

/// <summary>
/// Exception thrown when the rate limit is exceeded and an operation is attempted.
/// </summary>
/// <remarks>
/// Initializes a new instance of the RateLimitExceededException class with the specified message.
/// </remarks>
/// <param name="message">The message that describes the error.</param>
public class RateLimitExceededException(string message) : Exception(message)
{
}

/// <summary>
/// A fluent builder for configuring and creating a RateLimiter instance.
/// </summary>
/// <remarks>
/// Initializes a new instance of the RateLimiterBuilder with the specified rate limit settings.
/// </remarks>
/// <param name="maxOperations">The maximum number of operations allowed within the time window.</param>
/// <param name="window">The time window in which the maximum number of operations is enforced.</param>
public class RateLimiterBuilder(int maxOperations, TimeSpan window)
{
    private bool handleErrors = false;
    private ILogger logger = null;
    private IProgress<ResiliencyProgress> progress = null;

    /// <summary>
    /// Configures the rate limiter to handle errors by logging them instead of throwing.
    /// </summary>
    /// <param name="logger">The logger to use for error logging. If null, errors are silently ignored.</param>
    /// <returns>The RateLimiterBuilder instance for chaining.</returns>
    public RateLimiterBuilder HandleErrors(ILogger logger = null)
    {
        this.handleErrors = true;
        this.logger = logger;
        return this;
    }

    /// <summary>
    /// Configures the rate limiter to report progress using the specified progress reporter.
    /// </summary>
    /// <param name="progress">The progress reporter to use for rate-limiting operations.</param>
    /// <returns>The RateLimiterBuilder instance for chaining.</returns>
    public RateLimiterBuilder WithProgress(IProgress<ResiliencyProgress> progress)
    {
        this.progress = progress;
        return this;
    }

    /// <summary>
    /// Builds and returns a configured RateLimiter instance.
    /// </summary>
    /// <returns>A configured RateLimiter instance.</returns>
    public RateLimiter Build()
    {
        return new RateLimiter(
            maxOperations,
            window,
            this.handleErrors,
            this.logger,
            this.progress);
    }
}