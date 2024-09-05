// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;

/// <summary>
/// Maps an object of type <typeparamref name="TSource"/> to <typeparamref name="TTarget"/>.
/// </summary>
/// <typeparam name="TSource">The type of the object to map from.</typeparam>
/// <typeparam name="TTarget">The type of the object to map to.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="ObjectMapper{TSource, TDestination}"/> class.
/// </remarks>
/// <param name="action">The action.</param>
public class ObjectMapper<TSource, TTarget>(Action<TSource, TTarget> action)
    : IMapper<TSource, TTarget>
{
    private readonly Action<TSource, TTarget> action = action;

    /// <summary>
    /// Maps the specified source object into the destination object.
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

public class ObjectMapper : IMapper
{
    private readonly Dictionary<(Type, Type), Delegate> mappings = [];

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
            throw new InvalidOperationException($"No mapping configuration found for {typeof(TSource)} to {typeof(TTarget)}");
        }

        if (mappingDelegate is Func<TSource, TTarget> mapFunc)
        {
            return mapFunc(source);
        }
        else if (mappingDelegate is Action<TSource, TTarget> mapAction)
        {
            var target = Activator.CreateInstance<TTarget>();
            mapAction(source, target);
            return target;
        }
        else
        {
            throw new InvalidOperationException($"Invalid mapping delegate type for {typeof(TSource)} to {typeof(TTarget)}");
        }
    }

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
            throw new InvalidOperationException($"No mapping configuration found for {typeof(TSource)} to {typeof(TTarget)}");
        }

        if (mappingDelegate is Func<TSource, TTarget> mapFunc)
        {
            return mapFunc(source);
        }
        else if (mappingDelegate is Action<TSource, TTarget> mapAction)
        {
            mapAction(source, target);
            return target;
        }
        else
        {
            throw new InvalidOperationException($"Invalid mapping delegate type for {typeof(TSource)} to {typeof(TTarget)}");
        }
    }

    public ObjectMapperConfiguration<TSource, TTarget> For<TSource, TTarget>()
    {
        return new ObjectMapperConfiguration<TSource, TTarget>(this);
    }

    internal void AddMapping<TSource, TTarget>(Func<TSource, TTarget> mapping)
    {
        var key = (typeof(TSource), typeof(TTarget));
        this.mappings[key] = mapping;
    }

    internal void AddMapping<TSource, TTarget>(Action<TSource, TTarget> mapping)
    {
        var key = (typeof(TSource), typeof(TTarget));
        this.mappings[key] = mapping;
    }
}