// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

/// <summary>
/// Represents entity permission.
/// </summary>
[Table("__Identity_EntityPermissions")]
[Index(nameof(UserId))]
[Index(nameof(RoleName))]
[Index(nameof(EntityType))]
[Index(nameof(EntityId))]
[Index(nameof(Permission))]
[Index(nameof(IsRevoked))]
[Index(nameof(CreatedDate))]
public class EntityPermission
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the user id.
    /// </summary>
    [MaxLength(128)]
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the role name.
    /// </summary>
    [MaxLength(128)]
    public string RoleName { get; set; }

    /// <summary>
    /// Gets or sets the entity type.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string EntityType { get; set; }

    /// <summary>
    /// Gets or sets the entity id.
    /// </summary>
    [MaxLength(256)]
    public string EntityId { get; set; }  // Null or empty means wildcard (all entities of type)

    /// <summary>
    /// Gets or sets the permission.
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string Permission { get; set; }

    /// <summary>
    /// Gets or sets the is revoked.
    /// </summary>
    [Required]
    public bool IsRevoked { get; set; } // not used yet, revoked permissions are currently deleted from the table. maybe needed to revoke permissions in hierarchical tree where permissions are inherited (to break the inheritance at a deeper level)

    /// <summary>
    /// Gets or sets the module.
    /// </summary>
    [MaxLength(128)]
    public string Module { get; set; }

    /// <summary>
    /// Gets or sets the created date.
    /// </summary>
    [Required]
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the updated date.
    /// </summary>
    public DateTimeOffset UpdatedDate { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the properties.
    /// </summary>
    [NotMapped]
    public IDictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Stores the properties json.
    /// </summary>
    [Column("Properties")]
    public string PropertiesJson
    {
        get =>
            this.Properties.IsNullOrEmpty()
                ? null
                : JsonSerializer.Serialize(this.Properties, DefaultJsonSerializerOptions.Create());
        set =>
            this.Properties = value.IsNullOrEmpty()
                ? []
                : JsonSerializer.Deserialize<Dictionary<string, object>>(value,
                    DefaultJsonSerializerOptions.Create());
    }
}
