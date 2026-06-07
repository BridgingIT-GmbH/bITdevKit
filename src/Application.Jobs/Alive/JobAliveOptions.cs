// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

/// <summary>
/// Stores runtime availability for the built-in jobs alive probe.
/// </summary>
/// <example>
/// <code>
/// services.AddJobScheduler().AliveEnabled(false);
/// </code>
/// </example>
public sealed class JobAliveOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the jobs alive probe is available.
    /// </summary>
    public bool Enabled { get; set; } = true;
}

