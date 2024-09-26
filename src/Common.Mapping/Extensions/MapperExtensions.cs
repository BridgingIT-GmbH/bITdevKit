// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     <see cref="IMapper{TSource, TTarget}" /> extension methods.
/// </summary>
public static class MapperExtensions
{
    /// <summary>
    ///     Maps the specified source object to a new object with a type of <typeparamref name="TTarget" />.
    /// </summary>
    /// <typeparam name="TSource">The type of the source object.</typeparam>
    /// <typeparam name="TTarget">The type of the target object.</typeparam>
    /// <param name="mapper">The mapper instance.</param>
    /// <param name="source">The source object.</param>
    /// <returns>The mapped object of type <typeparamref name="TTarget" />.</returns>
    public static TTarget Map<TSource, TTarget>(this IMapper<TSource, TTarget> mapper, TSource source)
        where TTarget : class, new()
    {
        EnsureArg.IsNotNull(mapper, nameof(mapper));

        if (source is null)
        {
            return null;
        }

        var target = Factory<TTarget>.Create();
        mapper.Map(source, target);

        return target;
    }

    /// <summary>
    ///     Maps the specified source object to a new object with a type of <typeparamref name="TTarget" />.
    /// </summary>
    /// <typeparam name="TSource">The type of the source object.</typeparam>
    /// <typeparam name="TTarget">The type of the target object.</typeparam>
    /// <param name="mapper">The mapper instance.</param>
    /// <param name="source">The source object.</param>
    /// <param name="safe">Handles null sources.</param>
    /// <returns>The mapped object of type <typeparamref name="TTarget" />.</returns>
    public static TTarget Map<TSource, TTarget>(this IMapper<TSource, TTarget> mapper, TSource source, bool safe)
        where TSource : class, new()
        where TTarget : class, new()
    {
        EnsureArg.IsNotNull(mapper, nameof(mapper));

        if (source is null && !safe)
        {
            return null;
        }

        if (source is null)
        {
            source = Factory<TSource>.Create();
        }

        var target = Factory<TTarget>.Create();
        mapper.Map(source, target);

        return target;
    }

    /// <summary>
    ///     Maps the collection of <typeparamref name="TSource" /> into a IEnumerable of /// <typeparamref name="TTarget" />.
    /// </summary>
    /// <typeparam name="TSource">The type of the source objects.</typeparam>
    /// <typeparam name="TTarget">The type of the target objects.</typeparam>
    /// <param name="mapper">The mapper instance.</param>
    /// <param name="sources">The source collection.</param>
    /// <returns>An array of <typeparamref name="TTarget" />.</returns>
    public static IEnumerable<TTarget> Map<TSource, TTarget>(
        this IMapper<TSource, TTarget> mapper,
        IEnumerable<TSource> sources)
        where TTarget : class, new()
    {
        EnsureArg.IsNotNull(mapper, nameof(mapper));

        if (sources?.Any() == true)
        {
            foreach (var source in sources)
            {
                yield return mapper.Map(source);
            }
        }
    }

    /// <summary>
    ///     Maps the array of <typeparamref name="TSource" /> into an IEnumerable of /// <typeparamref name="TTarget" />.
    /// </summary>
    /// <typeparam name="TSource">The type of the source objects.</typeparam>
    /// <typeparam name="TTarget">The type of the target objects.</typeparam>
    /// <param name="mapper">The mapper instance.</param>
    /// <param name="sources">The source objects.</param>
    /// <returns>An array of <typeparamref name="TTarget" />.</returns>
    public static IEnumerable<TTarget> Map<TSource, TTarget>(this IMapper<TSource, TTarget> mapper, TSource[] sources)
        where TTarget : class, new()
    {
        EnsureArg.IsNotNull(mapper, nameof(mapper));

        if (sources?.Any() == true)
        {
            for (var i = 0; i < sources.Length; ++i)
            {
                var source = sources[i];
                var target = Factory<TTarget>.Create();
                mapper.Map(source, target);

                yield return target;
            }
        }
    }

    ///// <summary>
    ///// Maps the specified source object to a new object with a type of <typeparamref name="TTarget"/>.
    ///// </summary>
    ///// <typeparam name="TSource">The type of the source object.</typeparam>
    ///// <typeparam name="TTarget">The type of the target object.</typeparam>
    ///// <param name="mapper">The mapper instance.</param>
    ///// <param name="source">The source object.</param>
    ///// <returns>The mapped object of type <typeparamref name="TTarget"/>.</returns>
    //public static TTarget Map<TSource, TTarget>(
    //    this IMapper mapper,
    //    TSource source)
    //    where TTarget : class, new()
    //{
    //    EnsureArg.IsNotNull(mapper, nameof(mapper));

    //    if (source is null)
    //    {
    //        return null;
    //    }

    //    return mapper.Map<TSource, TTarget>(source);
    //}

    ///// <summary>
    ///// Maps the specified source object to a new object with a type of <typeparamref name="TTarget"/>.
    ///// </summary>
    ///// <typeparam name="TSource">The type of the source object.</typeparam>
    ///// <typeparam name="TTarget">The type of the target object.</typeparam>
    ///// <param name="mapper">The mapper instance.</param>
    ///// <param name="source">The source object.</param>
    ///// <param name="safe">Handles null sources.</param>
    ///// <returns>The mapped object of type <typeparamref name="TTarget"/>.</returns>
    //public static TTarget Map<TSource, TTarget>(
    //    this IMapper mapper,
    //    TSource source,
    //    bool safe)
    //    where TSource : class, new()
    //    where TTarget : class, new()
    //{
    //    EnsureArg.IsNotNull(mapper, nameof(mapper));

    //    if (source is null && !safe)
    //    {
    //        return null;
    //    }
    //    else if (source is null)
    //    {
    //        source = Factory<TSource>.Create();
    //    }

    //    return mapper.Map<TSource, TTarget>(source);
    //}

    /// <summary>
    ///     Maps the collection of <typeparamref name="TSource" /> into a IEnumerable of /// <typeparamref name="TTarget" />.
    /// </summary>
    /// <typeparam name="TSource">The type of the source objects.</typeparam>
    /// <typeparam name="TTarget">The type of the target objects.</typeparam>
    /// <param name="mapper">The mapper instance.</param>
    /// <param name="sources">The source collection.</param>
    /// <returns>An array of <typeparamref name="TTarget" />.</returns>
    public static IEnumerable<TTarget> Map<TSource, TTarget>(this IMapper mapper, IEnumerable<TSource> sources)
        where TTarget : class, new()
    {
        EnsureArg.IsNotNull(mapper, nameof(mapper));

        if (sources?.Any() == true)
        {
            foreach (var source in sources)
            {
                yield return mapper.Map<TSource, TTarget>(source);
            }
        }
    }

    /// <summary>
    ///     Maps the array of <typeparamref name="TSource" /> into an IEnumerable of /// <typeparamref name="TTarget" />.
    /// </summary>
    /// <typeparam name="TSource">The type of the source objects.</typeparam>
    /// <typeparam name="TTarget">The type of the target objects.</typeparam>
    /// <param name="mapper">The mapper instance.</param>
    /// <param name="sources">The source objects.</param>
    /// <returns>An array of <typeparamref name="TTarget" />.</returns>
    public static IEnumerable<TTarget> Map<TSource, TTarget>(this IMapper mapper, TSource[] sources)
        where TTarget : class, new()
    {
        EnsureArg.IsNotNull(mapper, nameof(mapper));

        if (sources?.Any() == true)
        {
            for (var i = 0; i < sources.Length; ++i)
            {
                yield return mapper.Map<TSource, TTarget>(sources[i]);
            }
        }
    }
}