// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

/// <summary>
/// Represents mutable runtime state required to calculate trigger materialization deterministically.
/// </summary>
/// <param name="ActivatedUtc">The activation instant used to anchor delay-based calculations.</param>
/// <param name="DueUtc">The persisted due instant for one-time and delay-based triggers.</param>
/// <param name="LastMaterializedScheduledUtc">The last scheduled instant materialized for recurring triggers.</param>
/// <param name="HasMaterializedOccurrence">Indicates whether a single-fire trigger already materialized its occurrence.</param>
public sealed record JobTriggerRuntimeState(
    DateTimeOffset? ActivatedUtc,
    DateTimeOffset? DueUtc,
    DateTimeOffset? LastMaterializedScheduledUtc,
    bool HasMaterializedOccurrence,
    bool? Enabled = null,
    bool Paused = false,
    DateTimeOffset? CreatedDate = null,
    DateTimeOffset? UpdatedDate = null,
    DateTimeOffset? LastAcceptedEventUtc = null,
    Guid? LastAcceptedEventId = null)
{
    /// <summary>
    /// Represents an empty runtime state.
    /// </summary>
    public static JobTriggerRuntimeState Empty { get; } = new(null, null, null, false);
}