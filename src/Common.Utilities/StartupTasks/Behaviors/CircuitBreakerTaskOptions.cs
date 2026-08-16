// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Identifies a startup task that supplies circuit-breaker options.
/// </summary>
public interface ICircuitBreakerStartupTask : IStartupTask
{
    /// <summary>Gets the circuit-breaker options for the task.</summary>
    CircuitBreakerTaskOptions Options { get; }
}

/// <summary>
///     Configures retry backoff and circuit-break duration for a startup task.
/// </summary>
public class CircuitBreakerTaskOptions
{
    /// <summary>Gets or sets the failures required to open the circuit.</summary>
    public int Attempts { get; set; } = 3;

    /// <summary>Gets or sets the delay between retry attempts.</summary>
    public TimeSpan Backoff { get; set; } = new(0, 0, 0, 0, 200);

    /// <summary>Gets or sets whether retry backoff grows exponentially.</summary>
    public bool BackoffExponential { get; set; }

    /// <summary>Gets or sets the duration for which the circuit remains open.</summary>
    public TimeSpan BreakDuration { get; set; } = new(0, 0, 0, 30);
}
