// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Xunit.Sdk;

/// <summary>
///     Associates a test class or method with an optional module identifier.
/// </summary>
[TraitDiscoverer(ModuleDiscoverer.TypeName, ModuleDiscoverer.AssemblyName)]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class ModuleAttribute : Attribute, ITraitAttribute
{
    /// <summary>
    ///     Initializes a module attribute without an identifier.
    /// </summary>
    public ModuleAttribute() { }

    /// <summary>
    ///     Initializes a module attribute with a text identifier.
    /// </summary>
    /// <param name="name">The module identifier.</param>
    public ModuleAttribute(string name)
    {
        this.Identifier = name;
    }

    /// <summary>
    ///     Initializes a module attribute with a numeric identifier.
    /// </summary>
    /// <param name="id">The module identifier.</param>
    public ModuleAttribute(long id)
    {
        this.Identifier = id.ToString();
    }

    /// <summary>
    ///     Gets the module identifier, when specified.
    /// </summary>
    public string Identifier { get; }
}
