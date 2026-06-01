// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

/// <summary>
/// Represents the roll-up rule applied to a batch as child occurrences finish.
/// </summary>
public enum JobBatchCompletionPolicy
{
    /// <summary>
    /// Requires every child occurrence to succeed for the batch to complete successfully.
    /// </summary>
    RequireAllSucceeded = 0,

    /// <summary>
    /// Allows the batch to complete even when some child occurrences fail.
    /// </summary>
    AllowPartialCompletion = 1,
}