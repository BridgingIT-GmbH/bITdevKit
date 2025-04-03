namespace BridgingIT.DevKit.Common.Utilities;

using BridgingIT.DevKit.Common.Resiliancy;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provides retry functionality to handle transient failures by retrying an operation a specified number of times with configurable delays.
/// </summary>
public class Retryer
{
    private readonly int maxRetries;
    private readonly TimeSpan delay;
    private readonly bool useExponentialBackoff;
    private readonly bool handleErrors;
    private readonly ILogger logger;
    private readonly IProgress<RetryProgress> progress;

    /// <summary>
    /// Initializes a new instance of the Retryer class with the specified retry settings.
    /// </summary>
    /// <param name="maxRetries">The maximum number of retry attempts.</param>
    /// <param name="delay">The initial delay between retry attempts.</param>
    /// <param name="useExponentialBackoff">If true, increases the delay exponentially with each retry attempt. Defaults to false.</param>
    /// <param name="handleErrors">If true, catches and logs exceptions from the action; otherwise, throws them. Defaults to false.</param>
    /// <param name="logger">An optional logger to log errors if handleErrors is true. Defaults to null.</param>
    /// <param name="progress">An optional progress reporter for retry operations. Defaults to null.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if maxRetries is less than 1 or delay is negative.</exception>
    /// <example>
    /// <code>
    /// var cts = new CancellationTokenSource();
    /// var progress = new Progress<RetryProgress>(p => Console.WriteLine($"Retry Attempt: {p.CurrentAttempt}/{p.MaxAttempts}, Delay: {p.Delay.TotalSeconds}s"));
    /// var retryer = new Retryer(3, TimeSpan.FromSeconds(1), progress: progress);
    /// await retryer.ExecuteAsync(async ct => await SomeOperation(ct), cts.Token);
    /// </code>
    /// </example>
    public Retryer(
        int maxRetries,
        TimeSpan delay,
        bool useExponentialBackoff = false,
        bool handleErrors = false,
        ILogger logger = null,
        IProgress<RetryProgress> progress = null)
    {
        if (maxRetries < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRetries), "Maximum retries must be at least 1.");
        }

        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), "Delay cannot be negative.");
        }

        this.maxRetries = maxRetries;
        this.delay = delay;
        this.useExponentialBackoff = useExponentialBackoff;
        this.handleErrors = handleErrors;
        this.logger = logger;
        this.progress = progress;
    }

    /// <summary>
    /// Executes the specified action with retry logic, retrying on failure up to the maximum number of attempts.
    /// </summary>
    /// <param name="action">The asynchronous action to execute, accepting a CancellationToken.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <param name="progress">An optional progress reporter for retry operations. Defaults to null.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="AggregateException">Thrown if all retry attempts fail and handleErrors is false.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
    /// <example>
    /// <code>
    /// var cts = new CancellationTokenSource();
    /// var progress = new Progress<RetryProgress>(p => Console.WriteLine($"Retry Attempt: {p.CurrentAttempt}/{p.MaxAttempts}, Delay: {p.Delay.TotalSeconds}s"));
    /// var retryer = new Retryer(3, TimeSpan.FromSeconds(1));
    /// await retryer.ExecuteAsync(async ct =>
    /// {
    ///     await Task.Delay(100, ct); // Simulate work
    ///     if (new Random().Next(2) == 0) throw new Exception("Transient failure");
    ///     Console.WriteLine("Success");
    /// }, cts.Token, progress);
    /// cts.Cancel(); // Cancel the operation if needed
    /// </code>
    /// </example>
    public async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default, IProgress<RetryProgress> progress = null)
    {
        progress ??= this.progress; // Use instance-level progress if provided
        Exception lastException = null;

        for (var attempt = 1; attempt <= this.maxRetries; attempt++)
        {
            try
            {
                await action(cancellationToken);
                return;
            }
            catch (OperationCanceledException)
            {
                // If the operation was canceled, don't count it as a failure
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
                if (attempt == this.maxRetries)
                {
                    if (this.handleErrors)
                    {
                        this.logger?.LogError(ex, "All retry attempts failed.");
                        progress?.Report(new RetryProgress(attempt, this.maxRetries, TimeSpan.Zero, "All retry attempts failed."));
                        return;
                    }
                    throw new AggregateException($"Operation failed after {this.maxRetries} attempts.", ex);
                }

                this.logger?.LogWarning(ex, $"Attempt {attempt} failed. Retrying after delay...");
                var currentDelay = this.useExponentialBackoff ? this.delay.Multiply(1 << (attempt - 1)) : this.delay;
                progress?.Report(new RetryProgress(attempt, this.maxRetries, currentDelay, $"Attempt {attempt} failed. Retrying after {currentDelay.TotalSeconds} seconds."));
                await Task.Delay(currentDelay, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Executes the specified action with retry logic, retrying on failure up to the maximum number of attempts, and returns a result.
    /// </summary>
    /// <typeparam name="T">The type of the result returned by the action.</typeparam>
    /// <param name="action">The asynchronous action to execute, accepting a CancellationToken.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <param name="progress">An optional progress reporter for retry operations. Defaults to null.</param>
    /// <returns>A task representing the asynchronous operation, returning the result of the action.</returns>
    /// <exception cref="AggregateException">Thrown if all retry attempts fail and handleErrors is false.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
    /// <example>
    /// <code>
    /// var cts = new CancellationTokenSource();
    /// var progress = new Progress<RetryProgress>(p => Console.WriteLine($"Retry Attempt: {p.CurrentAttempt}/{p.MaxAttempts}, Delay: {p.Delay.TotalSeconds}s"));
    /// var retryer = new Retryer(3, TimeSpan.FromSeconds(1));
    /// int result = await retryer.ExecuteAsync(async ct =>
    /// {
    ///     await Task.Delay(100, ct); // Simulate work
    ///     if (new Random().Next(2) == 0) throw new Exception("Transient failure");
    ///     return 42;
    /// }, cts.Token, progress);
    /// Console.WriteLine($"Result: {result}");
    /// </code>
    /// </example>
    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default, IProgress<RetryProgress> progress = null)
    {
        progress ??= this.progress; // Use instance-level progress if provided
        Exception lastException = null;

        for (var attempt = 1; attempt <= this.maxRetries; attempt++)
        {
            try
            {
                return await action(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // If the operation was canceled, don't count it as a failure
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
                if (attempt == this.maxRetries)
                {
                    if (this.handleErrors)
                    {
                        this.logger?.LogError(ex, "All retry attempts failed.");
                        progress?.Report(new RetryProgress(attempt, this.maxRetries, TimeSpan.Zero, "All retry attempts failed."));
                        return default;
                    }
                    throw new AggregateException($"Operation failed after {this.maxRetries} attempts.", ex);
                }

                this.logger?.LogWarning(ex, $"Attempt {attempt} failed. Retrying after delay...");
                var currentDelay = this.useExponentialBackoff ? this.delay.Multiply(1 << (attempt - 1)) : this.delay;
                progress?.Report(new RetryProgress(attempt, this.maxRetries, currentDelay, $"Attempt {attempt} failed. Retrying after {currentDelay.TotalSeconds} seconds."));
                await Task.Delay(currentDelay, cancellationToken);
            }
        }

        throw new InvalidOperationException("Retryer failed to execute the action.");
    }
}

/// <summary>
/// A fluent builder for configuring and creating a Retryer instance.
/// </summary>
/// <remarks>
/// Initializes a new instance of the RetryerBuilder with the specified retry settings.
/// </remarks>
/// <param name="maxRetries">The maximum number of retry attempts.</param>
/// <param name="delay">The initial delay between retry attempts.</param>
public class RetryerBuilder(int maxRetries, TimeSpan delay)
{
    private bool useExponentialBackoff = false;
    private bool handleErrors = false;
    private ILogger logger = null;
    private IProgress<RetryProgress> progress = null;

    /// <summary>
    /// Configures the retryer to use exponential backoff, increasing the delay with each retry attempt.
    /// </summary>
    /// <returns>The RetryerBuilder instance for chaining.</returns>
    public RetryerBuilder UseExponentialBackoff()
    {
        this.useExponentialBackoff = true;
        return this;
    }

    /// <summary>
    /// Configures the retryer to handle errors by logging them instead of throwing.
    /// </summary>
    /// <param name="logger">The logger to use for error logging. If null, errors are silently ignored.</param>
    /// <returns>The RetryerBuilder instance for chaining.</returns>
    public RetryerBuilder HandleErrors(ILogger logger = null)
    {
        this.handleErrors = true;
        this.logger = logger;
        return this;
    }

    /// <summary>
    /// Configures the retryer to report progress using the specified progress reporter.
    /// </summary>
    /// <param name="progress">The progress reporter to use for retry operations.</param>
    /// <returns>The RetryerBuilder instance for chaining.</returns>
    public RetryerBuilder WithProgress(IProgress<RetryProgress> progress)
    {
        this.progress = progress;
        return this;
    }

    /// <summary>
    /// Builds and returns a configured Retryer instance.
    /// </summary>
    /// <returns>A configured Retryer instance.</returns>
    public Retryer Build()
    {
        return new Retryer(
            maxRetries,
            delay,
            this.useExponentialBackoff,
            this.handleErrors,
            this.logger,
            this.progress);
    }
}