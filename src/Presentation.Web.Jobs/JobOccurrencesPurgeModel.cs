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
    public DateTimeOffset? OlderThan { get; set; }

    public JobOccurrenceStatus[] Statuses { get; set; } = [];

    public string JobName { get; set; }

    public string TriggerName { get; set; }

    public bool? IsArchived { get; set; }

    public bool DryRun { get; set; }

    public int BatchSize { get; set; } = 100;
}