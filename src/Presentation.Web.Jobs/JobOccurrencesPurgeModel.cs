// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Jobs;

using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Common;

/// <summary>
/// Represents the query parameters used to purge retained job occurrences.
/// </summary>
public sealed class JobOccurrencesPurgeModel
{
    /// <summary>
    /// Gets or sets the older than.
    /// </summary>
    public DateTimeOffset? OlderThan { get; set; }

    /// <summary>
    /// Gets or sets the statuses.
    /// </summary>
    public JobOccurrenceStatus[] Statuses { get; set; } = [];

    /// <summary>
    /// Gets or sets the job name.
    /// </summary>
    public string JobName { get; set; }

    /// <summary>
    /// Gets or sets the trigger name.
    /// </summary>
    public string TriggerName { get; set; }

    /// <summary>
    /// Gets or sets the is archived.
    /// </summary>
    public bool? IsArchived { get; set; }

    /// <summary>
    /// Gets or sets the dry run.
    /// </summary>
    public bool DryRun { get; set; }

    /// <summary>
    /// Gets or sets the batch size.
    /// </summary>
    public int BatchSize { get; set; } = 100;
}
