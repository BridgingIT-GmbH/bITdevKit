// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
///     Stores the modules selected for one application host in deterministic lifecycle order.
/// </summary>
/// <remarks>
///     Register one instance per service collection. Modules are ordered by priority, name, and concrete type.
/// </remarks>
/// <example>
/// <code>
/// var registry = new ModuleRegistry();
/// registry.Add(new CustomerModule());
/// services.AddSingleton(registry);
/// </code>
/// </example>
public sealed class ModuleRegistry : IModuleRegistry
{
    private static readonly StringComparer NameComparer = StringComparer.OrdinalIgnoreCase;
    private readonly List<IModule> modules = [];
    private readonly ReadOnlyCollection<IModule> readOnlyModules;
    private readonly HashSet<Type> registeredModuleTypes = [];
    private readonly List<ServiceDescriptor> moduleServiceDescriptors = [];
    private bool infrastructureRegistered;

    /// <summary>
    ///     Initializes an empty module registry.
    /// </summary>
    public ModuleRegistry()
    {
        this.readOnlyModules = this.modules.AsReadOnly();
    }

    /// <inheritdoc />
    public IReadOnlyList<IModule> Modules => this.readOnlyModules;

    /// <summary>
    ///     Finds the registered module whose concrete type exactly matches the specified type.
    /// </summary>
    /// <param name="moduleType">The concrete module type to find.</param>
    /// <returns>The registered module, or <see langword="null" /> when the type is not registered.</returns>
    public IModule Find(Type moduleType)
    {
        return this.modules.FirstOrDefault(module => module.GetType() == moduleType);
    }

    /// <summary>
    ///     Adds a module and keeps the registry in deterministic lifecycle order.
    /// </summary>
    /// <param name="module">The module instance to add.</param>
    /// <returns>The selected module and whether it was newly added.</returns>
    public (IModule Module, bool Added) Add(IModule module)
    {
        ArgumentNullException.ThrowIfNull(module);

        var moduleType = module.GetType();
        var existingByType = this.Find(moduleType);
        if (existingByType is not null)
        {
            if (!ReferenceEquals(existingByType, module))
            {
                throw new InvalidOperationException(
                    $"Module type '{moduleType.FullName}' is already registered with a different instance. " +
                    "Use the original instance or create a separate host.");
            }

            return (existingByType, false);
        }

        if (string.IsNullOrWhiteSpace(module.Name))
        {
            throw new InvalidOperationException($"Module type '{moduleType.FullName}' must provide a non-empty name.");
        }

        var existingByName = this.modules.FirstOrDefault(existing => NameComparer.Equals(existing.Name, module.Name));
        if (existingByName is not null)
        {
            throw new InvalidOperationException(
                $"Module name '{module.Name}' is already used by module type '{existingByName.GetType().FullName}'. " +
                $"Module type '{moduleType.FullName}' cannot use the same name. Module names are compared using " +
                "ordinal-ignore-case comparison.");
        }

        this.modules.Add(module);
        this.modules.Sort(CompareModules);

        return (module, true);
    }

    /// <summary>
    ///     Determines whether the module's registration callback completed for this registry.
    /// </summary>
    /// <param name="moduleType">The concrete module type to check.</param>
    /// <returns><see langword="true" /> when registration completed; otherwise, <see langword="false" />.</returns>
    public bool IsRegistered(Type moduleType)
    {
        return this.registeredModuleTypes.Contains(moduleType);
    }

    /// <summary>
    ///     Marks a module type as successfully registered.
    /// </summary>
    /// <param name="moduleType">The concrete module type whose registration completed.</param>
    public void MarkRegistered(Type moduleType)
    {
        this.registeredModuleTypes.Add(moduleType);
    }

    /// <summary>
    ///     Removes a module after an unsuccessful registration attempt.
    /// </summary>
    /// <param name="module">The module to remove.</param>
    public void Remove(IModule module)
    {
        this.modules.Remove(module);
        this.registeredModuleTypes.Remove(module.GetType());
    }

    /// <summary>
    ///     Synchronizes the ordered module instances registered in the service collection.
    /// </summary>
    /// <param name="services">The service collection that owns this registry.</param>
    public void SynchronizeModuleServices(IServiceCollection services)
    {
        foreach (var descriptor in this.moduleServiceDescriptors)
        {
            services.Remove(descriptor);
        }

        this.moduleServiceDescriptors.Clear();

        foreach (var module in this.modules)
        {
            var descriptor = ServiceDescriptor.Singleton(typeof(IModule), module);
            services.Add(descriptor);
            this.moduleServiceDescriptors.Add(descriptor);
        }
    }

    /// <summary>
    ///     Marks registry infrastructure as registered exactly once.
    /// </summary>
    /// <returns><see langword="true" /> on the first call; otherwise, <see langword="false" />.</returns>
    public bool TryRegisterInfrastructure()
    {
        if (this.infrastructureRegistered)
        {
            return false;
        }

        this.infrastructureRegistered = true;
        return true;
    }

    private static int CompareModules(IModule left, IModule right)
    {
        var priorityComparison = left.Priority.CompareTo(right.Priority);
        if (priorityComparison != 0)
        {
            return priorityComparison;
        }

        var nameComparison = NameComparer.Compare(left.Name, right.Name);
        if (nameComparison != 0)
        {
            return nameComparison;
        }

        return StringComparer.Ordinal.Compare(left.GetType().FullName, right.GetType().FullName);
    }
}
