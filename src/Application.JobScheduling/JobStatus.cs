// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

/// <summary>
/// Defines the supported job status values.
/// </summary>
public enum JobStatus
{
    /// <summary>
    /// Represents the started value.
    /// </summary>
    Started = 0,
    /// <summary>
    /// Represents the success value.
    /// </summary>
    Success = 1,
    /// <summary>
    /// Represents the failed value.
    /// </summary>
    Failed = 2,
    /// <summary>
    /// Represents the interrupted value.
    /// </summary>
    Interrupted = 3
}
