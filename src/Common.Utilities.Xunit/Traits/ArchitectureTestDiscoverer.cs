// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Xunit.Abstractions;
using Xunit.Sdk;

public class ArchitectureTestDiscoverer : ITraitDiscoverer
{
    /// <summary>
    /// Gets the fully qualified type name of the <see cref="ArchitectureTestDiscoverer"/>.
    /// </summary>
    internal const string TypeName = "BridgingIT.DevKit.Common." + nameof(ArchitectureTestDiscoverer);

    /// <summary>
    /// Gets the assembly name containing the <see cref="ArchitectureTestDiscoverer"/>.
    /// </summary>
    internal const string AssemblyName = "BridgingIT.DevKit.Common.Utilities.Xunit";

    /// <summary>
    /// Discovers traits from <see cref="ArchitectureTestAttribute"/> attributes applied to test methods or classes.
    /// </summary>
    /// <param name="traitAttribute">The architecture test attribute to extract traits from.</param>
    /// <returns>An enumerable of key-value pairs representing the traits.</returns>
    public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
    {
        var identifier = traitAttribute.GetNamedArgument<string>("Identifier");

        yield return new KeyValuePair<string, string>("Category", "ArchitectureTest");

        if (!identifier.IsNullOrEmpty())
        {
            yield return new KeyValuePair<string, string>("ArchitectureTest", identifier);
        }
    }
}