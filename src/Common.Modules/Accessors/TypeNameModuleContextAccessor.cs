// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// TypeNameModuleContextAccessor provides a mechanism to access module contexts based on the type names.
/// </summary>
/// <remarks>
/// This class implements the <see cref="IModuleContextAccessor"/> interface and allows retrieving modules by their type name.
/// A customizable delegate function can be used to select the module name from a given type.
/// </remarks>
public class TypeNameModuleContextAccessor : IModuleContextAccessor
{
    /// <summary>
    /// A delegate function used to select the module name from a specified type.
    /// </summary>
    /// <remarks>
    /// This function is typically used within the <see cref="TypeNameModuleContextAccessor"/> class to extract
    /// a module name from its <see cref="Type"/> representation.
    /// By default, this function extracts the module name by slicing the type's full name between "Modules."
    /// and the next "." character.
    /// </remarks>
    private readonly Func<Type, string> moduleNameSelector = t => t.FullName.SliceFrom("Modules.").SliceTill(".");

    /// <summary>
    /// Represents an accessor for module context which allows finding modules by their types.
    /// </summary>
    private readonly IEnumerable<IModule> modules;

    /// <summary>
    /// Provides a mechanism to access module context information by type name.
    /// </summary>
    public TypeNameModuleContextAccessor(
        IEnumerable<IModule> modules = null,
        Func<Type, string> moduleNameSelector = null)
    {
        this.modules = modules.SafeNull();

        if (moduleNameSelector is not null)
        {
            this.moduleNameSelector = moduleNameSelector;
        }
    }

    /// <summary>
    /// Finds an IModule instance that corresponds to the provided type based on the module name selector.
    /// </summary>
    /// <param name="type">The type for which the corresponding module needs to be found.</param>
    /// <returns>An IModule instance if a matching module is found; otherwise, null.</returns>
    public virtual IModule Find(Type type)
    {
        return this.modules.FirstOrDefault(m => m.Name.SafeEquals(this.moduleNameSelector(type)));
    }
}