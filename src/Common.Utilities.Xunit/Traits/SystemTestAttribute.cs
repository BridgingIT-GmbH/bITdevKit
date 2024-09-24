﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Xunit.Sdk;

[TraitDiscoverer(SystemTestDiscoverer.TypeName, SystemTestDiscoverer.AssemblyName)]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class SystemTestAttribute : Attribute, ITraitAttribute
{
    public SystemTestAttribute() { }

    public SystemTestAttribute(string name)
    {
        this.Identifier = name;
    }

    public SystemTestAttribute(long id)
    {
        this.Identifier = id.ToString();
    }

    public string Identifier { get; }
}