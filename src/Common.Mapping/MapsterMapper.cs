// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Mapster;

/// <summary>
///     Maps an object of type <typeparamref name="TSource" /> to <typeparamref name="TDestination" /> by using mapster.
/// </summary>
/// <typeparam name="TSource">The type of the object to map from.</typeparam>
/// <typeparam name="TDestination">The type of the object to map to.</typeparam>
public class MapsterMapper<TSource, TDestination>(TypeAdapterConfig config = null) : IMapper<TSource, TDestination>
{
    private readonly TypeAdapterConfig config = config ?? TypeAdapterConfig.GlobalSettings;

    /// <summary>
    ///     Maps the specified source object into the destination object.
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

    public Result MapResult(TSource source, TDestination destination)
    {
        try
        {
            this.Map(source, destination);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(
                new MappingError(ex, $"Mapping from {typeof(TSource).FullName} to {typeof(TDestination).FullName} failed: {ex.Message}"));
        }
    }
}

/// <summary>
///     Maps an object of type <typeparamref name="TSource" /> to <typeparamref name="TDestination" /> by using mapster.
/// </summary>
public class MapsterMapper(TypeAdapterConfig config = null) : IMapper
{
    private readonly TypeAdapterConfig config = config ?? TypeAdapterConfig.GlobalSettings;

    /// <summary>
    ///     Maps an object of type <typeparamref name="TSource" /> to <typeparamref name="TTarget" />.
    /// </summary>
    /// <param name="source">The source object to map from.</param>
    /// <returns>The mapped object of type <typeparamref name="TTarget" />.</returns>
    public TTarget Map<TSource, TTarget>(TSource source)
        where TTarget : class
    {
        return source is null ? default : source.Adapt<TTarget>(this.config);
    }

    public Result<TTarget> MapResult<TSource, TTarget>(TSource source)
        where TTarget : class
    {
        try
        {
            var result = this.Map<TSource, TTarget>(source);
            return Result<TTarget>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<TTarget>.Failure(
                new MappingError(ex, $"Mapping from {typeof(TSource).FullName} to {typeof(TTarget).FullName} failed: {ex.Message}"));
        }
    }

    /// <summary>
    ///     Maps the properties from the source object to the target object.
    /// </summary>
    /// <param name="source">The source object from which to map properties.</param>
    /// <param name="target">The target object to which properties will be mapped.</param>
    /// <returns>The target object with mapped properties from the source object.</returns>
    public TTarget Map<TSource, TTarget>(TSource source, TTarget target)
        where TTarget : class
    {
        return source is null ? target : source.Adapt(target, this.config);
    }

    public Result<TTarget> MapResult<TSource, TTarget>(TSource source, TTarget target)
        where TTarget : class
    {
        try
        {
            var result = this.Map(source, target);
            return Result<TTarget>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<TTarget>.Failure(
                new MappingError(ex, $"Mapping from {typeof(TSource).FullName} to {typeof(TTarget).FullName} failed: {ex.Message}"));
        }
    }
}