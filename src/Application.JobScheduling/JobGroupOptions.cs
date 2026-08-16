// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

/// <summary>
/// Configures job group.
/// </summary>
public class JobGroupOptions
{
    /// <summary>
    /// Gets or sets the disallow concurrent execution groups.
    /// </summary>
    public string[] DisallowConcurrentExecutionGroups { get; set; } = [];

    /// <summary>
    /// Gets or sets the disallow concurrent execution default group.
    /// </summary>
    public bool DisallowConcurrentExecutionDefaultGroup { get; set; }
}
