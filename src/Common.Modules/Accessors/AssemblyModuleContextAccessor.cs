// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Resolves modules by comparing their names with the module-name attribute on a type's assembly.
/// </summary>
/// <param name="modules">The modules available for resolution.</param>
public class AssemblyModuleContextAccessor(IEnumerable<IModule> modules = null) : IModuleContextAccessor
{
    private readonly IEnumerable<IModule> modules = modules.SafeNull();

    /// <inheritdoc/>
    public virtual IModule Find(Type type)
    {
        return this.modules.FirstOrDefault(m =>
            m.Name.SafeEquals(ModuleName.From(type, false))); // TODO: cache this ModuleName lookup for better perf?
    }
}
