// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Broadcasting;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

/// <summary>Represents one process registration in a shared Broadcasting registry.</summary>
/// <example><code>public DbSet&lt;BroadcastNodeRegistrationEntity&gt; BroadcastNodeRegistrations { get; set; }</code></example>
[Table("__Broadcasting_NodeRegistrations")]
[Index(nameof(NormalizedNodeIdentity), IsUnique = true)]
[Index(nameof(IsActive), nameof(LeaseExpiresUtc))]
[Index(nameof(IsActive), nameof(ConsecutiveFailureCount))]
public sealed class BroadcastNodeRegistrationEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>Gets or sets the display node identity.</summary>
    [Required]
    [MaxLength(256)]
    public string NodeIdentity { get; set; }

    /// <summary>Gets or sets the normalized identity key.</summary>
    [Required]
    [MaxLength(256)]
    public string NormalizedNodeIdentity { get; set; }

    /// <summary>Gets or sets the direct receiver address.</summary>
    [MaxLength(2048)]
    public string AdvertisedAddress { get; set; }

    /// <summary>Gets or sets the process start timestamp.</summary>
    [Required]
    public DateTimeOffset ProcessStartedUtc { get; set; }

    /// <summary>Gets or sets the latest registration timestamp.</summary>
    [Required]
    public DateTimeOffset RegisteredUtc { get; set; }

    /// <summary>Gets or sets the protocol version.</summary>
    [Required]
    [MaxLength(32)]
    public string ProtocolVersion { get; set; }

    /// <summary>Gets or sets whether the node participates in target snapshots.</summary>
    [Required]
    public bool IsActive { get; set; }

    /// <summary>Gets or sets the latest successful delivery timestamp.</summary>
    public DateTimeOffset? LastSuccessUtc { get; set; }

    /// <summary>Gets or sets the latest failed delivery timestamp.</summary>
    public DateTimeOffset? LastFailureUtc { get; set; }

    /// <summary>Gets or sets a safe latest failure summary.</summary>
    [MaxLength(4000)]
    public string LastFailure { get; set; }

    /// <summary>Gets or sets the consecutive failed-delivery count.</summary>
    [Required]
    public int ConsecutiveFailureCount { get; set; }

    /// <summary>Gets or sets the optional last lease renewal timestamp.</summary>
    public DateTimeOffset? LeaseRenewedUtc { get; set; }

    /// <summary>Gets or sets the optional lease expiration timestamp.</summary>
    public DateTimeOffset? LeaseExpiresUtc { get; set; }

    /// <summary>Gets or sets the optimistic concurrency token.</summary>
    [Required]
    [ConcurrencyCheck]
    public Guid ConcurrencyVersion { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets normalized scope associations.</summary>
    public ICollection<BroadcastNodeScopeEntity> Scopes { get; set; } = [];

    /// <summary>Advances the optimistic concurrency token.</summary>
    public void AdvanceConcurrencyVersion() => this.ConcurrencyVersion = Guid.NewGuid();
}
