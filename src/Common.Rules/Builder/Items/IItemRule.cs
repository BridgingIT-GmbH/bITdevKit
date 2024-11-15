// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Defines the contract for a rule that can be applied to an item of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of the item.</typeparam>
public interface IItemRule<T>
{
    /// <summary>
    /// Sets the item to be validated according to the rule.
    /// </summary>
    /// <param name="item">The item to be validated.</param>
    void SetItem(T item);
}