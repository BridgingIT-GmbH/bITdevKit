// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// Exposes the EF Core set used to persist ChangeHistory rows.
/// </summary>
/// <example>
/// <code>
/// public sealed class AppDbContext : DbContext, IChangeHistoryContext
/// {
///     public DbSet&lt;ChangeHistoryEntry&gt; ChangeHistory { get; set; }
/// }
/// </code>
/// </example>
public interface IChangeHistoryContext
{
    /// <summary>
    /// Gets or sets the persisted ChangeHistory rows.
    /// </summary>
    DbSet<ChangeHistoryEntry> ChangeHistory { get; set; }
}
