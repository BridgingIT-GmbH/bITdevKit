// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Reflection;

/// <summary>
/// Finds the database adapters of the provider assemblies that ship with the application. Each provider assembly
/// declares its adapter with <see cref="DevAppInstanceDatabaseAdapterAttribute"/>.
/// </summary>
/// <remarks>
/// The locator loads the DevKit Entity Framework provider assemblies by name when they are present, because modules
/// usually register their DbContext after <c>AddDevAppInstance</c> runs and the runtime loads assemblies on first use.
/// It also reads assemblies that are already loaded, which covers third-party adapters. The SQL Server adapter comes
/// first, then PostgreSQL, then SQLite, then any other adapter.
/// </remarks>
/// <example>
/// <code>
/// var adapters = DevAppInstanceDatabaseAdapterLocator.Locate();
/// </code>
/// </example>
public static class DevAppInstanceDatabaseAdapterLocator
{
    private const string LogKey = "BDK";

    /// <summary>
    /// The DevKit provider assemblies the locator loads by name.
    /// </summary>
    public static readonly IReadOnlyList<string> WellKnownAssemblyNames =
    [
        "BridgingIT.DevKit.Infrastructure.EntityFramework.SqlServer",
        "BridgingIT.DevKit.Infrastructure.EntityFramework.Postgres",
        "BridgingIT.DevKit.Infrastructure.EntityFramework.Sqlite",
    ];

    /// <summary>
    /// Returns one adapter per kind from the provider assemblies of the application.
    /// </summary>
    /// <returns>The adapters, in a stable order.</returns>
    /// <example>
    /// <code>
    /// foreach (var adapter in DevAppInstanceDatabaseAdapterLocator.Locate())
    /// {
    ///     Console.WriteLine(adapter.Kind);
    /// }
    /// </code>
    /// </example>
    public static IReadOnlyList<IDevAppInstanceDatabaseAdapter> Locate()
    {
        var assemblies = new List<Assembly>();
        foreach (var name in WellKnownAssemblyNames)
        {
            var assembly = TryLoad(name);
            if (assembly is not null)
            {
                assemblies.Add(assembly);
            }
        }

        assemblies.AddRange(AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic)
            .OrderBy(a => a.FullName, StringComparer.Ordinal));

        return Locate(assemblies);
    }

    /// <summary>
    /// Returns one adapter per kind from the given assemblies, in assembly order.
    /// </summary>
    /// <param name="assemblies">The assemblies to read.</param>
    /// <returns>The adapters.</returns>
    /// <example>
    /// <code>
    /// var adapters = DevAppInstanceDatabaseAdapterLocator.Locate([typeof(TaskHarborDbContext).Assembly]);
    /// </code>
    /// </example>
    public static IReadOnlyList<IDevAppInstanceDatabaseAdapter> Locate(IEnumerable<Assembly> assemblies)
    {
        var result = new List<IDevAppInstanceDatabaseAdapter>();
        foreach (var assembly in (assemblies ?? []).Distinct())
        {
            IEnumerable<DevAppInstanceDatabaseAdapterAttribute> attributes;
            try
            {
                attributes = assembly.GetCustomAttributes<DevAppInstanceDatabaseAdapterAttribute>();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // Adapter discovery is best effort and must never stop a host. Provider registrations still resolve their adapter directly.
                if (ex is not (FileNotFoundException or FileLoadException or TypeLoadException or BadImageFormatException))
                {
                    ReportSkipped(assembly.GetName().Name, ex);
                }

                continue;
            }

            foreach (var attribute in attributes)
            {
                try
                {
                    var adapter = Create(attribute.AdapterType);
                    if (adapter is not null && !result.Any(a => a.Kind == adapter.Kind))
                    {
                        result.Add(adapter);
                    }
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    ReportSkipped(attribute.AdapterType?.FullName, ex);
                }
            }
        }

        return result;
    }

    private static void ReportSkipped(string source, Exception exception)
    {
        var error = exception is TargetInvocationException { InnerException: not null } tie ? tie.InnerException : exception;
        Console.Error.WriteLine($"[{LogKey}] worktree database adapter skipped (source={source}, error={error.GetType().Name}: {error.Message})");
    }

    private static IDevAppInstanceDatabaseAdapter Create(Type type)
    {
        if (type is null || !typeof(IDevAppInstanceDatabaseAdapter).IsAssignableFrom(type))
        {
            return null;
        }

        var instance = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        if (instance is not null && typeof(IDevAppInstanceDatabaseAdapter).IsAssignableFrom(instance.PropertyType))
        {
            return (IDevAppInstanceDatabaseAdapter)instance.GetValue(null);
        }

        return type.GetConstructor(Type.EmptyTypes) is not null ? (IDevAppInstanceDatabaseAdapter)Activator.CreateInstance(type) : null;
    }

    private static Assembly TryLoad(string name)
    {
        try
        {
            return Assembly.Load(new AssemblyName(name));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return null;
        }
    }
}
