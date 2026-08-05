// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Broadcasting;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

/// <summary>Associates one registered node with one normalized broadcast scope.</summary>
/// <example><code>public DbSet&lt;BroadcastNodeScopeEntity&gt; BroadcastNodeScopes { get; set; }</code></example>
[Table("__Broadcasting_NodeScopes")]
[PrimaryKey(nameof(NodeRegistrationId), nameof(NormalizedScope))]
[Index(nameof(NormalizedScope), nameof(NodeRegistrationId))]
public sealed class BroadcastNodeScopeEntity
{
    /// <summary>Gets or sets the parent node-registration identifier.</summary>
    public Guid NodeRegistrationId { get; set; }

    /// <summary>Gets or sets the normalized scope key.</summary>
    [Required]
    [MaxLength(256)]
    public string NormalizedScope { get; set; }

    /// <summary>Gets or sets the scope display value.</summary>
    [Required]
    [MaxLength(256)]
    public string Scope { get; set; }

    /// <summary>Gets or sets the parent registration.</summary>
    [Required]
    [ForeignKey(nameof(NodeRegistrationId))]
    public BroadcastNodeRegistrationEntity NodeRegistration { get; set; }
}
