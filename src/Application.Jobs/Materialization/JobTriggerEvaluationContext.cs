// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

/// <summary>
/// Represents the context passed to custom trigger providers.
/// </summary>
/// <param name="Job">The owning job definition.</param>
/// <param name="Trigger">The trigger definition.</param>
/// <param name="Request">The evaluation request.</param>
/// <param name="RuntimeState">The current runtime state.</param>
/// <param name="NowUtc">The current scheduler instant.</param>
public sealed record JobTriggerEvaluationContext(
    JobDefinition Job,
    JobTriggerDefinition Trigger,
    JobTriggerEvaluationRequest Request,
    JobTriggerRuntimeState RuntimeState,
    DateTimeOffset NowUtc);