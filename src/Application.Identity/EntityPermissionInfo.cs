// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Identity;

using System.Diagnostics;

/// <summary>
/// Represents detailed information about a permission granted to a specific entity.
/// </summary>
[DebuggerDisplay("{EntityType} [{Permission}] user={UserId}, role={RoleName}")]
public class EntityPermissionInfo : IEquatable<EntityPermissionInfo>
{
    /// <summary>
    /// Gets or sets the permission value.
    /// </summary>
    public string Permission { get; set; }

    /// <summary>
    /// Gets or sets the source of the permission (e.g., "Direct", "Role:Admins", "Parent", "Default:ReadOnlyProvider").
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the ID of the source (e.g., role id, parent entity id).
    /// </summary>
    //public object SourceId { get; set; }

    /// <summary>
    /// Gets or sets the entity type this permission applies to.
    /// </summary>
    public string EntityType { get; set; }

    /// <summary>
    /// Gets or sets the entity ID this permission applies to (null for type-wide permissions).
    /// </summary>
    public string EntityId { get; set; }

    /// <summary>
    /// Gets or sets the ID of the user.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the name of the role.
    /// </summary>
    public string RoleName { get; set; }

    /// <summary>
    /// Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
    public override bool Equals(object obj)
    {
        if (obj is null)
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        return obj is EntityPermissionInfo other && this.Equals(other);
    }

    /// <summary>
    /// Determines whether the specified EntityPermissionInfo is equal to the current object.
    /// </summary>
    /// <param name="other">The EntityPermissionInfo to compare with the current object.</param>
    /// <returns>true if the specified EntityPermissionInfo is equal to the current object; otherwise, false.</returns>
    public bool Equals(EntityPermissionInfo other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return this.StringEquals(this.Permission, other.Permission)
               && this.StringEquals(this.Source, other.Source)
               && this.StringEquals(this.EntityType, other.EntityType)
               && this.StringEquals(this.EntityId, other.EntityId)
               && this.StringEquals(this.UserId, other.UserId)
               && this.StringEquals(this.RoleName, other.RoleName);
    }

    /// <summary>
    /// Returns a hash code for this instance.
    /// </summary>
    /// <returns>A hash code for the current object.</returns>
    public override int GetHashCode()
    {
        // XOR the hash codes of all fields
        return this.GetStringHashCode(this.Permission)
               ^ this.GetStringHashCode(this.Source)
               ^ this.GetStringHashCode(this.EntityType)
               ^ this.GetStringHashCode(this.EntityId)
               ^ this.GetStringHashCode(this.UserId)
               ^ this.GetStringHashCode(this.RoleName);
    }

    /// <summary>
    /// Compares two EntityPermissionInfo objects for equality.
    /// </summary>
    /// <param name="left">The first EntityPermissionInfo to compare.</param>
    /// <param name="right">The second EntityPermissionInfo to compare.</param>
    /// <returns>true if left and right are equal; otherwise, false.</returns>
    public static bool operator ==(EntityPermissionInfo left, EntityPermissionInfo right)
    {
        if (left is null)
        {
            return right is null;
        }

        return left.Equals(right);
    }

    /// <summary>
    /// Compares two EntityPermissionInfo objects for inequality.
    /// </summary>
    /// <param name="left">The first EntityPermissionInfo to compare.</param>
    /// <param name="right">The second EntityPermissionInfo to compare.</param>
    /// <returns>true if left and right are not equal; otherwise, false.</returns>
    public static bool operator !=(EntityPermissionInfo left, EntityPermissionInfo right)
    {
        return !(left == right);
    }

    /// <summary>
    /// Helper method for comparing strings with null handling.
    /// </summary>
    /// <param name="left">First string to compare.</param>
    /// <param name="right">Second string to compare.</param>
    /// <returns>true if both strings are equal or both null; otherwise, false.</returns>
    private bool StringEquals(string left, string right)
    {
        if (left is null && right is null)
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return string.Equals(left, right, StringComparison.Ordinal);
    }

    /// <summary>
    /// Helper method for getting string hash code with null handling.
    /// </summary>
    /// <param name="value">The string to get hash code for.</param>
    /// <returns>The hash code of the string or 0 if null.</returns>
    private int GetStringHashCode(string value)
    {
        return value?.GetHashCode(StringComparison.Ordinal) ?? 0;
    }

    public override string ToString()
    {
        var entityAndPermission = $"{this.EntityType} [{this.Permission}]";

        if (!string.IsNullOrEmpty(this.UserId) && string.IsNullOrEmpty(this.RoleName))
        {
            return $"{entityAndPermission} User={this.UserId}";
        }

        if (!string.IsNullOrEmpty(this.RoleName) && string.IsNullOrEmpty(this.UserId))
        {
            return $"{entityAndPermission} Role={this.RoleName}";
        }

        return $"{entityAndPermission} user={this.UserId}, role={this.RoleName}";
    }
}
