// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

/// <summary>
/// Represents the persisted status of an occurrence dependency.
/// </summary>
public enum JobDependencyStatus
{
    /// <summary>
    /// The prerequisite has not yet been satisfied.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// The prerequisite requirement was satisfied.
    /// </summary>
    Satisfied = 1,

    /// <summary>
    /// The prerequisite ended in a failing state for this dependency.
    /// </summary>
    Failed = 2,

    /// <summary>
    /// The dependency was intentionally skipped.
    /// </summary>
    Skipped = 3,

    /// <summary>
    /// The dependency was cancelled before resolution.
    /// </summary>
    Cancelled = 4,
}