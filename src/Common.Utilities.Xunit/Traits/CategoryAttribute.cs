// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;
using Xunit.Sdk;

[TraitDiscoverer(CategoryDiscoverer.TypeName, CategoryDiscoverer.AssemblyName)]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class CategoryAttribute(string categoryName) : Attribute, ITraitAttribute
{
    public string Name { get; } = categoryName;
}