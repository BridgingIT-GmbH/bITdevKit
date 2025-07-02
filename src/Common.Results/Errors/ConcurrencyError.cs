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
    public ConcurrencyError() : this(null)
    {
    }

    public string EntityType { get; init; }

    public string EntityId { get; init; }
}