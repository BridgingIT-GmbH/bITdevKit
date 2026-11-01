// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Auditing;

using Microsoft.EntityFrameworkCore;

/// <summary>Exposes audit-owned sets directly without repository, Active Entity or ChangeHistory requirements.</summary>
/// <example><code>public sealed class Store(DbContextOptions&lt;Store&gt; options) : DbContext(options), IAuditingDbContext { /* expose the three audit sets and singleton control set */ }</code></example>
public interface IAuditingDbContext
{
    /// <summary>Finalized execution headers and owned graphs.</summary>
    /// <example><code>var records = context.AuditRecords;</code></example>
    DbSet<AuditRecordEntity> AuditRecords { get; set; }

    /// <summary>Retained execution action occurrences.</summary>
    /// <example><code>var actions = context.AuditActions;</code></example>
    DbSet<AuditActionEntity> AuditActions { get; set; }

    /// <summary>Optional retained source evidence groups.</summary>
    /// <example><code>var changes = context.AuditEntityChanges;</code></example>
    DbSet<AuditEntityChangeEntity> AuditEntityChanges { get; set; }

    /// <summary>Durable singleton store identity and publication/retention control state.</summary>
    /// <example><code>var state = context.AuditStoreState;</code></example>
    DbSet<AuditStoreStateEntity> AuditStoreState { get; set; }
}
