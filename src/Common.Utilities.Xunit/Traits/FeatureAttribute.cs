// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Xunit.Sdk;

/// <summary>
///     Associates a test class or method with an optional feature identifier.
/// </summary>
[TraitDiscoverer(IntegrationTestDiscoverer.TypeName, IntegrationTestDiscoverer.AssemblyName)]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class FeatureAttribute : Attribute, ITraitAttribute
{
    /// <summary>
    ///     Initializes a feature attribute without an identifier.
    /// </summary>
    public FeatureAttribute() { }

    /// <summary>
    ///     Initializes a feature attribute with a text identifier.
    /// </summary>
    /// <param name="name">The feature identifier.</param>
    public FeatureAttribute(string name)
    {
        this.Identifier = name;
    }

    /// <summary>
    ///     Initializes a feature attribute with a numeric identifier.
    /// </summary>
    /// <param name="id">The feature identifier.</param>
    public FeatureAttribute(long id)
    {
        this.Identifier = id.ToString();
    }

    /// <summary>
    ///     Gets the feature identifier, when specified.
    /// </summary>
    public string Identifier { get; }
}
