// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Specifications;

/// <summary>
///     A static class containing extension methods for collections of specifications.
/// </summary>
public static class SpecificationExtensions
{
    /// <summary>
    ///     Checks if the given entity satisfies all specifications in the provided collection.
    /// </summary>
    /// <typeparam name="T">The type of entity being evaluated.</typeparam>
    /// <param name="source">A collection of specifications to evaluate against the entity.</param>
    /// <param name="entity">The entity to be evaluated.</param>
    /// <returns>
    ///     True if the entity satisfies all specifications, false otherwise. If the specification collection is null,
    ///     returns true.
    /// </returns>
    public static bool IsSatisfiedBy<T>(IEnumerable<ISpecification<T>> source, T entity)
    {
        if (source is null)
        {
            return true;
        }

        var specifications = source as ISpecification<T>[] ?? source.ToArray();

        return specifications.Any() != true || specifications.All(specification => specification.IsSatisfiedBy(entity));
    }
}