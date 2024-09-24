// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Xunit.Abstractions;
using Xunit.Sdk;

public class FeatureDiscoverer : ITraitDiscoverer
{
    internal const string TypeName = "BridgingIT.DevKit.Common." + nameof(FeatureDiscoverer);
    internal const string AssemblyName = "BridgingIT.DevKit.Common.Utilities.Xunit";

    public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
    {
        var identifier = traitAttribute.GetNamedArgument<string>("Identifier");

        yield return new KeyValuePair<string, string>("Category", "Feature");

        if (!identifier.IsNullOrEmpty())
        {
            yield return new KeyValuePair<string, string>("Feature", identifier);
        }
    }
}