// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Defines a group constant structure to prevent magic strings.
/// </summary>
public readonly struct Role : IEquatable<Role>
{
    private readonly string value;

    private Role(string value)
    {
        this.value = value;
    }

    public static Role For(string group)
    {
        return new Role(group);
    }

    /// <summary>
    /// Gets the Administrators group value.
    /// </summary>
    public static Role Administrators => new("Administrators");

    /// <summary>
    /// Gets the Contributors group value.
    /// </summary>
    public static Role Contributors => new("Contributors");

    /// <summary>
    /// Gets the Users group value.
    /// </summary>
    public static Role Users => new("Users");

    /// <summary>
    /// Gets the Writers group value.
    /// </summary>
    public static Role Writers => new("Writers");

    /// <summary>
    /// Gets the Readers group value.
    /// </summary>
    public static Role Readers => new("Readers");

    /// <summary>
    /// Gets the Guests group value.
    /// </summary>
    public static Role Guests => new("Guests");

    public bool Equals(Role other)
    {
        return this.value == other.value;
    }

    /// <summary>
    /// Gets the string value of the group.
    /// </summary>
    public override string ToString() => this.value;

    /// <summary>
    /// Implicitly converts the group to its string representation.
    /// </summary>
    public static implicit operator string(Role group) => group.value;
}
