// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.Generic;
using Xunit.Abstractions;
using Xunit.Sdk;

public class CategoryDiscoverer : ITraitDiscoverer
{
    internal const string TypeName = "BridgingIT.DevKit.Common." + nameof(CategoryDiscoverer);
    internal const string AssemblyName = "BridgingIT.DevKit.Common.Utilities.Xunit";

    public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
    {
        var categoryName = traitAttribute.GetNamedArgument<string>("Name");

        if (!string.IsNullOrWhiteSpace(categoryName))
        {
            yield return new KeyValuePair<string, string>("Category", categoryName);
        }
    }
}