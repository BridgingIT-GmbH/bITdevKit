// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.Utilities;

using Microsoft.Extensions.Logging;

/// <summary>
/// Implements the circuit breaker pattern to prevent repeated calls to a failing operation, allowing the system to fail fast and recover gracefully.
/// </summary>
public class CircuitBreaker
{
    private readonly int failureThreshold;
    private readonly TimeSpan resetTimeout;
    private readonly bool handleErrors;
    private readonly ILogger logger;
    private int failureCount;
    private DateTime lastFailureTime;
    private CircuitBreakerState state;
    private readonly Lock lockObject = new();

    /// <summary>
    /// Initializes a new instance of the CircuitBreaker class with the specified settings.
    /// </summary>
    /// <param name="failureThreshold">The number of failures allowed before opening the circuit.</param>
    /// <param name="resetTimeout">The duration to wait before transitioning from Open to Half-Open state.</param>
    /// <param name="handleErrors">If true, catches and logs exceptions from the action; otherwise, throws them. Defaults to false.</param>
    /// <param name="logger">An optional logger to log errors if handleErrors is true. Defaults to null.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if failureThreshold is less than 1 or resetTimeout is negative.</exception>
    /// <example>
    /// <code>
    /// var circuitBreaker = new CircuitBreaker(3, TimeSpan.FromSeconds(30));
    /// await circuitBreaker.ExecuteAsync(async ct => await SomeOperation(ct), CancellationToken.None);
    /// </code>
    /// </example>
    public CircuitBreaker(
        int failureThreshold,
        TimeSpan resetTimeout,
        bool handleErrors = false,
        ILogger logger = null)
    {
        if (failureThreshold < 1)
            throw new ArgumentOutOfRangeException(nameof(failureThreshold), "Failure threshold must be at least 1.");
        if (resetTimeout < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(resetTimeout), "Reset timeout cannot be negative.");

        this.failureThreshold = failureThreshold;
        this.resetTimeout = resetTimeout;
        this.handleErrors = handleErrors;
        this.logger = logger;
        this.state = CircuitBreakerState.Closed;
    }

    /// <summary>
    /// Gets the current state of the circuit breaker.
    /// </summary>
    /// <example>
    /// <code>
    /// var circuitBreaker = new CircuitBreaker(3, TimeSpan.FromSeconds(30));
    /// Console.WriteLine($"Circuit breaker state: {circuitBreaker.State}");
    /// </code>
    /// </example>
    public CircuitBreakerState State
    {
        get
        {
            lock (this.lockObject)
            {
                if (this.state == CircuitBreakerState.Open && DateTime.UtcNow - this.lastFailureTime >= this.resetTimeout)
                {
                    this.state = CircuitBreakerState.HalfOpen;
                }
                return this.state;
            }
        }
    }

    /// <summary>
    /// Executes the specified action, applying circuit breaker logic to prevent repeated calls if the circuit is open.
    /// </summary>
    /// <param name="action">The asynchronous action to execute, accepting a CancellationToken.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="CircuitBreakerOpenException">Thrown if the circuit is open and the reset timeout has not elapsed.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
    /// <example>
    /// <code>
    /// var cts = new CancellationTokenSource();
    /// var circuitBreaker = new CircuitBreaker(3, TimeSpan.FromSeconds(30));
    /// await circuitBreaker.ExecuteAsync(async ct =>
    /// {
    ///     await Task.Delay(100, ct); // Simulate work
    ///     if (new Random().Next(2) == 0) throw new Exception("Operation failed");
    ///     Console.WriteLine("Success");
    /// }, cts.Token);
    /// cts.Cancel(); // Cancel the operation if needed
    /// </code>
    /// </example>
    public async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        var (success, _) = await this.TryExecuteAsync(ct => action(ct), cancellationToken);
        if (!success)
        {
            if (this.handleErrors)
            {
                this.logger?.LogWarning("Circuit breaker is open, operation skipped.");
                return;
            }
            throw new CircuitBreakerOpenException("Circuit breaker is open.");
        }
    }

    /// <summary>
    /// Executes the specified action with a return value, applying circuit breaker logic to prevent repeated calls if the circuit is open.
    /// </summary>
    /// <typeparam name="T">The type of the result returned by the action.</typeparam>
    /// <param name="action">The asynchronous action to execute, accepting a CancellationToken.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, returning the result of the action.</returns>
    /// <exception cref="CircuitBreakerOpenException">Thrown if the circuit is open and the reset timeout has not elapsed.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
    /// <example>
    /// <code>
    /// var cts = new CancellationTokenSource();
    /// var circuitBreaker = new CircuitBreaker(3, TimeSpan.FromSeconds(30));
    /// int result = await circuitBreaker.ExecuteAsync(async ct =>
    /// {
    ///     await Task.Delay(100, ct); // Simulate work
    ///     if (new Random().Next(2) == 0) throw new Exception("Operation failed");
    ///     return 42;
    /// }, cts.Token);
    /// Console.WriteLine($"Result: {result}");
    /// </code>
    /// </example>
    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
    {
        var (success, result) = await this.TryExecuteAsync(ct => action(ct), cancellationToken);
        if (!success)
        {
            if (this.handleErrors)
            {
                this.logger?.LogWarning("Circuit breaker is open, operation skipped.");
                return default;
            }
            throw new CircuitBreakerOpenException("Circuit breaker is open.");
        }
        return (T)result;
    }

    private async Task<(bool Success, object Result)> TryExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        lock (this.lockObject)
        {
            if (this.state == CircuitBreakerState.Open)
            {
                if (DateTime.UtcNow - this.lastFailureTime < this.resetTimeout)
                {
                    return (false, null); // Circuit is still open
                }
                this.state = CircuitBreakerState.HalfOpen;
            }
        }

        try
        {
            await action(cancellationToken);
            lock (this.lockObject)
            {
                this.state = CircuitBreakerState.Closed;
                this.failureCount = 0;
            }
            return (true, null);
        }
        catch (OperationCanceledException)
        {
            // If the operation was canceled, don't count it as a failure
            throw;
        }
        catch (Exception ex)
        {
            lock (this.lockObject)
            {
                this.failureCount++;
                this.lastFailureTime = DateTime.UtcNow;
                if (this.failureCount >= this.failureThreshold)
                {
                    this.state = CircuitBreakerState.Open;
                    this.logger?.LogWarning(ex, "Circuit breaker opened due to repeated failures.");
                }
            }
            if (this.handleErrors)
            {
                this.logger?.LogError(ex, "Operation failed.");
                return (true, null);
            }
            throw;
        }
    }

    private async Task<(bool Success, object Result)> TryExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        lock (this.lockObject)
        {
            if (this.state == CircuitBreakerState.Open)
            {
                if (DateTime.UtcNow - this.lastFailureTime < this.resetTimeout)
                {
                    return (false, null); // Circuit is still open
                }
                this.state = CircuitBreakerState.HalfOpen;
            }
        }

        try
        {
            var result = await action(cancellationToken);
            lock (this.lockObject)
            {
                this.state = CircuitBreakerState.Closed;
                this.failureCount = 0;
            }
            return (true, result);
        }
        catch (OperationCanceledException)
        {
            // If the operation was canceled, don't count it as a failure
            throw;
        }
        catch (Exception ex)
        {
            lock (this.lockObject)
            {
                this.failureCount++;
                this.lastFailureTime = DateTime.UtcNow;
                if (this.failureCount >= this.failureThreshold)
                {
                    this.state = CircuitBreakerState.Open;
                    this.logger?.LogWarning(ex, "Circuit breaker opened due to repeated failures.");
                }
            }
            if (this.handleErrors)
            {
                this.logger?.LogError(ex, "Operation failed.");
                return (true, null);
            }
            throw;
        }
    }
}

/// <summary>
/// Represents the possible states of a circuit breaker.
/// </summary>
public enum CircuitBreakerState
{
    /// <summary>
    /// The circuit is closed, allowing operations to proceed.
    /// </summary>
    Closed,

    /// <summary>
    /// The circuit is open, preventing operations until the reset timeout elapses.
    /// </summary>
    Open,

    /// <summary>
    /// The circuit is half-open, allowing a single operation to test if the system has recovered.
    /// </summary>
    HalfOpen
}

/// <summary>
/// Exception thrown when the circuit breaker is open and an operation is attempted.
/// </summary>
/// <remarks>
/// Initializes a new instance of the CircuitBreakerOpenException class with the specified message.
/// </remarks>
/// <param name="message">The message that describes the error.</param>
public class CircuitBreakerOpenException(string message) : Exception(message)
{
}

/// <summary>
/// A fluent builder for configuring and creating a CircuitBreaker instance.
/// </summary>
/// <remarks>
/// Initializes a new instance of the CircuitBreakerBuilder with the specified settings.
/// </remarks>
/// <param name="failureThreshold">The number of failures allowed before opening the circuit.</param>
/// <param name="resetTimeout">The duration to wait before transitioning from Open to Half-Open state.</param>
public class CircuitBreakerBuilder(int failureThreshold, TimeSpan resetTimeout)
{
    private bool handleErrors;
    private ILogger logger;

    /// <summary>
    /// Configures the circuit breaker to handle errors by logging them instead of throwing.
    /// </summary>
    /// <param name="logger">The logger to use for error logging. If null, errors are silently ignored.</param>
    /// <returns>The CircuitBreakerBuilder instance for chaining.</returns>
    public CircuitBreakerBuilder HandleErrors(ILogger logger = null)
    {
        this.handleErrors = true;
        this.logger = logger;
        return this;
    }

    /// <summary>
    /// Builds and returns a configured CircuitBreaker instance.
    /// </summary>
    /// <returns>A configured CircuitBreaker instance.</returns>
    public CircuitBreaker Build()
    {
        return new CircuitBreaker(
            failureThreshold,
            resetTimeout,
            this.handleErrors,
            this.logger);
    }
}