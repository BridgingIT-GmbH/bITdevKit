// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>
/// Represents one occurrence created by trigger evaluation.
/// </summary>
/// <param name="OccurrenceKey">The deterministic occurrence key.</param>
/// <param name="JobName">The originating job name.</param>
/// <param name="TriggerName">The originating trigger name.</param>
/// <param name="TriggerType">The originating trigger type.</param>
/// <param name="DueUtc">The due instant in UTC.</param>
/// <param name="ScheduledUtc">The scheduled instant in UTC when the trigger is schedule-based.</param>
/// <param name="Data">The occurrence payload.</param>
/// <param name="DataType">The occurrence payload type.</param>
/// <param name="Properties">The occurrence properties.</param>
/// <param name="IdempotencyKey">The idempotency key derived from the trigger input.</param>
public sealed record JobOccurrenceMaterialization(
    string OccurrenceKey,
    string JobName,
    string TriggerName,
    JobTriggerType TriggerType,
    DateTimeOffset DueUtc,
    DateTimeOffset? ScheduledUtc,
    object Data,
    Type DataType,
    PropertyBag Properties,
    string IdempotencyKey);