// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.Utilities;

using Microsoft.Extensions.Logging;

/// <summary>
/// Provides debouncing and throttling functionality to delay or rate-limit execution of an action.
/// </summary>
/// <remarks>
/// Initializes a new instance of the Debouncer class with the specified delay and action.
/// </remarks>
/// <param name="delay">The delay interval before executing the action in debounce mode, or the minimum interval between executions in throttle mode.</param>
/// <param name="action">The asynchronous action to execute.</param>
/// <param name="executeImmediatelyOnFirstCall">If true, executes the action immediately on the first call in debounce mode. Defaults to false.</param>
/// <param name="useThrottling">If true, uses throttling mode (executes immediately and then at fixed intervals); otherwise, uses debouncing mode. Defaults to false.</param>
/// <param name="handleErrors">If true, catches and logs exceptions from the action; otherwise, throws them. Defaults to false.</param>
/// <param name="logger">An optional logger to log errors if handleErrors is true. Defaults to null.</param>
/// <example>
/// <code>
/// var debouncer = new Debouncer(TimeSpan.FromSeconds(1), async ct => await Task.Delay(100, ct));
/// await debouncer.DebounceAsync(CancellationToken.None); // Delays execution by 1 second
/// </code>
/// </example>
public class Debouncer(
    TimeSpan delay,
    Func<CancellationToken, Task> action,
    bool executeImmediatelyOnFirstCall = false,
    bool useThrottling = false,
    bool handleErrors = false,
    ILogger logger = null) : IDisposable
{
    private readonly Func<CancellationToken, Task> action = action ?? throw new ArgumentNullException(nameof(action));
    private CancellationTokenSource cts = new();
    private Task pendingTask;
    private bool isPending;
    private DateTime lastExecution;
    private readonly Lock lockObject = new();

    /// <summary>
    /// Initializes a new instance of the Debouncer class with a simpler action that does not require a CancellationToken.
    /// </summary>
    /// <param name="delay">The delay interval before executing the action in debounce mode, or the minimum interval between executions in throttle mode.</param>
    /// <param name="action">The asynchronous action to execute.</param>
    /// <param name="executeImmediatelyOnFirstCall">If true, executes the action immediately on the first call in debounce mode. Defaults to false.</param>
    /// <param name="useThrottling">If true, uses throttling mode (executes immediately and then at fixed intervals); otherwise, uses debouncing mode. Defaults to false.</param>
    /// <param name="handleErrors">If true, catches and logs exceptions from the action; otherwise, throws them. Defaults to false.</param>
    /// <param name="logger">An optional logger to log errors if handleErrors is true. Defaults to null.</param>
    /// <example>
    /// <code>
    /// var debouncer = new Debouncer(TimeSpan.FromSeconds(1), async () => await Task.Delay(100));
    /// await debouncer.DebounceAsync(CancellationToken.None); // Delays execution by 1 second
    /// </code>
    /// </example>
    public Debouncer(
        TimeSpan delay,
        Func<Task> action,
        bool executeImmediatelyOnFirstCall = false,
        bool useThrottling = false,
        bool handleErrors = false,
        ILogger logger = null)
        : this(delay, ct => action(), executeImmediatelyOnFirstCall, useThrottling, handleErrors, logger)
    {
    }

    /// <summary>
    /// Triggers the debounced or throttled action based on the configured mode.
    /// In debounce mode, delays execution until the specified interval has passed since the last call.
    /// In throttle mode, executes immediately and then at fixed intervals during rapid calls.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
    /// <example>
    /// <code>
    /// var cts = new CancellationTokenSource();
    /// var debouncer = new Debouncer(TimeSpan.FromSeconds(1), async ct => Console.WriteLine("Action executed"));
    /// await debouncer.DebounceAsync(cts.Token); // Action executes after 1 second
    /// await debouncer.DebounceAsync(cts.Token); // Resets delay, executes after another 1 second
    /// cts.Cancel(); // Cancel the operation if needed
    /// </code>
    /// </example>
    public async Task DebounceAsync(CancellationToken cancellationToken = default)
    {
        if (useThrottling)
        {
            var now = DateTime.UtcNow;
            bool shouldExecute;
            lock (this.lockObject)
            {
                shouldExecute = !this.isPending || (now - this.lastExecution) >= delay;
                if (shouldExecute)
                {
                    this.isPending = true;
                    this.lastExecution = now;
                }
            }

            if (shouldExecute)
            {
                try
                {
                    await this.action(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Handle cancellation
                }
                catch (Exception ex) when (handleErrors)
                {
                    logger?.LogError(ex, "Debounced action failed.");
                }
                finally
                {
                    lock (this.lockObject)
                    {
                        this.isPending = false;
                    }
                }
            }
        }
        else
        {
            var shouldExecuteImmediately = false;
            lock (this.lockObject)
            {
                if (this.isPending)
                {
                    this.cts.Cancel();
                    this.cts.Dispose();
                    this.cts = new CancellationTokenSource();
                }
                else if (executeImmediatelyOnFirstCall)
                {
                    shouldExecuteImmediately = true;
                }
                else
                {
                    shouldExecuteImmediately = false;
                    this.isPending = true;
                }
            }

            if (shouldExecuteImmediately)
            {
                await this.action(cancellationToken);
                return;
            }

            var token = this.cts.Token;
            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, cancellationToken))
            {
                this.pendingTask = Task.Delay(delay, linkedCts.Token)
                    .ContinueWith(async _ =>
                    {
                        if (!linkedCts.Token.IsCancellationRequested)
                        {
                            lock (this.lockObject)
                            {
                                this.isPending = false;
                            }
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
                                logger?.LogError(ex, "Debounced action failed.");
                            }
                        }
                    }, linkedCts.Token);
                await this.pendingTask;
            }
        }
    }

    /// <summary>
    /// Cancels any pending debounced or throttled action.
    /// </summary>
    /// <example>
    /// <code>
    /// var debouncer = new Debouncer(TimeSpan.FromSeconds(1), async ct => Console.WriteLine("Action executed"));
    /// await debouncer.DebounceAsync(CancellationToken.None);
    /// debouncer.Cancel(); // Cancels the pending action
    /// </code>
    /// </example>
    public void Cancel()
    {
        lock (this.lockObject)
        {
            if (this.isPending)
            {
                this.cts.Cancel();
                this.isPending = false;
            }
        }
    }

    /// <summary>
    /// Disposes of the debouncer, canceling any pending actions and releasing resources.
    /// </summary>
    /// <example>
    /// <code>
    /// var debouncer = new Debouncer(TimeSpan.FromSeconds(1), async ct => Console.WriteLine("Action executed"));
    /// debouncer.Dispose(); // Cancels any pending actions and cleans up
    /// </code>
    /// </example>
    public void Dispose()
    {
        this.Cancel();
        this.cts?.Dispose();
    }
}

/// <summary>
/// A fluent builder for configuring and creating a Debouncer instance.
/// </summary>
public class DebouncerBuilder
{
    private readonly TimeSpan delay;
    private readonly Func<CancellationToken, Task> action;
    private bool executeImmediatelyOnFirstCall;
    private bool useThrottling;
    private bool handleErrors;
    private ILogger logger;

    /// <summary>
    /// Initializes a new instance of the DebouncerBuilder with the specified delay and action.
    /// </summary>
    /// <param name="delay">The delay interval for debouncing or throttling.</param>
    /// <param name="action">The asynchronous action to execute.</param>
    public DebouncerBuilder(TimeSpan delay, Func<Task> action)
    {
        this.delay = delay;
        this.action = ct => action();
        this.executeImmediatelyOnFirstCall = false;
        this.useThrottling = false;
        this.handleErrors = false;
        this.logger = null;
    }

    /// <summary>
    /// Initializes a new instance of the DebouncerBuilder with the specified delay and action that accepts a CancellationToken.
    /// </summary>
    /// <param name="delay">The delay interval for debouncing or throttling.</param>
    /// <param name="action">The asynchronous action to execute, accepting a CancellationToken.</param>
    public DebouncerBuilder(TimeSpan delay, Func<CancellationToken, Task> action)
    {
        this.delay = delay;
        this.action = action;
        this.executeImmediatelyOnFirstCall = false;
        this.useThrottling = false;
        this.handleErrors = false;
        this.logger = null;
    }

    /// <summary>
    /// Configures the debouncer to execute the action immediately on the first call in debounce mode.
    /// </summary>
    /// <returns>The DebouncerBuilder instance for chaining.</returns>
    public DebouncerBuilder ExecuteImmediatelyOnFirstCall()
    {
        this.executeImmediatelyOnFirstCall = true;
        return this;
    }

    /// <summary>
    /// Configures the debouncer to use throttling mode, executing the action immediately and then at fixed intervals.
    /// </summary>
    /// <returns>The DebouncerBuilder instance for chaining.</returns>
    public DebouncerBuilder UseThrottling()
    {
        this.useThrottling = true;
        return this;
    }

    /// <summary>
    /// Configures the debouncer to handle errors by logging them instead of throwing.
    /// </summary>
    /// <param name="logger">The logger to use for error logging. If null, errors are silently ignored.</param>
    /// <returns>The DebouncerBuilder instance for chaining.</returns>
    public DebouncerBuilder HandleErrors(ILogger logger = null)
    {
        this.handleErrors = true;
        this.logger = logger;
        return this;
    }

    /// <summary>
    /// Builds and returns a configured Debouncer instance.
    /// </summary>
    /// <returns>A configured Debouncer instance.</returns>
    public Debouncer Build()
    {
        return new Debouncer(
            this.delay,
            this.action,
            this.executeImmediatelyOnFirstCall,
            this.useThrottling,
            this.handleErrors,
            this.logger);
    }
}