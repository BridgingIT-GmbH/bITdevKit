// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using System.Linq.Expressions;
using Specifications;

/// <summary>
///     Provides methods for mapping entities.
/// </summary>
public interface IEntityMapper
{
    /// <summary>
    ///     Maps a source object to a destination object. Creates a new instance of <see cref="TDestination" />.
    /// </summary>
    /// <typeparam name="TDestination">The type of the destination object.</typeparam>
    /// <param name="source">The source entity.</param>
    TDestination Map<TDestination>(object source);

    /// <summary>
    ///     Execute a mapping from the source object to the new destination object.
    /// </summary>
    /// <typeparam name="TSource">Source object type.</typeparam>
    /// <typeparam name="TDestination">Destination object type.</typeparam>
    /// <param name="source">The source object.</param>
    TDestination Map<TSource, TDestination>(TSource source);

    /// <summary>
    ///     Execute a mapping from the source object to the existing destination object.
    /// </summary>
    /// <typeparam name="TSource">Source object type.</typeparam>
    /// <typeparam name="TDestination">Destination object type.</typeparam>
    /// <param name="source">The source object.</param>
    /// <param name="destination">The destination object.</param>
    /// <returns>
    ///     Returns the same <see cref="destination" /> object after the mapping.
    /// </returns>
    TDestination Map<TSource, TDestination>(TSource source, TDestination destination);

    TDestination MapExpression<TDestination>(LambdaExpression expression)
        where TDestination : LambdaExpression;

    /// <summary>
    ///     Maps the specified TSource specification to a predicate for TDestination types.
    /// </summary>
    /// <param name="specification">The specification.</param>
    Expression<Func<TDestination, bool>> MapSpecification<TSource, TDestination>(ISpecification<TSource> specification);
}