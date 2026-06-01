// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

/// <summary>
/// Represents the accepted dispatch details for one occurrence.
/// </summary>
public sealed class JobDispatchResult
{
    /// <summary>
    /// Gets or sets the stable job name.
    /// </summary>
    public string JobName { get; set; }

    /// <summary>
    /// Gets or sets the trigger name.
    /// </summary>
    public string TriggerName { get; set; }

    /// <summary>
    /// Gets or sets the occurrence identifier.
    /// </summary>
    public Guid OccurrenceId { get; set; }

    /// <summary>
    /// Gets or sets the correlation identifier.
    /// </summary>
    public string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the acceptance time in UTC.
    /// </summary>
    public DateTimeOffset AcceptedUtc { get; set; }
}