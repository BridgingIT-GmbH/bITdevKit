// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

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
    [Key]
    public Guid Id { get; set; }

    [MaxLength(128)]
    public string UserId { get; set; }

    [MaxLength(128)]
    public string RoleName { get; set; }

    [Required]
    [MaxLength(256)]
    public string EntityType { get; set; }

    [MaxLength(256)]
    public string EntityId { get; set; }  // Null or empty means wildcard (all entities of type)

    [Required]
    [MaxLength(64)]
    public string Permission { get; set; }

    [Required]
    public bool IsRevoked { get; set; } // not used yet, revoked permissions are currently deleted from the table. maybe needed to revoke permissions in hierarchical tree where permissions are inherited (to break the inheritance at a deeper level)

    [MaxLength(128)]
    public string Module { get; set; }

    [Required]
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedDate { get; set; } = DateTimeOffset.UtcNow;

    [NotMapped]
    public IDictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

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