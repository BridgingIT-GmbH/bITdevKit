// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

/// <summary>
/// Represents one child failure returned by a bulk scheduler operation.
/// </summary>
public sealed class JobBulkOperationFailureModel
{
    /// <summary>
    /// Gets or sets the child occurrence identifier when available.
    /// </summary>
    public Guid? OccurrenceId { get; set; }

    /// <summary>
    /// Gets or sets the affected job name when available.
    /// </summary>
    public string JobName { get; set; }

    /// <summary>
    /// Gets or sets the failure message.
    /// </summary>
    public string Message { get; set; }
}