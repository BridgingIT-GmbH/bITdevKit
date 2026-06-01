// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

/// <summary>
/// Represents concurrency settings for a job definition.
/// </summary>
/// <param name="Limit">The maximum concurrent executions.</param>
public sealed record JobConcurrencyOptions(int? Limit)
{
    /// <summary>
    /// Represents the default concurrency settings.
    /// </summary>
    public static readonly JobConcurrencyOptions Default = new JobConcurrencyOptions(default(int?));
}