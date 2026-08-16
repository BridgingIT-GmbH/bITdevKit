// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.Resiliancy;

using BridgingIT.DevKit.Common.Utilities;

/// <summary>
/// Base class for progress reporting in Resilience utilities.
/// </summary>
/// <remarks>
/// Initializes a new instance of the ResiliencyProgress class.
/// </remarks>
/// <param name="status">The current progress status message.</param>
public abstract class ResiliencyProgress(string status)
{
    /// <summary>
    /// A message describing the current progress state.
    /// </summary>
    public string Status { get; set; } = status;
}

/// <summary>
/// Progress information for Retryer operations.
/// </summary>
public class RetryProgress(int currentAttempt, int maxAttempts, TimeSpan delay, string status) : ResiliencyProgress(status)
{
    /// <summary>Gets or sets the current attempt number.</summary>
    public int CurrentAttempt { get; set; } = currentAttempt;
    /// <summary>Gets or sets the maximum number of attempts.</summary>
    public int MaxAttempts { get; set; } = maxAttempts;
    /// <summary>Gets or sets the delay before the next attempt.</summary>
    public TimeSpan Delay { get; set; } = delay;
}

/// <summary>
/// Progress information for Debouncer operations.
/// </summary>
public class DebouncerProgress(TimeSpan remainingDelay, bool isThrottling, string status) : ResiliencyProgress(status)
{
    /// <summary>Gets or sets the remaining debounce delay.</summary>
    public TimeSpan RemainingDelay { get; set; } = remainingDelay;
    /// <summary>Gets or sets whether calls are currently being throttled.</summary>
    public bool IsThrottling { get; set; } = isThrottling;
}

/// <summary>
/// Progress information for Throttler operations.
/// </summary>
public class ThrottlerProgress(TimeSpan remainingInterval, string status) : ResiliencyProgress(status)
{
    /// <summary>Gets or sets the time remaining in the throttle interval.</summary>
    public TimeSpan RemainingInterval { get; set; } = remainingInterval;
}

/// <summary>
/// Progress information for CircuitBreaker operations.
/// </summary>
public class CircuitBreakerProgress(CircuitBreakerState state, int failureCount, TimeSpan resetTimeout, string status) : ResiliencyProgress(status)
{
    /// <summary>Gets or sets the current circuit state.</summary>
    public CircuitBreakerState State { get; set; } = state;
    /// <summary>Gets or sets the consecutive failure count.</summary>
    public int FailureCount { get; set; } = failureCount;
    /// <summary>Gets or sets the duration before an open circuit can become half-open.</summary>
    public TimeSpan ResetTimeout { get; set; } = resetTimeout;
}

/// <summary>
/// Progress information for RateLimiter operations.
/// </summary>
public class RateLimiterProgress(int currentOperations, int maxOperations, TimeSpan window, string status) : ResiliencyProgress(status)
{
    /// <summary>Gets or sets the current operation count.</summary>
    public int CurrentOperations { get; set; } = currentOperations;
    /// <summary>Gets or sets the maximum operations permitted in the window.</summary>
    public int MaxOperations { get; set; } = maxOperations;
    /// <summary>Gets or sets the rate-limit window.</summary>
    public TimeSpan Window { get; set; } = window;
}

/// <summary>
/// Progress information for Notifier operations.
/// </summary>
public class SimpleNotifierProgress(int handlersProcessed, int totalHandlers, string status) : ResiliencyProgress(status)
{
    /// <summary>Gets or sets the number of handlers processed.</summary>
    public int HandlersProcessed { get; set; } = handlersProcessed;
    /// <summary>Gets or sets the total number of handlers.</summary>
    public int TotalHandlers { get; set; } = totalHandlers;
}

/// <summary>
/// Progress information for BackgroundWorker operations.
/// </summary>
public class BackgroundWorkerProgress(int progressPercentage, string status) : ResiliencyProgress(status)
{
    /// <summary>Gets or sets the completion percentage.</summary>
    public int ProgressPercentage { get; set; } = progressPercentage;
}

/// <summary>
/// Progress information for Requester operations.
/// </summary>
public class SimpleRequesterProgress(string requestType, string status) : ResiliencyProgress(status)
{
    /// <summary>Gets or sets the request type name.</summary>
    public string RequestType { get; set; } = requestType;
}

/// <summary>
/// Progress information for TimeoutHandler operations.
/// </summary>
public class TimeoutHandlerProgress(TimeSpan remainingTime, string status) : ResiliencyProgress(status)
{
    /// <summary>Gets or sets the time remaining before timeout.</summary>
    public TimeSpan RemainingTime { get; set; } = remainingTime;
}

/// <summary>
/// Progress information for Bulkhead operations.
/// </summary>
public class BulkheadProgress(int currentConcurrency, int maxConcurrency, int queuedTasks, string status) : ResiliencyProgress(status)
{
    /// <summary>Gets or sets the current number of concurrent operations.</summary>
    public int CurrentConcurrency { get; set; } = currentConcurrency;
    /// <summary>Gets or sets the maximum permitted concurrency.</summary>
    public int MaxConcurrency { get; set; } = maxConcurrency;
    /// <summary>Gets or sets the number of queued operations.</summary>
    public int QueuedTasks { get; set; } = queuedTasks;
}
