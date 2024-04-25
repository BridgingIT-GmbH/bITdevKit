// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;
using Xunit.Sdk;

[TraitDiscoverer(IntegrationTestDiscoverer.TypeName, IntegrationTestDiscoverer.AssemblyName)]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class IntegrationTestAttribute : Attribute, ITraitAttribute
{
    public IntegrationTestAttribute()
    {
    }

    public IntegrationTestAttribute(string name)
    {
        this.Identifier = name;
    }

    public IntegrationTestAttribute(long id)
    {
        this.Identifier = id.ToString();
    }

    public string Identifier { get; }
}