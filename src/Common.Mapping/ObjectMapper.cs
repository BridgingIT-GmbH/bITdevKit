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
public class ObjectMapper<TSource, TTarget> : IMapper<TSource, TTarget>
{
    private readonly Action<TSource, TTarget> action;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectMapper{TSource, TDestination}"/> class.
    /// </summary>
    /// <param name="action">The action.</param>
    public ObjectMapper(Action<TSource, TTarget> action)
    {
        this.action = action;
    }

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
    private readonly Dictionary<(Type, Type), Delegate> mappings = new();

    public TTarget Map<TSource, TTarget>(TSource source)
        where TTarget : class
    {
        if (source is null)
        {
            return default;
        }

        var key = (typeof(TSource), typeof(TTarget));
        if (!this.mappings.TryGetValue(key, out var mappingFunc))
        {
            throw new InvalidOperationException($"No mapping configuration found for {typeof(TSource)} to {typeof(TTarget)}");
        }

        var mapFunc = (Func<TSource, TTarget>)mappingFunc;
        return mapFunc(source);
    }

    public ObjectMapperConfiguration<TSource, TTarget> For<TSource, TTarget>()
    {
        return new ObjectMapperConfiguration<TSource, TTarget>(this);
    }

    internal void AddMapping<TSource, TTarget>(Func<TSource, TTarget> mapping)
    {
        var key = (typeof(TSource), typeof(TTarget));
        if (this.mappings.ContainsKey(key))
        {
            this.mappings.Remove(key);
        }

        this.mappings[key] = mapping;
    }
}