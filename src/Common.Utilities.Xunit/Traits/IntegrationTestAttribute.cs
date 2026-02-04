// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Xunit.Sdk;

[TraitDiscoverer(IntegrationTestDiscoverer.TypeName, IntegrationTestDiscoverer.AssemblyName)]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class IntegrationTestAttribute : CategoryAttribute, ITraitAttribute
{
    public IntegrationTestAttribute() : base("IntegrationTest") { }

    public IntegrationTestAttribute(string name) : base("IntegrationTest")
    {
        this.Identifier = name;
    }

    public IntegrationTestAttribute(long id) : base("IntegrationTest")
    {
        this.Identifier = id.ToString();
    }

    public string Identifier { get; }
}