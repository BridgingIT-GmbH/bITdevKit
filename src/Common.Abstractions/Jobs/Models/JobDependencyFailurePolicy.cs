// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

/// <summary>
/// Represents the action to apply when a prerequisite occurrence does not reach the required terminal outcome.
/// </summary>
public enum JobDependencyFailurePolicy
{
    /// <summary>
    /// Leaves the dependent occurrence blocked for operator intervention.
    /// </summary>
    KeepBlocked = 0,

    /// <summary>
    /// Skips the dependent occurrence.
    /// </summary>
    Skip = 1,

    /// <summary>
    /// Cancels the dependent occurrence.
    /// </summary>
    Cancel = 2,

    /// <summary>
    /// Fails the dependent occurrence.
    /// </summary>
    Fail = 3,
}