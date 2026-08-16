// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents an error that indicates a conflict with the current state of the resource.
/// </summary>
/// <param name="message"></param>
public class ConcurrencyError(string message = null) : ResultErrorBase(message ?? "Concurrency error")
{
    /// <summary>Initializes a concurrency error with the default message.</summary>
    public ConcurrencyError() : this(null)
    {
    }

    /// <summary>Gets the type name of the entity involved in the conflict, when supplied.</summary>
    public string EntityType { get; init; }

    /// <summary>Gets the identifier of the entity involved in the conflict, when supplied.</summary>
    public string EntityId { get; init; }
}
