// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Maps an object of type <typeparamref name="TSource" /> to <typeparamref name="TTarget" />.
/// </summary>
/// <typeparam name="TSource">The type of the object to map from.</typeparam>
/// <typeparam name="TTarget">The type of the object to map to.</typeparam>
public interface IMapper<in TSource, in TTarget>
{
    /// <summary>
    ///     Maps the specified source object into the destination object.
    /// </summary>
    /// <param name="source">The source object to map from.</param>
    /// <param name="target">The target object to map to.</param>
    void Map(TSource source, TTarget target);

    Result MapResult(TSource source, TTarget target);
}

/// <summary>
///     Defines a contract for mapping objects from one type to another.
/// </summary>
public interface IMapper
{
    /// <summary>
    ///     Maps the specified source object of type <typeparamref name="TSource" /> into the destination object of type
    ///     <typeparamref name="TTarget" />.
    /// </summary>
    /// <param name="source">The source object to map from.</param>
    TTarget Map<TSource, TTarget>(TSource source)
        where TTarget : class;

    Result<TTarget> MapResult<TSource, TTarget>(TSource source)
        where TTarget : class;

    /// <summary>
    ///     Maps the specified source object of type <typeparamref name="TSource" /> into the destination object of type
    ///     <typeparamref name="TTarget" />.
    /// </summary>
    /// <param name="source">The source object to map from.</param>
    /// <param name="target">The target object to map to.</param>
    TTarget Map<TSource, TTarget>(TSource source, TTarget target)
        where TTarget : class;

    Result<TTarget> MapResult<TSource, TTarget>(TSource source, TTarget target)
        where TTarget : class;
}