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
/// <remarks>
///     Initializes a new instance of the <see cref="ObjectMapper{TSource, TDestination}" /> class.
/// </remarks>
/// <param name="action">The action.</param>
public class ObjectMapper<TSource, TTarget>(Action<TSource, TTarget> action) : IMapper<TSource, TTarget>
{
    private readonly Action<TSource, TTarget> action = action;

    /// <summary>
    ///     Maps the specified source object into the destination object.
    /// </summary>
    /// <param name="source">The source object to map from.</param>
    /// <param name="target">The target object to map to.</param>
    public void Map(TSource source, TTarget target)
    {
        if (source is not null && target is not null && this.action is not null)
        {
            this.action(source, target);
        }
    }
}

/// <summary>
///     Provides functionalities to map objects of type <typeparamref name="TSource" /> to objects of type
///     <typeparamref name="TTarget" />.
/// </summary>
/// <remarks>
///     Implements the <see cref="IMapper" /> interface and allows for configuration of custom mapping logic.
/// </remarks>
public class ObjectMapper : IMapper
{
    /// <summary>
    ///     A dictionary holding mappings between source and target types.
    ///     The key is a tuple containing the source type and target type.
    ///     The value is a delegate that performs the mapping operation.
    ///     Mapping delegates can be either a function that maps a source object
    ///     to a newly created target object or an action that maps a source
    ///     object to an existing target object.
    /// </summary>
    private readonly Dictionary<(Type, Type), Delegate> mappings = [];

    /// <summary>
    ///     Maps the specified source object into a new instance of the target object type.
    /// </summary>
    /// <param name="source">The source object to map from.</param>
    /// <typeparam name="TSource">The type of the source object.</typeparam>
    /// <typeparam name="TTarget">The type of the target object.</typeparam>
    /// <returns>An instance of the target object type, mapped from the source object.</returns>
    public TTarget Map<TSource, TTarget>(TSource source)
        where TTarget : class
    {
        if (source is null)
        {
            return default;
        }

        var key = (typeof(TSource), typeof(TTarget));
        if (!this.mappings.TryGetValue(key, out var mappingDelegate))
        {
            throw new InvalidOperationException(
                $"No mapping configuration found for {typeof(TSource)} to {typeof(TTarget)}");
        }

        if (mappingDelegate is Func<TSource, TTarget> mapFunc)
        {
            return mapFunc(source);
        }

        if (mappingDelegate is Action<TSource, TTarget> mapAction)
        {
            var target = Activator.CreateInstance<TTarget>();
            mapAction(source, target);

            return target;
        }

        throw new InvalidOperationException(
            $"Invalid mapping delegate type for {typeof(TSource)} to {typeof(TTarget)}");
    }

    /// <summary>
    ///     Maps the specified source object into the destination object.
    /// </summary>
    /// <param name="source">The source object to map from.</param>
    /// <param name="target">The target object to map to.</param>
    /// <returns>
    ///     The mapped target object.
    /// </returns>
    public TTarget Map<TSource, TTarget>(TSource source, TTarget target)
        where TTarget : class
    {
        if (source is null)
        {
            return target;
        }

        var key = (typeof(TSource), typeof(TTarget));
        if (!this.mappings.TryGetValue(key, out var mappingDelegate))
        {
            throw new InvalidOperationException(
                $"No mapping configuration found for {typeof(TSource)} to {typeof(TTarget)}");
        }

        if (mappingDelegate is Func<TSource, TTarget> mapFunc)
        {
            return mapFunc(source);
        }

        if (mappingDelegate is Action<TSource, TTarget> mapAction)
        {
            mapAction(source, target);

            return target;
        }

        throw new InvalidOperationException(
            $"Invalid mapping delegate type for {typeof(TSource)} to {typeof(TTarget)}");
    }

    /// <summary>
    ///     Configures a mapping for the specified source and target types.
    /// </summary>
    /// <returns>An instance of <see cref="ObjectMapperConfiguration{TSource, TTarget}" /> to further configure the mapping.</returns>
    public ObjectMapperConfiguration<TSource, TTarget> For<TSource, TTarget>()
    {
        return new ObjectMapperConfiguration<TSource, TTarget>(this);
    }

    /// <summary>
    ///     Adds a mapping function between the specified source and target types.
    /// </summary>
    /// <param name="mapping">The function that defines the mapping from TSource to TTarget.</param>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TTarget">The target type.</typeparam>
    internal void AddMapping<TSource, TTarget>(Func<TSource, TTarget> mapping)
    {
        var key = (typeof(TSource), typeof(TTarget));
        this.mappings[key] = mapping;
    }

    /// <summary>
    ///     Adds a mapping action between the specified source and target types.
    /// </summary>
    /// <param name="mapping">The action delegate that defines the mapping from the source type to the target type.</param>
    /// <typeparam name="TSource">The type of the source object.</typeparam>
    /// <typeparam name="TTarget">The type of the target object.</typeparam>
    internal void AddMapping<TSource, TTarget>(Action<TSource, TTarget> mapping)
    {
        var key = (typeof(TSource), typeof(TTarget));
        this.mappings[key] = mapping;
    }
}