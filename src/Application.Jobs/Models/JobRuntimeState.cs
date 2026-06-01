// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

/// <summary>
/// Represents persisted runtime state for a registered job.
/// </summary>
public sealed record JobRuntimeState
{
    /// <summary>
    /// Gets the stable job name.
    /// </summary>
    public string JobName { get; init; }

    /// <summary>
    /// Gets a value indicating whether the job is enabled when overridden at runtime.
    /// </summary>
    public bool? Enabled { get; init; }

    /// <summary>
    /// Gets a value indicating whether the job is paused.
    /// </summary>
    public bool Paused { get; init; }

    /// <summary>
    /// Gets the created audit timestamp.
    /// </summary>
    public DateTimeOffset CreatedDate { get; init; }

    /// <summary>
    /// Gets the updated audit timestamp.
    /// </summary>
    public DateTimeOffset UpdatedDate { get; init; }
}