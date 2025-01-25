// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Represents a policy for retrying operations that might fail.
/// </summary>
public class RetryPolicy
{
    private readonly List<Type> retryableExceptions = [];
    private readonly List<Func<Exception, bool>> retryConditions = [];
    private TimeSpan initialDelay = TimeSpan.FromSeconds(1);
    private TimeSpan maxDelay = TimeSpan.FromSeconds(30);
    private int maxRetries = 3;
    private double backoffMultiplier = 2.0;
    private bool useJitter = true;
    private Action<RetryContext> onRetry;

    /// <summary>
    ///     Creates a new RetryPolicy with default settings.
    /// </summary>
    public static RetryPolicy Create() => new();

    /// <summary>
    ///     Specifies which exception types should trigger a retry.
    /// </summary>
    /// <typeparam name="TException">The type of exception to retry on.</typeparam>
    public RetryPolicy RetryOn<TException>()
        where TException : Exception
    {
        this.retryableExceptions.Add(typeof(TException));

        return this;
    }

    /// <summary>
    ///     Adds a custom condition for determining if an exception should trigger a retry.
    /// </summary>
    public RetryPolicy RetryWhen(Func<Exception, bool> condition)
    {
        this.retryConditions.Add(condition);

        return this;
    }

    /// <summary>
    ///     Sets the initial delay between retries.
    /// </summary>
    public RetryPolicy WithInitialDelay(TimeSpan delay)
    {
        this.initialDelay = delay;

        return this;
    }

    /// <summary>
    ///     Sets the maximum delay between retries.
    /// </summary>
    public RetryPolicy WithMaxDelay(TimeSpan delay)
    {
        this.maxDelay = delay;

        return this;
    }

    /// <summary>
    ///     Sets the maximum number of retry attempts.
    /// </summary>
    public RetryPolicy WithMaxRetries(int retries)
    {
        this.maxRetries = retries;

        return this;
    }

    /// <summary>
    ///     Sets the backoff multiplier for exponential backoff.
    /// </summary>
    public RetryPolicy WithBackoffMultiplier(double multiplier)
    {
        this.backoffMultiplier = multiplier;

        return this;
    }

    /// <summary>
    ///     Enables or disables jitter in the delay calculation.
    /// </summary>
    public RetryPolicy WithJitter(bool enabled = true)
    {
        this.useJitter = enabled;

        return this;
    }

    /// <summary>
    ///     Sets an action to be executed on each retry attempt.
    /// </summary>
    public RetryPolicy OnRetry(Action<RetryContext> action)
    {
        this.onRetry = action;

        return this;
    }

    internal bool ShouldRetry(Exception exception)
    {
        return this.retryableExceptions.Any(t => t.IsInstanceOfType(exception)) ||
            this.retryConditions.Any(c => c(exception));
    }

    internal TimeSpan GetDelayForAttempt(int attempt)
    {
        var delay = TimeSpan.FromTicks((long)(this.initialDelay.Ticks * Math.Pow(this.backoffMultiplier, attempt)));

        if (this.useJitter)
        {
            var random = new Random();
            var jitter = random.NextDouble() * 0.3 + 0.85; // 85-115% of original delay
            delay = TimeSpan.FromTicks((long)(delay.Ticks * jitter));
        }

        return delay > this.maxDelay ? this.maxDelay : delay;
    }

    internal int MaxRetries => this.maxRetries;
    internal Action<RetryContext> OnRetryAction => this.onRetry;
}

/// <summary>
///     Provides context information for retry operations.
/// </summary>
public class RetryContext(Exception exception, int attempt, TimeSpan delay)
{
    public Exception Exception { get; } = exception;

    public int Attempt { get; } = attempt;

    public TimeSpan Delay { get; } = delay;
}