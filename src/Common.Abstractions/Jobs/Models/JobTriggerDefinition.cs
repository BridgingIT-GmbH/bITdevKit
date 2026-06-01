// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>
/// Represents a resolved code-first trigger definition.
/// </summary>
public sealed record JobTriggerDefinition
{
    /// <summary>
    /// Gets or sets the stable trigger name.
    /// </summary>
    public string TriggerName { get; init; }

    /// <summary>
    /// Gets or sets the trigger type.
    /// </summary>
    public JobTriggerType TriggerType { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the trigger is enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Gets or sets the optional priority override.
    /// </summary>
    public int? Priority { get; init; }

    /// <summary>
    /// Gets or sets the optional timeout override.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Gets or sets the optional retry override.
    /// </summary>
    public JobRetryPolicy RetryPolicy { get; init; }

    /// <summary>
    /// Gets or sets the trigger data.
    /// </summary>
    public object Data { get; init; }

    /// <summary>
    /// Gets or sets the time zone used for schedule calculation.
    /// </summary>
    public TimeZoneInfo TimeZone { get; init; } = TimeZoneInfo.Utc;

    /// <summary>
    /// Gets or sets the declared data type.
    /// </summary>
    public Type DataType { get; init; }

    /// <summary>
    /// Gets or sets the missed-occurrence policy.
    /// </summary>
    public JobMissedOccurrencePolicy MissedOccurrencePolicy { get; init; } = JobMissedOccurrencePolicy.Skip;

    /// <summary>
    /// Gets or sets the properties.
    /// </summary>
    public PropertyBag Properties { get; init; } = new();

    /// <summary>
    /// Gets or sets the eligible scheduler instance targets for this trigger override.
    /// </summary>
    public IReadOnlyList<string> TargetInstances { get; init; }

    /// <summary>
    /// Gets or sets the schedule string when the trigger is schedule-based.
    /// </summary>
    public string Schedule { get; init; }

    /// <summary>
    /// Gets or sets the due time when the trigger is one-time.
    /// </summary>
    public DateTimeOffset? DueUtc { get; init; }

    /// <summary>
    /// Gets or sets the delay when the trigger is delay-based.
    /// </summary>
    public TimeSpan? Delay { get; init; }

    /// <summary>
    /// Gets or sets the scheduler-owned calendar definition for <see cref="JobTriggerType.Calendar"/> definitions.
    /// </summary>
    public JobCalendarDefinition Calendar { get; init; }

    /// <summary>
    /// Gets or sets the custom trigger provider type for <see cref="JobTriggerType.Custom"/> definitions.
    /// </summary>
    public Type CustomTriggerProviderType { get; init; }

    /// <summary>
    /// Gets or sets the logical event source for <see cref="JobTriggerType.Event"/> definitions.
    /// </summary>
    public string EventSource { get; init; }

    /// <summary>
    /// Gets or sets the accepted event payload type for <see cref="JobTriggerType.Event"/> definitions.
    /// </summary>
    public Type EventDataType { get; init; }
}
