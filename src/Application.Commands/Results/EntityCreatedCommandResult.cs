// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands;

/// <summary>
/// Represents entity created command result.
/// </summary>
/// <param name="entityId">The entity identifier.</param>
public class EntityCreatedCommandResult(string entityId)
{
    /// <summary>
    ///     The entity id
    /// </summary>
    public string EntityId { get; } = entityId;
}

/// <summary>
/// Represents entity created command result.
/// </summary>
/// <typeparam name="TId">The id type.</typeparam>
/// <param name="entityId">The entity identifier.</param>
public class EntityCreatedCommandResult<TId>(TId entityId)
{
    /// <summary>
    ///     The entity id
    /// </summary>
    public TId EntityId { get; } = entityId;
}
