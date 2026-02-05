// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Specifies a circuit breaker policy for a handler.
/// </summary>
/// <remarks>
/// When parameters are not specified, defaults from <see cref="CircuitBreakerOptions"/> will be used.
/// If no options are configured, an exception will be thrown at runtime.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class HandlerCircuitBreakerAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HandlerCircuitBreakerAttribute"/> class with specified values.
    /// </summary>
    /// <param name="attempts">The number of attempts before the circuit opens.</param>
    /// <param name="breakDurationSeconds">The duration in seconds the circuit stays open.</param>
    /// <param name="backoffMilliseconds">The backoff time in milliseconds between retries.</param>
    /// <param name="backoffExponential">Whether to use exponential backoff.</param>
    public HandlerCircuitBreakerAttribute(int attempts, int breakDurationSeconds, int backoffMilliseconds, bool backoffExponential = false)
    {
        if (attempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(attempts), "Attempts must be greater than 0.");
        }
        if (breakDurationSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(breakDurationSeconds), "Break duration must be non-negative.");
        }
        if (backoffMilliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(backoffMilliseconds), "Backoff milliseconds must be non-negative.");
        }

        this.Attempts = attempts;
        this.BreakDurationSeconds = breakDurationSeconds;
        this.BackoffMilliseconds = backoffMilliseconds;
        this.BackoffExponential = backoffExponential;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HandlerCircuitBreakerAttribute"/> class using defaults from options.
    /// </summary>
    /// <remarks>
    /// When using this constructor, ensure <see cref="CircuitBreakerOptions"/> is configured with default values.
    /// </remarks>
    public HandlerCircuitBreakerAttribute()
    {
    }

    /// <summary>
    /// Gets the number of attempts before the circuit opens, or null to use the default from <see cref="CircuitBreakerOptions"/>.
    /// </summary>
    public int? Attempts { get; }

    /// <summary>
    /// Gets the break duration in seconds, or null to use the default from <see cref="CircuitBreakerOptions"/>.
    /// </summary>
    public int? BreakDurationSeconds { get; }

    /// <summary>
    /// Gets the backoff time in milliseconds, or null to use the default from <see cref="CircuitBreakerOptions"/>.
    /// </summary>
    public int? BackoffMilliseconds { get; }

    /// <summary>
    /// Gets whether to use exponential backoff, or null to use the default from <see cref="CircuitBreakerOptions"/>.
    /// </summary>
    public bool? BackoffExponential { get; }

    /// <summary>
    /// Gets the break duration as a TimeSpan. Returns null if <see cref="BreakDurationSeconds"/> is null.
    /// </summary>
    public TimeSpan? BreakDuration => this.BreakDurationSeconds.HasValue
        ? TimeSpan.FromSeconds(this.BreakDurationSeconds.Value)
        : null;

    /// <summary>
    /// Gets the backoff as a TimeSpan. Returns null if <see cref="BackoffMilliseconds"/> is null.
    /// </summary>
    public TimeSpan? Backoff => this.BackoffMilliseconds.HasValue
        ? TimeSpan.FromMilliseconds(this.BackoffMilliseconds.Value)
        : null;
}