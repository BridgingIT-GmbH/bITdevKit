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
    public int CurrentAttempt { get; set; } = currentAttempt;
    public int MaxAttempts { get; set; } = maxAttempts;
    public TimeSpan Delay { get; set; } = delay;
}

/// <summary>
/// Progress information for Debouncer operations.
/// </summary>
public class DebouncerProgress(TimeSpan remainingDelay, bool isThrottling, string status) : ResiliencyProgress(status)
{
    public TimeSpan RemainingDelay { get; set; } = remainingDelay;
    public bool IsThrottling { get; set; } = isThrottling;
}

/// <summary>
/// Progress information for Throttler operations.
/// </summary>
public class ThrottlerProgress(TimeSpan remainingInterval, string status) : ResiliencyProgress(status)
{
    public TimeSpan RemainingInterval { get; set; } = remainingInterval;
}

/// <summary>
/// Progress information for CircuitBreaker operations.
/// </summary>
public class CircuitBreakerProgress(CircuitBreakerState state, int failureCount, TimeSpan resetTimeout, string status) : ResiliencyProgress(status)
{
    public CircuitBreakerState State { get; set; } = state;
    public int FailureCount { get; set; } = failureCount;
    public TimeSpan ResetTimeout { get; set; } = resetTimeout;
}

/// <summary>
/// Progress information for RateLimiter operations.
/// </summary>
public class RateLimiterProgress(int currentOperations, int maxOperations, TimeSpan window, string status) : ResiliencyProgress(status)
{
    public int CurrentOperations { get; set; } = currentOperations;
    public int MaxOperations { get; set; } = maxOperations;
    public TimeSpan Window { get; set; } = window;
}

/// <summary>
/// Progress information for Notifier operations.
/// </summary>
public class NotifierProgress(int handlersProcessed, int totalHandlers, string status) : ResiliencyProgress(status)
{
    public int HandlersProcessed { get; set; } = handlersProcessed;
    public int TotalHandlers { get; set; } = totalHandlers;
}

/// <summary>
/// Progress information for BackgroundWorker operations.
/// </summary>
public class BackgroundWorkerProgress(int progressPercentage, string status) : ResiliencyProgress(status)
{
    public int ProgressPercentage { get; set; } = progressPercentage;
}

/// <summary>
/// Progress information for Requester operations.
/// </summary>
public class RequesterProgress(string requestType, string status) : ResiliencyProgress(status)
{
    public string RequestType { get; set; } = requestType;
}

/// <summary>
/// Progress information for TimeoutHandler operations.
/// </summary>
public class TimeoutHandlerProgress(TimeSpan remainingTime, string status) : ResiliencyProgress(status)
{
    public TimeSpan RemainingTime { get; set; } = remainingTime;
}

/// <summary>
/// Progress information for Bulkhead operations.
/// </summary>
public class BulkheadProgress(int currentConcurrency, int maxConcurrency, int queuedTasks, string status) : ResiliencyProgress(status)
{
    public int CurrentConcurrency { get; set; } = currentConcurrency;
    public int MaxConcurrency { get; set; } = maxConcurrency;
    public int QueuedTasks { get; set; } = queuedTasks;
}