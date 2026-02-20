// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

/// <summary>
/// Represents the outcome of an entity change operation, tracking whether changes occurred.
/// </summary>
public class EntityChangeOperationOutcome
{
    /// <summary>
    /// Gets a value indicating whether the operation resulted in a change to the entity.
    /// </summary>
    public bool HasChanged { get; init; }

    /// <summary>
    /// Gets an optional description of what changed during the operation.
    /// </summary>
    public string ChangeDescription { get; init; }

    /// <summary>
    /// Creates an outcome indicating that a change occurred.
    /// </summary>
    /// <param name="description">Optional description of the change.</param>
    /// <returns>An OperationOutcome with HasChanged set to true.</returns>
    public static EntityChangeOperationOutcome Changed(string description = null) =>
        new() { HasChanged = true, ChangeDescription = description };

    /// <summary>
    /// Creates an outcome indicating that no change occurred.
    /// </summary>
    /// <returns>An OperationOutcome with HasChanged set to false.</returns>
    public static EntityChangeOperationOutcome NoChange() =>
        new() { HasChanged = false };
}
