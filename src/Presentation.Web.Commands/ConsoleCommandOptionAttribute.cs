// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Attribute to mark an option (named --key) for a command.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class ConsoleCommandOptionAttribute(string name) : Attribute
{
    /// <summary>Gets the canonical long option name (without leading dashes).</summary>
    public string Name { get; } = name;

    /// <summary>Gets or sets the short alias (without leading dash).</summary>
    public string Alias { get; init; }

    /// <summary>Gets or sets the human readable description.</summary>
    public string Description { get; init; }

    /// <summary>Gets or sets a value indicating whether the option is required.</summary>
    public bool Required { get; init; }

    /// <summary>Gets or sets the default value if not provided.</summary>
    public object Default { get; init; }
}
