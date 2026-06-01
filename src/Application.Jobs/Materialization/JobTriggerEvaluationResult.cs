// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

/// <summary>
/// Represents the result of evaluating one trigger.
/// </summary>
/// <param name="RuntimeState">The updated runtime state.</param>
/// <param name="Occurrences">The occurrences to persist.</param>
public sealed record JobTriggerEvaluationResult(
    JobTriggerRuntimeState RuntimeState,
    IReadOnlyList<JobOccurrenceMaterialization> Occurrences);