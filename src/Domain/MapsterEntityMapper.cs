// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Mapping;

using System.Linq.Expressions;
using Domain.Repositories;
using BridgingIT.DevKit.Domain;
using IMapper = MapsterMapper.IMapper;

/// <summary>
/// Represents mapster entity mapper.
/// </summary>
public class MapsterEntityMapper : IEntityMapper
{
    private readonly IMapper mapper;

    /// <summary>
    /// Initializes a new instance of the <c>MapsterEntityMapper</c> class.
    /// </summary>
    /// <param name="mapper">The mapper used to transform values.</param>
    public MapsterEntityMapper(IMapper mapper)
    {
        EnsureArg.IsNotNull(mapper, nameof(mapper));

        this.mapper = mapper;
    }

    /// <summary>
    /// Executes the map operation.
    /// </summary>
    /// <typeparam name="TDestination">The destination type.</typeparam>
    /// <param name="source">The source sequence.</param>
    /// <returns>The result of the operation.</returns>
    public TDestination Map<TDestination>(object source)
    {
        if (source is null)
        {
            return default;
        }

        return this.mapper.Map<TDestination>(source);
    }

    /// <summary>
    /// Executes the map operation.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TDestination">The destination type.</typeparam>
    /// <param name="source">The source sequence.</param>
    /// <returns>The result of the operation.</returns>
    public TDestination Map<TSource, TDestination>(TSource source)
    {
        if (source is null)
        {
            return default;
        }

        return this.mapper.Map<TDestination>(source);
    }

    /// <summary>
    /// Executes the map operation.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TDestination">The destination type.</typeparam>
    /// <param name="source">The source sequence.</param>
    /// <param name="destination">The destination used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public TDestination Map<TSource, TDestination>(TSource source, TDestination destination)
    {
        if (source is null)
        {
            return default;
        }

        return this.mapper.Map(source, destination);
    }

    /// <summary>
    /// Executes the map expression operation.
    /// </summary>
    /// <typeparam name="TDestination">The destination type.</typeparam>
    /// <param name="expression">The expression used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public TDestination MapExpression<TDestination>(LambdaExpression expression)
        where TDestination : LambdaExpression
    {
        if (expression is null)
        {
            return default;
        }

        throw new NotSupportedException();
    }

    /// <summary>
    /// Executes the map specification operation.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TDestination">The destination type.</typeparam>
    /// <param name="specification">The specification used to filter entities.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public Expression<Func<TDestination, bool>> MapSpecification<TSource, TDestination>(
        ISpecification<TSource> specification)
    {
        if (specification is null)
        {
            return default;
        }

        throw new NotSupportedException();
    }
}
