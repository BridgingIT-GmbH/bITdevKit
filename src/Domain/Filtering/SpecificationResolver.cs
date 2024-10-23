// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using System.Collections.Concurrent;

public static class SpecificationResolver
{
    private static readonly ConcurrentDictionary<string, Type> SpecificationTypes = new ConcurrentDictionary<string, Type>();

    public static void Register<TEntity, TSpecification>(string name = null)
        where TEntity : class, IEntity
        where TSpecification : ISpecification<TEntity>
    {
        Register<TEntity>(typeof(TSpecification), name ?? typeof(TSpecification).Name);
    }

    public static void Register<TEntity>(Type specificationType, string name = null)
        where TEntity : class, IEntity
    {
        if (!typeof(ISpecification<TEntity>).IsAssignableFrom(specificationType))
        {
            throw new ArgumentException($"Type {specificationType.Name} does not implement ISpecification<{typeof(TEntity).Name}>");
        }

        name ??= specificationType.Name;
        if (!SpecificationTypes.TryAdd(name, specificationType))
        {
            throw new InvalidOperationException($"A specification with the name '{name}' has already been registered.");
        }
    }

    public static ISpecification<TEntity> Resolve<TEntity>(object[] arguments)
        where TEntity : class, IEntity
    {
        return Resolve<TEntity>(typeof(TEntity).Name, arguments);
    }

    public static ISpecification<TEntity> Resolve<TEntity>(string name, object[] arguments)
        where TEntity : class, IEntity
    {
        name ??= typeof(TEntity).Name;
        if (!SpecificationTypes.TryGetValue(name, out var specificationType))
        {
            throw new ArgumentException($"No specification registered with name '{name}'");
        }

        return (ISpecification<TEntity>)Activator.CreateInstance(specificationType, arguments);
    }

    public static bool IsRegistered(string name)
    {
        return SpecificationTypes.ContainsKey(name);
    }

    public static Type GetType(string name)
    {
        if (!SpecificationTypes.TryGetValue(name, out var specificationType))
        {
            throw new ArgumentException($"No specification registered with name '{name}'");
        }
        return specificationType;
    }

    public static void Clear()
    {
        SpecificationTypes.Clear();
    }
}