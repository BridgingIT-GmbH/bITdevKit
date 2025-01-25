// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using System.Collections.Concurrent;

/// <summary>
/// Provides methods to register, resolve, and manage specifications for entities. Used by the SpecificationBuilder to resolve specifications by name.
/// </summary>
public static class SpecificationResolver
{
    /// <summary>
    /// Concurrent dictionary that stores the mapping of specification names to their respective types.
    /// </summary>
    private static readonly ConcurrentDictionary<string, Type> SpecificationTypes = [];

    /// <summary>
    /// Registers a specification for a given entity type with an optional name.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <typeparam name="TSpecification">The type of the specification.</typeparam>
    /// <param name="name">The name of the specification. If not provided, the specification type name is used.</param>
    public static void Register<TEntity, TSpecification>(string name = null)
        where TEntity : class, IEntity
        where TSpecification : ISpecification<TEntity>
    {
        Register<TEntity>(typeof(TSpecification), name ?? typeof(TSpecification).Name);
    }

    /// <summary>
    /// Registers a specification type for a given entity type with an optional name.
    /// </summary>
    /// <typeparam name="TEntity">The entity type that the specification is for.</typeparam>
    /// <typeparam name="TSpecification">The specification type to be registered.</typeparam>
    /// <param name="name">An optional name for the specification. If not provided, the name of the specification type will be used.</param>
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

    /// <summary>
    /// Resolve a registered specification for the given entity type using provided arguments.
    /// </summary>
    /// <typeparam name="TEntity">The type of entity that the specification applies to.</typeparam>
    /// <param name="arguments">The arguments to be passed to the specification's constructor.</param>
    /// <returns>An instance of the specification for the given entity type.</returns>
    public static ISpecification<TEntity> Resolve<TEntity>(object[] arguments)
        where TEntity : class, IEntity
    {
        return Resolve<TEntity>(typeof(TEntity).Name, arguments);
    }

    /// <summary>
    /// Resolves a specification of type <see cref="ISpecification{T}"/> for the specified entity type and arguments.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="name">The name of the specification to resolve.</param>
    /// <param name="arguments">The arguments to pass to the specification's constructor.</param>
    /// <returns>An instance of the resolved specification.</returns>
    /// <exception cref="ArgumentException">Thrown when a specification with the given name is not registered.</exception>
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

    /// <summary>
    /// Determines if a specification with the given name is registered.
    /// </summary>
    /// <param name="name">The name of the specification to check.</param>
    /// <returns>True if a specification with the given name is registered; otherwise, false.</returns>
    public static bool IsRegistered(string name)
    {
        return SpecificationTypes.ContainsKey(name);
    }

    /// <summary>
    /// Retrieves the type of the specification registered with the specified name.
    /// </summary>
    /// <param name="name">The name of the registered specification.</param>
    /// <returns>The type of the registered specification.</returns>
    /// <exception cref="ArgumentException">Thrown when no specification is registered with the provided name.</exception>
    public static Type GetType(string name)
    {
        if (!SpecificationTypes.TryGetValue(name, out var specificationType))
        {
            throw new ArgumentException($"No specification registered with name '{name}'");
        }

        return specificationType;
    }

    /// <summary>
    /// Clears all entries from the SpecificationTypes dictionary, effectively removing all registered specifications.
    /// </summary>
    public static void Clear()
    {
        SpecificationTypes.Clear();
    }
}