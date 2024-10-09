// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Mapping;

using System.Linq.Expressions;
using Domain.Repositories;
using BridgingIT.DevKit.Domain;
using MapsterMapper;

public class MapsterEntityMapper : IEntityMapper
{
    private readonly IMapper mapper;

    public MapsterEntityMapper(IMapper mapper)
    {
        EnsureArg.IsNotNull(mapper, nameof(mapper));

        this.mapper = mapper;
    }

    public TDestination Map<TDestination>(object source)
    {
        if (source is null)
        {
            return default;
        }

        return this.mapper.Map<TDestination>(source);
    }

    public TDestination Map<TSource, TDestination>(TSource source)
    {
        if (source is null)
        {
            return default;
        }

        return this.mapper.Map<TDestination>(source);
    }

    public TDestination Map<TSource, TDestination>(TSource source, TDestination destination)
    {
        if (source is null)
        {
            return default;
        }

        return this.mapper.Map(source, destination);
    }

    public TDestination MapExpression<TDestination>(LambdaExpression expression)
        where TDestination : LambdaExpression
    {
        if (expression is null)
        {
            return default;
        }

        throw new NotSupportedException();
    }

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