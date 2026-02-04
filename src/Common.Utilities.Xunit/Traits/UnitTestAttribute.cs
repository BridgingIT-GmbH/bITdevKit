// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Xunit.Sdk;

/// <summary>
/// Trait attribute that marks a test method or class as a unit test.
/// This attribute is used to categorize and filter tests during test discovery.
/// </summary>
[TraitDiscoverer(UnitTestDiscoverer.TypeName, UnitTestDiscoverer.AssemblyName)]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class UnitTestAttribute : CategoryAttribute, ITraitAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnitTestAttribute"/> class.
    /// </summary>
    public UnitTestAttribute() : base("UnitTest") { }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnitTestAttribute"/> class with an identifier.
    /// </summary>
    /// <param name="name">The identifier name for this unit test.</param>
    public UnitTestAttribute(string name) : base("UnitTest")
    {
        this.Identifier = name;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnitTestAttribute"/> class with a numeric identifier.
    /// </summary>
    /// <param name="id">The numeric identifier for this unit test.</param>
    public UnitTestAttribute(long id) : base("UnitTest")
    {
        this.Identifier = id.ToString();
    }

    /// <summary>
    /// Gets the identifier for this unit test.
    /// </summary>
    public string Identifier { get; }
}
