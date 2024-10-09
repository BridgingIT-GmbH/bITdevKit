// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

/// <summary>
///     Specification to check if an entity has the specified identifier.
/// </summary>
/// <typeparam name="T">The type of entity that this specification applies to.</typeparam>
public class HasIdSpecification<T>(object id) : Specification<T>
    where T : IEntity
{
    /// <summary>
    ///     Creates an expression that determines if an entity of type T has a specified Id.
    /// </summary>
    /// <returns>An expression that evaluates to true if the entity's Id matches the specified Id, otherwise false.</returns>
    public override Expression<Func<T, bool>> ToExpression()
    {
        return t => t.Id == id;
    }
}