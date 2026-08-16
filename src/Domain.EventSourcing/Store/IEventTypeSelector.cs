// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing.Store;

/// <summary>
/// Defines operations for i event type selector.
/// </summary>
public interface IEventTypeSelector
{
    /// <summary>
    /// Finds type.
    /// </summary>
    /// <param name="typename">The typename used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    Type FindType(string typename);
}
