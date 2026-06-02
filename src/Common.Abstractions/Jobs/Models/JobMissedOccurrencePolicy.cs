// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

/// <summary>
/// Represents the catch-up behavior to apply when time-based trigger work was missed while the scheduler was inactive.
/// </summary>
public enum JobMissedOccurrencePolicy
{
    /// <summary>
    /// Ignores occurrences that became due while the scheduler was inactive.
    /// </summary>
    Skip = 1,

    /// <summary>
    /// Creates a single catch-up occurrence for the missed window.
    /// </summary>
    RunOnce = 0,

    /// <summary>
    /// Creates every missed occurrence up to the active safety limit.
    /// </summary>
    RunAll = 2,
}
