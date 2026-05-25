// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

/// <summary>
/// Enables chaos-exception injection for an orchestration type.
/// </summary>
public interface IChaosExceptionOrchestration
{
    /// <summary>
    /// Gets the chaos options.
    /// </summary>
    ChaosExceptionOrchestrationOptions Options { get; }
}

/// <summary>
/// Configuration options for orchestration chaos-exception injection.
/// </summary>
public class ChaosExceptionOrchestrationOptions
{
    /// <summary>
    /// A decimal between 0 and 1 inclusive indicating how often a chaos fault should be injected.
    /// </summary>
    public double InjectionRate { get; set; }

    /// <summary>
    /// Gets or sets the injected fault. Defaults to <see cref="ChaosException"/>.
    /// </summary>
    public Exception Fault { get; set; } = new ChaosException();
}
