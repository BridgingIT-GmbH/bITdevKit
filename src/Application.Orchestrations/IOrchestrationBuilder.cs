// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

/// <summary>
/// Provides the fluent authoring surface for orchestration definitions.
/// </summary>
/// <typeparam name="TData">The orchestration data type.</typeparam>
public interface IOrchestrationBuilder<TData>
    where TData : class, IOrchestrationData
{
    /// <summary>
    /// Adds a state to the orchestration definition.
    /// </summary>
    /// <param name="name">The unique state name.</param>
    /// <param name="configure">The state configuration callback.</param>
    /// <returns>The current builder.</returns>
    IOrchestrationBuilder<TData> State(string name, Action<IOrchestrationStateBuilder<TData>> configure);
}