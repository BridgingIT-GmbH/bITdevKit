﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Xunit.Sdk;

[TraitDiscoverer(UnitTestDiscoverer.TypeName, UnitTestDiscoverer.AssemblyName)]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class UnitTestAttribute : Attribute, ITraitAttribute
{
    public UnitTestAttribute() { }

    public UnitTestAttribute(string name)
    {
        this.Identifier = name;
    }

    public UnitTestAttribute(long id)
    {
        this.Identifier = id.ToString();
    }

    public string Identifier { get; }
}