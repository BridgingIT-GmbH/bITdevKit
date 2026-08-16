// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Xunit.Sdk;

/// <summary>
///     Marks a test class or method as a system test with an optional identifier.
/// </summary>
[TraitDiscoverer(SystemTestDiscoverer.TypeName, SystemTestDiscoverer.AssemblyName)]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class SystemTestAttribute : CategoryAttribute, ITraitAttribute
{
    /// <summary>
    ///     Initializes a system-test attribute without an identifier.
    /// </summary>
    public SystemTestAttribute() : base("SystemTest") { }

    /// <summary>
    ///     Initializes a system-test attribute with a text identifier.
    /// </summary>
    /// <param name="name">The system-test identifier.</param>
    public SystemTestAttribute(string name) : base("SystemTest")
    {
        this.Identifier = name;
    }

    /// <summary>
    ///     Initializes a system-test attribute with a numeric identifier.
    /// </summary>
    /// <param name="id">The system-test identifier.</param>
    public SystemTestAttribute(long id) : base("SystemTest")
    {
        this.Identifier = id.ToString();
    }

    /// <summary>
    ///     Gets the system-test identifier, when specified.
    /// </summary>
    public string Identifier { get; }
}
