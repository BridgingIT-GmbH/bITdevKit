// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.Generic;

public class TypeNameModuleContextAccessor : IModuleContextAccessor
{
    private readonly IEnumerable<IModule> modules;
    private readonly Func<Type, string> moduleNameSelector = (t) => t.FullName.SliceFrom("Modules.").SliceTill(".");

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

    public virtual IModule Find(Type type)
    {
        return this.modules.FirstOrDefault(m =>
            m.Name.SafeEquals(this.moduleNameSelector(type)));
    }
}