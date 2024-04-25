// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;
using global::AutoMapper;
using global::AutoMapper.Internal;

public static class AutoMapperExtensions
{
    /// <summary>
    /// Ignores all properties that are not explicitly mapped.
    /// </summary>
    /// <typeparam name="TDest"></typeparam>
    public static IMappingExpression<TSource, TDest> IgnoreAllUnmapped<TSource, TDest>(
        this IMappingExpression<TSource, TDest> expression)
    {
        expression.ForAllMembers(opt => opt.Ignore());
        return expression;
    }

    /// <summary>
    /// Ignores null values in source.
    /// </summary>
    /// <typeparam name="TDest"></typeparam>
    public static IMappingExpression<TSource, TDest> IgnoreNullValues<TSource, TDest>(
        this IMappingExpression<TSource, TDest> expression)
    {
        expression.ForAllMembers(opt => opt.Condition((src, dest, srcMember) => srcMember is not null));
        return expression;
    }

    public static void IgnoreUnmapped(this IProfileExpression profile)
    {
        profile.Internal().ForAllMaps(IgnoreUnmappedProperties);
    }

    public static void IgnoreUnmapped(this IProfileExpression profile, Func<TypeMap, bool> filter)
    {
        profile.Internal().ForAllMaps((map, expr) =>
        {
            if (filter(map))
            {
                IgnoreUnmappedProperties(map, expr);
            }
        });
    }

    public static void IgnoreUnmapped(this IProfileExpression profile, Type source, Type destination)
    {
        profile.IgnoreUnmapped((TypeMap map) => map.SourceType == source && map.DestinationType == destination);
    }

    public static void IgnoreUnmapped<TSrc, TDest>(this IProfileExpression profile)
    {
        profile.IgnoreUnmapped(typeof(TSrc), typeof(TDest));
    }

    private static void IgnoreUnmappedProperties(TypeMap map, IMappingExpression expression)
    {
        foreach (var propertyName in map.GetUnmappedPropertyNames())
        {
            if (map.SourceType.GetProperty(propertyName) is not null)
            {
                expression.ForSourceMember(propertyName, opt => opt.DoNotValidate());
            }

            if (map.DestinationType.GetProperty(propertyName) is not null)
            {
                expression.ForMember(propertyName, opt => opt.Ignore());
            }
        }
    }
}