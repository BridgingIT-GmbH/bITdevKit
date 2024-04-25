// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;
using Xunit.Sdk;

[TraitDiscoverer(IntegrationTestDiscoverer.TypeName, IntegrationTestDiscoverer.AssemblyName)]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class FeatureAttribute : Attribute, ITraitAttribute
{
    public FeatureAttribute()
    {
    }

    public FeatureAttribute(string name)
    {
        this.Identifier = name;
    }

    public FeatureAttribute(long id)
    {
        this.Identifier = id.ToString();
    }

    public string Identifier { get; }
}