// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

/// <summary>
/// Represents a built orchestration definition.
/// </summary>
/// <typeparam name="TData">The orchestration data type.</typeparam>
/// <param name="name">The orchestration name.</param>
/// <param name="initialState">The first state to execute.</param>
/// <param name="states">The states keyed by state name.</param>
public class OrchestrationDefinition<TData>(
    string name,
    string initialState,
    IReadOnlyDictionary<string, OrchestrationStateDefinition<TData>> states)
    where TData : class, IOrchestrationData
{
    /// <summary>
    /// Gets the orchestration name.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Gets the initial state name.
    /// </summary>
    public string InitialState { get; } = initialState;

    /// <summary>
    /// Gets the defined states.
    /// </summary>
    public IReadOnlyDictionary<string, OrchestrationStateDefinition<TData>> States { get; } = states;
}
