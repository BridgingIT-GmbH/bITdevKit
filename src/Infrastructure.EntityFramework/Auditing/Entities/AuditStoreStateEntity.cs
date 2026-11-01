// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Auditing;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>Single durable technical control row retained after all captured records are removed.</summary>
/// <example><code>var entity = new AuditStoreStateEntity();</code></example>
[Table("__Auditing_StoreState")]
[EntityTypeConfiguration(typeof(AuditStoreStateEntity.Configuration))]
public sealed class AuditStoreStateEntity
{
    /// <summary>Constrained singleton key, always one.</summary>
    /// <example><code>var value = entity.Singleton;</code></example>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Singleton { get; set; } = 1;

    /// <summary>Stable configured store identity, seeded explicitly by host migration.</summary>
    /// <example><code>var value = entity.StoreId;</code></example>
    public Guid StoreId { get; set; }

    /// <summary>Current persistence-position interpretation epoch.</summary>
    /// <example><code>var value = entity.StoreEpoch;</code></example>
    public Guid StoreEpoch { get; set; }

    /// <summary>Highest whole-graph committed publication position.</summary>
    /// <example><code>var value = entity.PublishedPosition;</code></example>
    public long PublishedPosition { get; set; }

    /// <summary>Generation advanced by archive/removal membership transitions.</summary>
    /// <example><code>var value = entity.LifecycleGeneration;</code></example>
    public long LifecycleGeneration { get; set; }

    /// <summary>Conservative availability watermark for extraction gaps.</summary>
    /// <example><code>var value = entity.RemovedThroughPosition;</code></example>
    public long RemovedThroughPosition { get; set; }

    /// <summary>Durable removal/replay rejection watermark.</summary>
    /// <example><code>var value = entity.RejectCompletedBeforeUtcTicks;</code></example>
    public long RejectCompletedBeforeUtcTicks { get; set; }

    /// <summary>Durable archive watermark preventing delayed writes from returning to the active path.</summary>
    /// <example><code>var value = entity.ArchiveCompletedBeforeUtcTicks;</code></example>
    public long ArchiveCompletedBeforeUtcTicks { get; set; }

    /// <summary>Owns the singleton database constraint, applied automatically when the entity is included in a client model.</summary>
    /// <example><code>var configuration = new AuditStoreStateEntity.Configuration();</code></example>
    public sealed class Configuration : IEntityTypeConfiguration<AuditStoreStateEntity>
    {
        /// <summary>Restricts the durable control table to its single supported key.</summary>
        /// <example><code>configuration.Configure(entityTypeBuilder);</code></example>
        public void Configure(EntityTypeBuilder<AuditStoreStateEntity> builder)
        {
            builder.ToTable(table => table.HasCheckConstraint("CK___Auditing_StoreState_Singleton", "\"Singleton\" = 1"));
        }
    }
}
