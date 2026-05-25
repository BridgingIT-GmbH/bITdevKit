// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Marks a type as an orchestration definition for the supplied orchestration data type.
/// </summary>
/// <typeparam name="TData">The orchestration data type.</typeparam>
public interface IOrchestration<out TData>
    where TData : class, IOrchestrationData
{
    /// <summary>
    /// Gets the orchestration definition name.
    /// </summary>
    string Name { get; }
}
