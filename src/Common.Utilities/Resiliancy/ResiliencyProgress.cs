// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.Resiliancy;

using BridgingIT.DevKit.Common.Utilities;

/// <summary>
/// Base class for progress reporting in Resilience utilities.
/// </summary>
public abstract class ResiliencyProgress
{
    /// <summary>
    /// A message describing the current progress state.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Initializes a new instance of the ResiliencyProgress class.
    /// </summary>
    /// <param name="status">The current progress status message.</param>
    protected ResiliencyProgress(string status)
    {
        this.Status = status;
    }
}

/// <summary>
/// Progress information for Retryer operations.
/// </summary>
public class RetryProgress : ResiliencyProgress
{
    public int CurrentAttempt { get; set; }
    public int MaxAttempts { get; set; }
    public TimeSpan Delay { get; set; }

    public RetryProgress(int currentAttempt, int maxAttempts, TimeSpan delay, string status)
        : base(status)
    {
        this.CurrentAttempt = currentAttempt;
        this.MaxAttempts = maxAttempts;
        this.Delay = delay;
    }
}

/// <summary>
/// Progress information for Debouncer operations.
/// </summary>
public class DebouncerProgress : ResiliencyProgress
{
    public TimeSpan RemainingDelay { get; set; }
    public bool IsThrottling { get; set; }

    public DebouncerProgress(TimeSpan remainingDelay, bool isThrottling, string status)
        : base(status)
    {
        this.RemainingDelay = remainingDelay;
        this.IsThrottling = isThrottling;
    }
}

/// <summary>
/// Progress information for Throttler operations.
/// </summary>
public class ThrottlerProgress : ResiliencyProgress
{
    public TimeSpan RemainingInterval { get; set; }

    public ThrottlerProgress(TimeSpan remainingInterval, string status)
        : base(status)
    {
        this.RemainingInterval = remainingInterval;
    }
}

/// <summary>
/// Progress information for CircuitBreaker operations.
/// </summary>
public class CircuitBreakerProgress : ResiliencyProgress
{
    public CircuitBreakerState State { get; set; }
    public int FailureCount { get; set; }
    public TimeSpan ResetTimeout { get; set; }

    public CircuitBreakerProgress(CircuitBreakerState state, int failureCount, TimeSpan resetTimeout, string status)
        : base(status)
    {
        this.State = state;
        this.FailureCount = failureCount;
        this.ResetTimeout = resetTimeout;
    }
}

/// <summary>
/// Progress information for RateLimiter operations.
/// </summary>
public class RateLimiterProgress : ResiliencyProgress
{
    public int CurrentOperations { get; set; }
    public int MaxOperations { get; set; }
    public TimeSpan Window { get; set; }

    public RateLimiterProgress(int currentOperations, int maxOperations, TimeSpan window, string status)
        : base(status)
    {
        this.CurrentOperations = currentOperations;
        this.MaxOperations = maxOperations;
        this.Window = window;
    }
}

/// <summary>
/// Progress information for Notifier operations.
/// </summary>
public class NotifierProgress : ResiliencyProgress
{
    public int HandlersProcessed { get; set; }
    public int TotalHandlers { get; set; }

    public NotifierProgress(int handlersProcessed, int totalHandlers, string status)
        : base(status)
    {
        this.HandlersProcessed = handlersProcessed;
        this.TotalHandlers = totalHandlers;
    }
}

/// <summary>
/// Progress information for BackgroundWorker operations.
/// </summary>
public class BackgroundWorkerProgress : ResiliencyProgress
{
    public int ProgressPercentage { get; set; }

    public BackgroundWorkerProgress(int progressPercentage, string status)
        : base(status)
    {
        this.ProgressPercentage = progressPercentage;
    }
}

/// <summary>
/// Progress information for Requester operations.
/// </summary>
public class RequesterProgress : ResiliencyProgress
{
    public string RequestType { get; set; }

    public RequesterProgress(string requestType, string status)
        : base(status)
    {
        this.RequestType = requestType;
    }
}

/// <summary>
/// Progress information for TimeoutHandler operations.
/// </summary>
public class TimeoutHandlerProgress : ResiliencyProgress
{
    public TimeSpan RemainingTime { get; set; }

    public TimeoutHandlerProgress(TimeSpan remainingTime, string status)
        : base(status)
    {
        this.RemainingTime = remainingTime;
    }
}

/// <summary>
/// Progress information for Bulkhead operations.
/// </summary>
public class BulkheadProgress : ResiliencyProgress
{
    public int CurrentConcurrency { get; set; }
    public int MaxConcurrency { get; set; }
    public int QueuedTasks { get; set; }

    public BulkheadProgress(int currentConcurrency, int maxConcurrency, int queuedTasks, string status)
        : base(status)
    {
        this.CurrentConcurrency = currentConcurrency;
        this.MaxConcurrency = maxConcurrency;
        this.QueuedTasks = queuedTasks;
    }
}