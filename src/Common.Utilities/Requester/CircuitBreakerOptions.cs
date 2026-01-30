// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Options for configuring default circuit breaker behavior.
/// </summary>
public class CircuitBreakerOptions
{
    /// <summary>
    /// Gets or sets the default number of attempts before the circuit breaker opens.
    /// Used when the <see cref="HandlerCircuitBreakerAttribute"/> doesn't specify attempts.
    /// </summary>
    public int? DefaultAttempts { get; set; }

    /// <summary>
    /// Gets or sets the default break duration in seconds when the circuit is open.
    /// Used when the <see cref="HandlerCircuitBreakerAttribute"/> doesn't specify a break duration.
    /// </summary>
    public int? DefaultBreakDurationSeconds { get; set; }

    /// <summary>
    /// Gets or sets the default backoff time in milliseconds between retry attempts.
    /// Used when the <see cref="HandlerCircuitBreakerAttribute"/> doesn't specify backoff.
    /// </summary>
    public int? DefaultBackoffMilliseconds { get; set; }

    /// <summary>
    /// Gets or sets the default value indicating whether to use exponential backoff.
    /// Used when the <see cref="HandlerCircuitBreakerAttribute"/> doesn't specify this setting.
    /// </summary>
    public bool? DefaultBackoffExponential { get; set; }
}