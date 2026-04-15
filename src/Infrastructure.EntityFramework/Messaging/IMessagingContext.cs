// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Messaging;

/// <summary>
/// Defines the Entity Framework capability contract required by the SQL-backed message broker.
/// </summary>
/// <remarks>
/// A host <see cref="DbContext" /> opts into the broker by implementing this interface and exposing
/// the broker message set alongside any other feature-specific sets it already supports.
/// </remarks>
/// <example>
/// <code>
/// public class AppDbContext : DbContext, IMessagingContext
/// {
///     public DbSet&lt;BrokerMessage&gt; BrokerMessages { get; set; }
/// }
/// </code>
/// </example>
public interface IMessagingContext
{
    /// <summary>
    /// Gets or sets the durable broker messages persisted for transport processing.
    /// </summary>
    DbSet<BrokerMessage> BrokerMessages { get; set; }
}