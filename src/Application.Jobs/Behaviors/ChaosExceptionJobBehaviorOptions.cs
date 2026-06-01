// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>
/// Marks a job for chaos exception injection.
/// </summary>
public interface IChaosExceptionJob
{
    /// <summary>
    /// Gets the chaos configuration.
    /// </summary>
    ChaosExceptionJobBehaviorOptions Options { get; }
}

/// <summary>
/// Configures chaos exception injection for a job behavior.
/// </summary>
public class ChaosExceptionJobBehaviorOptions
{
    /// <summary>
    /// Gets or sets the fault injection rate between 0 and 1.
    /// </summary>
    public double InjectionRate { get; set; }

    /// <summary>
    /// Gets or sets the injected fault.
    /// </summary>
    public Exception Fault { get; set; } = new ChaosException();
}