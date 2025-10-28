// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Attribute to mark a positional argument.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class ConsoleCommandArgumentAttribute(int order) : Attribute
{
    /// <summary>Gets the zero-based positional order of the argument.</summary>
    public int Order { get; } = order;

    /// <summary>Gets or sets the argument description.</summary>
    public string Description { get; init; }

    /// <summary>Gets or sets a value indicating whether the argument is required.</summary>
    public bool Required { get; init; }
}
