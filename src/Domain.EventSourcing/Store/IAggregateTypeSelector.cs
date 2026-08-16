// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing.Store;

/// <summary>
/// Defines operations for i aggregate type selector.
/// </summary>
public interface IAggregateTypeSelector
{
    /// <summary>
    /// Finds .
    /// </summary>
    /// <param name="typeName">The type name used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    Type Find(string typeName);
}
