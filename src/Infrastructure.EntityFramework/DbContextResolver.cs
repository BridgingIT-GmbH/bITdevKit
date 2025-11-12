// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

/// <summary>
/// Resolves Entity Framework <see cref="DbContext"/> instances at runtime
/// for transactional behaviors and other cross-cutting concerns.
/// </summary>
/// <remarks>
/// <para>
/// Supports two resolution forms:
/// - By context name (string): logical name that may omit the "DbContext" suffix
///   (e.g., "Core" resolves to "CoreDbContext"). The resolver locates the
///   corresponding DbContext type once and caches it, then retrieves the instance
///   from DI.
/// - By type (Type): the exact <see cref="DbContext"/> derived type. The resolver
///   retrieves the instance from DI without any reflection or lookup.
/// </para>
/// <para>
/// Requirements:
/// - The DbContext type must be registered in DI using AddDbContext&lt;TContext&gt;().
/// - For name-based resolution, the DbContext type must be loadable in the current AppDomain.
/// </para>
/// </remarks>
public interface IDbContextResolver
{
    /// <summary>
    /// Resolves a <see cref="DbContext"/> by a logical context name that may
    /// omit the "DbContext" suffix (e.g., "Core" or "CoreDbContext").
    /// </summary>
    /// <param name="contextName">
    /// The logical context name or the DbContext simple type name. If the value
    /// does not end with "DbContext", the suffix will be appended for matching.
    /// </param>
    /// <returns>
    /// A <see cref="DbContext"/> instance resolved from the DI container.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the name is null/empty, no matching DbContext type can be found,
    /// or the type is not registered in DI.
    /// </exception>
    DbContext Resolve(string contextName);

    /// <summary>
    /// Resolves a <see cref="DbContext"/> by its concrete <see cref="Type"/>.
    /// </summary>
    /// <param name="contextType">The concrete type derived from <see cref="DbContext"/>.</param>
    /// <returns>
    /// A <see cref="DbContext"/> instance resolved from the DI container.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="contextType"/> is null or not a <see cref="DbContext"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the type is not registered in DI.
    /// </exception>
    DbContext Resolve(Type contextType);
}

/// <summary>
/// Default implementation of <see cref="IDbContextResolver"/> that resolves
/// DbContexts by context name or by concrete type using the DI container.
/// </summary>
/// <remarks>
/// - Name-based resolution normalizes names by appending "DbContext" if missing
///   and performs a one-time type lookup across loaded assemblies, cached per name.
/// - Type-based resolution directly requests the instance from DI without reflection.
/// </remarks>
public sealed class DbContextResolver(IServiceProvider serviceProvider) : IDbContextResolver
{
    private readonly IServiceProvider serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    private static readonly ConcurrentDictionary<string, Type> TypeCache = new(StringComparer.OrdinalIgnoreCase);

    public DbContext Resolve(string contextName)
    {
        if (string.IsNullOrWhiteSpace(contextName))
        {
            throw new InvalidOperationException("No database context name provided.");
        }

        var normalized = NormalizeContextName(contextName.Trim());
        var contextType = TypeCache.GetOrAdd(normalized, FindDbContextType);

        return this.ResolveInternal(contextType);
    }

    public DbContext Resolve(Type contextType)
    {
        if (contextType is null || !typeof(DbContext).IsAssignableFrom(contextType))
        {
            throw new ArgumentException("Type must be a non-null DbContext type.", nameof(contextType));
        }

        return this.ResolveInternal(contextType);
    }

    private DbContext ResolveInternal(Type contextType)
    {
        if (this.serviceProvider.GetRequiredService(contextType) is not DbContext context)
        {
            throw new InvalidOperationException($"Database context '{contextType.FullName}' is not registered. Ensure AddDbContext<{contextType.Name}>() is called.");
        }

        return context;
    }

    private static string NormalizeContextName(string name)
    {
        return name.EndsWith("DbContext", StringComparison.OrdinalIgnoreCase)
            ? name
            : name + "DbContext";
    }

    private static Type FindDbContextType(string name)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic)
            {
                continue;
            }

            Type[] types;
            try { types = assembly.GetTypes(); }
            catch { continue; }

            var match = types.FirstOrDefault(t =>
                !t.IsAbstract &&
                typeof(DbContext).IsAssignableFrom(t) &&
                string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                return match;
            }
        }

        throw new InvalidOperationException($"No database context type with name '{name}' was found in loaded assemblies. Ensure the assembly containing is loaded and the name matches.");
    }
}