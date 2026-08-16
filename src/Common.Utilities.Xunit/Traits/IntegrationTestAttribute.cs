// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Xunit.Sdk;

/// <summary>
///     Marks a test class or method as an integration test with an optional identifier.
/// </summary>
[TraitDiscoverer(IntegrationTestDiscoverer.TypeName, IntegrationTestDiscoverer.AssemblyName)]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class IntegrationTestAttribute : CategoryAttribute, ITraitAttribute
{
    /// <summary>
    ///     Initializes an integration-test attribute without an identifier.
    /// </summary>
    public IntegrationTestAttribute() : base("IntegrationTest") { }

    /// <summary>
    ///     Initializes an integration-test attribute with a text identifier.
    /// </summary>
    /// <param name="name">The integration-test identifier.</param>
    public IntegrationTestAttribute(string name) : base("IntegrationTest")
    {
        this.Identifier = name;
    }

    /// <summary>
    ///     Initializes an integration-test attribute with a numeric identifier.
    /// </summary>
    /// <param name="id">The integration-test identifier.</param>
    public IntegrationTestAttribute(long id) : base("IntegrationTest")
    {
        this.Identifier = id.ToString();
    }

    /// <summary>
    ///     Gets the integration-test identifier, when specified.
    /// </summary>
    public string Identifier { get; }
}
