// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;
using Xunit.Sdk;

[TraitDiscoverer(ModuleDiscoverer.TypeName, ModuleDiscoverer.AssemblyName)]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class ModuleAttribute : Attribute, ITraitAttribute
{
    public ModuleAttribute()
    {
    }

    public ModuleAttribute(string name)
    {
        this.Identifier = name;
    }

    public ModuleAttribute(long id)
    {
        this.Identifier = id.ToString();
    }

    public string Identifier { get; }
}