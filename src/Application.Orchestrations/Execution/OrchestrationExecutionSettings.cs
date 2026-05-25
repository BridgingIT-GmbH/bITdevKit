// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

/// <summary>
/// Configures background execution and recovery behavior for orchestrations.
/// </summary>
public class OrchestrationExecutionSettings
{
    /// <summary>
    /// Gets a value indicating whether background execution and automatic recovery are enabled.
    /// </summary>
    public bool EnableBackgroundExecution { get; init; } = true;

    /// <summary>
    /// Gets the delay applied before hosted recovery work starts after the application has started.
    /// </summary>
    public TimeSpan StartupDelay { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// Gets the interval between background recovery sweeps.
    /// </summary>
    public TimeSpan BackgroundSweepInterval { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets the maximum number of instances processed in a single recovery batch.
    /// </summary>
    public int BackgroundSweepBatchSize { get; init; } = 100;
}
