// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

/// <summary>
/// Executes registered orchestrations in memory.
/// </summary>
public interface IOrchestrationExecutor
{
    /// <summary>
    /// Executes a registered orchestration definition in memory.
    /// </summary>
    /// <typeparam name="TOrchestration">The orchestration type.</typeparam>
    /// <typeparam name="TData">The orchestration data type.</typeparam>
    /// <param name="data">The initial orchestration data.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The final in-memory orchestration context.</returns>
    Task<OrchestrationContext<TData>> ExecuteAsync<TOrchestration, TData>(
        TData data,
        CancellationToken cancellationToken = default)
        where TOrchestration : class, IOrchestration<TData>
        where TData : class, IOrchestrationData;
}
