namespace BridgingIT.DevKit.Common.Utilities;

using BridgingIT.DevKit.Common.Resiliancy;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provides throttling functionality to rate-limit execution of an action, ensuring it executes immediately and then at fixed intervals during rapid calls.
/// </summary>
/// <remarks>
/// Initializes a new instance of the Throttler class with the specified interval and action.
/// </remarks>
/// <param name="interval">The minimum interval between executions of the action.</param>
/// <param name="action">The asynchronous action to execute, accepting a CancellationToken.</param>
/// <param name="handleErrors">If true, catches and logs exceptions from the action; otherwise, throws them. Defaults to false.</param>
/// <param name="logger">An optional logger to log errors if handleErrors is true. Defaults to null.</param>
/// <param name="progress">An optional progress reporter for throttling operations. Defaults to null.</param>
/// <example>
/// <code>
/// var throttler = new Throttler(TimeSpan.FromSeconds(1), async ct => await Task.Delay(100, ct), progress: new Progress<ThrottlerProgress>(p => Console.WriteLine($"Progress: {p.Status}, Remaining: {p.RemainingInterval.TotalSeconds}s")));
/// await throttler.ThrottleAsync(CancellationToken.None); // Executes immediately
/// await throttler.ThrottleAsync(CancellationToken.None); // Skips if within 1 second
/// </code>
/// </example>
public class Throttler(
    TimeSpan interval,
    Func<CancellationToken, Task> action,
    bool handleErrors = false,
    ILogger logger = null,
    IProgress<ThrottlerProgress> progress = null) : IDisposable
{
    private readonly Func<CancellationToken, Task> action = action ?? throw new ArgumentNullException(nameof(action));
    private CancellationTokenSource cts = new();
    private bool isThrottled;
    private DateTime lastExecution;
    private readonly Lock lockObject = new();
    private readonly IProgress<ThrottlerProgress> progress = progress;

    /// <summary>
    /// Initializes a new instance of the Throttler class with a simpler action that does not require a CancellationToken.
    /// </summary>
    /// <param name="interval">The minimum interval between executions of the action.</param>
    /// <param name="action">The asynchronous action to execute.</param>
    /// <param name="handleErrors">If true, catches and logs exceptions from the action; otherwise, throws them. Defaults to false.</param>
    /// <param name="logger">An optional logger to log errors if handleErrors is true. Defaults to null.</param>
    /// <param name="progress">An optional progress reporter for throttling operations. Defaults to null.</param>
    /// <example>
    /// <code>
    /// var throttler = new Throttler(TimeSpan.FromSeconds(1), async () => await Task.Delay(100), progress: new Progress<ThrottlerProgress>(p => Console.WriteLine($"Progress: {p.Status}, Remaining: {p.RemainingInterval.TotalSeconds}s")));
    /// await throttler.ThrottleAsync(CancellationToken.None); // Executes immediately
    /// await throttler.ThrottleAsync(CancellationToken.None); // Skips if within 1 second
    /// </code>
    /// </example>
    public Throttler(
        TimeSpan interval,
        Func<Task> action,
        bool handleErrors = false,
        ILogger logger = null,
        IProgress<ThrottlerProgress> progress = null)
        : this(interval, ct => action(), handleErrors, logger, progress)
    {
    }

    /// <summary>
    /// Triggers the throttled action, executing it immediately if the interval has passed since the last execution, or skipping if within the interval.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <param name="progress">An optional progress reporter for throttling operations. Defaults to null.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
    /// <example>
    /// <code>
    /// var cts = new CancellationTokenSource();
    /// var progress = new Progress<ThrottlerProgress>(p => Console.WriteLine($"Progress: {p.Status}, Remaining: {p.RemainingInterval.TotalSeconds}s"));
    /// var throttler = new Throttler(TimeSpan.FromSeconds(1), async ct => Console.WriteLine("Action executed"));
    /// await throttler.ThrottleAsync(cts.Token, progress); // Executes immediately with progress
    /// await throttler.ThrottleAsync(cts.Token, progress); // Skips if within 1 second with progress
    /// await Task.Delay(1100);
    /// await throttler.ThrottleAsync(cts.Token, progress); // Executes after 1.1 seconds
    /// cts.Cancel(); // Cancel the operation if needed
    /// </code>
    /// </example>
    public async Task ThrottleAsync(CancellationToken cancellationToken = default, IProgress<ThrottlerProgress> progress = null)
    {
        progress ??= this.progress; // Use instance-level progress if provided
        var now = DateTime.UtcNow;
        bool shouldExecute;
        CancellationToken token;

        lock (this.lockObject)
        {
            shouldExecute = !this.isThrottled || (now - this.lastExecution) >= interval;
            if (shouldExecute)
            {
                this.isThrottled = true;
                this.lastExecution = now;
                this.cts.Cancel();
                this.cts.Dispose();
                this.cts = new CancellationTokenSource();
            }
            token = this.cts.Token;
        }

        if (shouldExecute)
        {
            progress?.Report(new ThrottlerProgress(TimeSpan.Zero, "Executing throttled action"));
            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, cancellationToken))
            {
                try
                {
                    await this.action(linkedCts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Handle cancellation
                }
                catch (Exception ex) when (handleErrors)
                {
                    logger?.LogError(ex, "Throttled action failed.");
                }
                finally
                {
                    lock (this.lockObject)
                    {
                        this.isThrottled = false;
                    }
                }

                // Wait for the remaining interval time to ensure the throttle period is respected
                var elapsed = DateTime.UtcNow - this.lastExecution;
                var remaining = interval - elapsed;
                if (remaining > TimeSpan.Zero)
                {
                    progress?.Report(new ThrottlerProgress(remaining, $"Waiting remaining interval of {remaining.TotalSeconds} seconds"));
                    await Task.Delay(remaining, linkedCts.Token);
                }
            }
        }
        else
        {
            var remaining = interval - (now - this.lastExecution);
            progress?.Report(new ThrottlerProgress(remaining, $"Throttled, waiting {remaining.TotalSeconds} seconds"));
        }
    }

    /// <summary>
    /// Cancels any pending throttled action.
    /// </summary>
    /// <example>
    /// <code>
    /// var throttler = new Throttler(TimeSpan.FromSeconds(1), async ct => Console.WriteLine("Action executed"));
    /// await throttler.ThrottleAsync(CancellationToken.None);
    /// throttler.Cancel(); // Cancels any pending action
    /// </code>
    /// </example>
    public void Cancel()
    {
        lock (this.lockObject)
        {
            if (this.isThrottled)
            {
                this.cts.Cancel();
                this.isThrottled = false;
            }
        }
    }

    /// <summary>
    /// Disposes of the throttler, canceling any pending actions and releasing resources.
    /// </summary>
    /// <example>
    /// <code>
    /// var throttler = new Throttler(TimeSpan.FromSeconds(1), async ct => Console.WriteLine("Action executed"));
    /// throttler.Dispose(); // Cancels any pending actions and cleans up
    /// </code>
    /// </example>
    public void Dispose()
    {
        this.Cancel();
        this.cts?.Dispose();
    }
}

/// <summary>
/// A fluent builder for configuring and creating a Throttler instance.
/// </summary>
public class ThrottlerBuilder
{
    private readonly TimeSpan interval;
    private readonly Func<CancellationToken, Task> action;
    private bool handleErrors;
    private ILogger logger;
    private IProgress<ThrottlerProgress> progress;

    /// <summary>
    /// Initializes a new instance of the ThrottlerBuilder with the specified interval and action.
    /// </summary>
    /// <param name="interval">The minimum interval between executions of the action.</param>
    /// <param name="action">The asynchronous action to execute.</param>
    public ThrottlerBuilder(TimeSpan interval, Func<Task> action)
    {
        this.interval = interval;
        this.action = ct => action();
        this.handleErrors = false;
        this.logger = null;
        this.progress = null;
    }

    /// <summary>
    /// Initializes a new instance of the ThrottlerBuilder with the specified interval and action that accepts a CancellationToken.
    /// </summary>
    /// <param name="interval">The minimum interval between executions of the action.</param>
    /// <param name="action">The asynchronous action to execute, accepting a CancellationToken.</param>
    public ThrottlerBuilder(TimeSpan interval, Func<CancellationToken, Task> action)
    {
        this.interval = interval;
        this.action = action;
        this.handleErrors = false;
        this.logger = null;
        this.progress = null;
    }

    /// <summary>
    /// Configures the throttler to handle errors by logging them instead of throwing.
    /// </summary>
    /// <param name="logger">The logger to use for error logging. If null, errors are silently ignored.</param>
    /// <returns>The ThrottlerBuilder instance for chaining.</returns>
    public ThrottlerBuilder HandleErrors(ILogger logger = null)
    {
        this.handleErrors = true;
        this.logger = logger;
        return this;
    }

    /// <summary>
    /// Configures the throttler to report progress using the specified progress reporter.
    /// </summary>
    /// <param name="progress">The progress reporter to use for throttling operations.</param>
    /// <returns>The ThrottlerBuilder instance for chaining.</returns>
    public ThrottlerBuilder WithProgress(IProgress<ThrottlerProgress> progress)
    {
        this.progress = progress;
        return this;
    }

    /// <summary>
    /// Builds and returns a configured Throttler instance.
    /// </summary>
    /// <returns>A configured Throttler instance.</returns>
    public Throttler Build()
    {
        return new Throttler(
            this.interval,
            this.action,
            this.handleErrors,
            this.logger,
            this.progress);
    }
}