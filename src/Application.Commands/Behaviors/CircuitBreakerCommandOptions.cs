// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands;

/// <summary>
/// Defines operations for i circuit breaker command.
/// </summary>
public interface ICircuitBreakerCommand
{
    /// <summary>
    /// Gets the options.
    /// </summary>
    CircuitBreakerCommandOptions Options { get; }
}

/// <summary>
/// Configures circuit breaker command.
/// </summary>
public class CircuitBreakerCommandOptions
{
    /// <summary>
    /// Gets or sets the attempts.
    /// </summary>
    public int Attempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the backoff.
    /// </summary>
    public TimeSpan Backoff { get; set; } = new(0, 0, 0, 0, 200);

    /// <summary>
    /// Gets or sets the backoff exponential.
    /// </summary>
    public bool BackoffExponential { get; set; }

    /// <summary>
    /// Gets or sets the break duration.
    /// </summary>
    public TimeSpan BreakDuration { get; set; } = new(0, 0, 0, 30);
}
