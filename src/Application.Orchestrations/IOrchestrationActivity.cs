// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

/// <summary>
/// Represents an executable orchestration activity.
/// </summary>
/// <typeparam name="TData">The orchestration data type.</typeparam>
public interface IOrchestrationActivity<TData>
    where TData : class, IOrchestrationData
{
    /// <summary>
    /// Executes the activity.
    /// </summary>
    /// <param name="context">The orchestration context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The activity outcome.</returns>
    Task<OrchestrationOutcome> ExecuteAsync(OrchestrationContext<TData> context, CancellationToken cancellationToken = default);
}