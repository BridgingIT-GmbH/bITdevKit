// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Xunit.Sdk;

/// <summary>
/// Trait attribute that marks a test method or class as an architecture test.
/// This attribute is used to categorize and filter tests during test discovery.
/// </summary>
[TraitDiscoverer(UnitTestDiscoverer.TypeName, UnitTestDiscoverer.AssemblyName)]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class ArchitectureTestAttribute : CategoryAttribute, ITraitAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ArchitectureTestAttribute"/> class.
    /// </summary>
    public ArchitectureTestAttribute() : base("ArchitectureTest") { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ArchitectureTestAttribute"/> class with an identifier.
    /// </summary>
    /// <param name="name">The identifier name for this architecture test.</param>
    public ArchitectureTestAttribute(string name) : base("ArchitectureTest")
    {
        this.Identifier = name;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ArchitectureTestAttribute"/> class with a numeric identifier.
    /// </summary>
    /// <param name="id">The numeric identifier for this architecture test.</param>
    public ArchitectureTestAttribute(long id) : base("ArchitectureTest")
    {
        this.Identifier = id.ToString();
    }

    /// <summary>
    /// Gets the identifier for this architecture test.
    /// </summary>
    public string Identifier { get; }
}