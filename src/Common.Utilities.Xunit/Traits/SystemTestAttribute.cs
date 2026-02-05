// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Xunit.Sdk;

[TraitDiscoverer(SystemTestDiscoverer.TypeName, SystemTestDiscoverer.AssemblyName)]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class SystemTestAttribute : CategoryAttribute, ITraitAttribute
{
    public SystemTestAttribute() : base("SystemTest") { }

    public SystemTestAttribute(string name) : base("SystemTest")
    {
        this.Identifier = name;
    }

    public SystemTestAttribute(long id) : base("SystemTest")
    {
        this.Identifier = id.ToString();
    }

    public string Identifier { get; }
}