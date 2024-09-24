// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using global::AutoMapper;

/// <summary>
///     Maps an object of type <typeparamref name="TSource" /> to <typeparamref name="TDestination" /> by using automapper.
/// </summary>
/// <typeparam name="TSource">The type of the object to map from.</typeparam>
/// <typeparam name="TDestination">The type of the object to map to.</typeparam>
public class AutoMapper<TSource, TDestination> : IMapper<TSource, TDestination>
{
    private readonly global::AutoMapper.IMapper mapper;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AutoMapper{TSource, TDestination}" /> class.
    /// </summary>
    /// <param name="mapper">The mapper.</param>
    public AutoMapper(global::AutoMapper.IMapper mapper)
    {
        EnsureArg.IsNotNull(mapper, nameof(mapper));

        this.mapper = mapper;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="AutoMapper{TSource, TDestination}" /> class.
    /// </summary>
    public AutoMapper(MapperConfiguration configuration)
    {
        EnsureArg.IsNotNull(configuration, nameof(configuration));

        this.mapper = new Mapper(configuration);
    }

    /// <summary>
    ///     Maps the specified source object into the destination object.
    /// </summary>
    /// <param name="source">The source object to map from.</param>
    /// <param name="destination">The destination object to map to.</param>
    public void Map(TSource source, TDestination destination)
    {
        if (source is not null && destination is not null)
        {
            this.mapper.Map(source, destination);
        }
    }
}

/// <summary>
///     Maps an object of type <typeparamref name="TSource" /> to <typeparamref name="TDestination" /> by using automapper.
/// </summary>
public class AutoMapper : IMapper
{
    private readonly global::AutoMapper.IMapper mapper;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AutoMapper" /> class.
    /// </summary>
    /// <param name="mapper">The mapper.</param>
    public AutoMapper(global::AutoMapper.IMapper mapper)
    {
        EnsureArg.IsNotNull(mapper, nameof(mapper));

        this.mapper = mapper;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="AutoMapper" /> class.
    /// </summary>
    public AutoMapper(MapperConfiguration configuration)
    {
        EnsureArg.IsNotNull(configuration, nameof(configuration));

        this.mapper = new Mapper(configuration);
    }

    /// <summary>
    ///     Maps the specified source object into the destination object.
    /// </summary>
    /// <param name="source">The source object to map from.</param>
    public TTarget Map<TSource, TTarget>(TSource source)
        where TTarget : class
    {
        if (source is null)
        {
            return default;
        }

        return this.mapper.Map<TTarget>(source);
    }

    public TTarget Map<TSource, TTarget>(TSource source, TTarget target)
        where TTarget : class
    {
        if (source is null)
        {
            return default;
        }

        return this.mapper.Map(source, target);
    }
}