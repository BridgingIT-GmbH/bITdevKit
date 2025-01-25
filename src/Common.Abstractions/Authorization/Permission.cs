// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Defines a permission constant structure to prevent magic strings.
/// </summary>
public readonly struct Permission : IEquatable<Permission>
{
    private readonly string value;

    private Permission(string value)
    {
        this.value = value;
    }

    public static Permission For(string permission)
    {
        return new Permission(permission);
    }

    /// <summary>
    /// Gets the read permission value.
    /// </summary>
    public static Permission Read => new("Read");

    /// <summary>
    /// Gets the write permission value.
    /// </summary>
    public static Permission Write => new("Write");

    /// <summary>
    /// Gets the create permission value.
    /// </summary>
    public static Permission Create => new("Create");

    /// <summary>
    /// Gets the update permission value.
    /// </summary>
    public static Permission Update => new("Update");

    /// <summary>
    /// Gets the delete permission value.
    /// </summary>
    public static Permission Delete => new("Delete");

    /// <summary>
    /// Gets the list/view all permission value. Differs from Read as it allows seeing all entries.
    /// </summary>
    public static Permission List => new("List");

    /// <summary>
    /// Gets the export permission value. Allows exporting data to files.
    /// </summary>
    public static Permission Export => new("Export");

    /// <summary>
    /// Gets the import permission value. Allows importing data from files.
    /// </summary>
    public static Permission Import => new("Import");

    /// <summary>
    /// Gets the archive permission value. Different from delete, moves to archive.
    /// </summary>
    public static Permission Archive => new("Archive");

    /// <summary>
    /// Gets the restore permission value. Can restore archived items.
    /// </summary>
    public static Permission Restore => new("Restore");

    /// <summary>
    /// Gets the approve permission value. Can approve changes/entries.
    /// </summary>
    public static Permission Approve => new("Approve");

    /// <summary>
    /// Gets the assign permission value. Can assign items to other users.
    /// </summary>
    public static Permission Assign => new("Assign");

    /// <summary>
    /// Gets the share permission value. Can share items with other users/groups.
    /// </summary>
    public static Permission Share => new("Share");

    /// <summary>
    /// Gets the manage permission value. Can manage permissions for this entity.
    /// </summary>
    public static Permission Manage => new("Manage");

    /// <summary>
    /// Gets the print permission value. Can generate printable versions.
    /// </summary>
    public static Permission Print => new("Print");

    public bool Equals(Permission other)
    {
        return this.value == other.value;
    }

    /// <summary>
    /// Gets the string value of the permission.
    /// </summary>
    public override string ToString() => this.value;

    /// <summary>
    /// Implicitly converts the permission to its string representation.
    /// </summary>
    public static implicit operator string(Permission permission) => permission.value;
}
