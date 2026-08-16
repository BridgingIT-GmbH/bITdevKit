// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Xunit.Sdk;

/// <summary>
///     Assigns a named xUnit category trait to a test class or method.
/// </summary>
/// <param name="name">The category name.</param>
[TraitDiscoverer(CategoryDiscoverer.TypeName, CategoryDiscoverer.AssemblyName)]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class CategoryAttribute(string name) : Attribute, ITraitAttribute
{
    /// <summary>
    ///     Gets the category name.
    /// </summary>
    public string Name { get; } = name;
}
