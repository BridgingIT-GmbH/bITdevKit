// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public class AssemblyModuleContextAccessor(IEnumerable<IModule> modules = null) : IModuleContextAccessor
{
    private readonly IEnumerable<IModule> modules = modules.SafeNull();

    public virtual IModule Find(Type type)
    {
        return this.modules.FirstOrDefault(m =>
            m.Name.SafeEquals(ModuleName.From(type, false))); // TODO: cache this ModuleName lookup for better perf?
    }
}