// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Broadcasting;

using Microsoft.EntityFrameworkCore;

/// <summary>Defines the Entity Framework sets required by Broadcasting.</summary>
/// <example>
/// <code>
/// public sealed class AppDbContext : DbContext, IBroadcastingContext
/// {
///     public DbSet&lt;BroadcastNodeRegistrationEntity&gt; BroadcastNodeRegistrations { get; set; }
///     public DbSet&lt;BroadcastNodeScopeEntity&gt; BroadcastNodeScopes { get; set; }
/// }
/// </code>
/// </example>
public interface IBroadcastingContext
{
    /// <summary>Gets or sets node registration rows.</summary>
    DbSet<BroadcastNodeRegistrationEntity> BroadcastNodeRegistrations { get; set; }

    /// <summary>Gets or sets normalized node-to-scope rows.</summary>
    DbSet<BroadcastNodeScopeEntity> BroadcastNodeScopes { get; set; }
}
