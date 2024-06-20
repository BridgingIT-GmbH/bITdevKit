// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Mapster;

/// <summary>
/// Maps an object of type <typeparamref name="TSource"/> to <typeparamref name="TDestination"/> by using mapster.
/// </summary>
/// <typeparam name="TSource">The type of the object to map from.</typeparam>
/// <typeparam name="TDestination">The type of the object to map to.</typeparam>
public class MapsterMapper<TSource, TDestination>(TypeAdapterConfig config = null) : IMapper<TSource, TDestination>
{
    private readonly TypeAdapterConfig config = config ?? TypeAdapterConfig.GlobalSettings;

    /// <summary>
    /// Maps the specified source object into the destination object.
    /// </summary>
    /// <param name="source">The source object to map from.</param>
    /// <param name="destination">The destination object to map to.</param>
    public void Map(TSource source, TDestination destination)
    {
        if (source is not null && destination is not null)
        {
            source.Adapt(destination, this.config);
        }
    }
}

/// <summary>
/// Maps an object of type <typeparamref name="TSource"/> to <typeparamref name="TDestination"/> by using mapster.
/// </summary>
/// <typeparam name="TSource">The type of the object to map from.</typeparam>
/// <typeparam name="TDestination">The type of the object to map to.</typeparam>
public class MapsterMapper(TypeAdapterConfig config = null) : IMapper
{
    private readonly TypeAdapterConfig config = config ?? TypeAdapterConfig.GlobalSettings;

    public TTarget Map<TSource, TTarget>(TSource source)
        where TTarget : class
    {
        if (source is null)
        {
            return default;
        }

        return source.Adapt<TTarget>(this.config);
    }
}