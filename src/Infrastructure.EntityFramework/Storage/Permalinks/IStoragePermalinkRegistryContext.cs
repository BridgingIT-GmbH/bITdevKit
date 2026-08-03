// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Storage;

using BridgingIT.DevKit.Infrastructure.EntityFramework;

/// <summary>
/// Defines the Entity Framework set required by the Storage Permalink Registry.
/// </summary>
/// <example>
/// <code>
/// public sealed class AppDbContext : DbContext, IStoragePermalinkRegistryContext { public DbSet&lt;StoragePermalink&gt; StoragePermalinks { get; set; } }
/// </code>
/// </example>
public interface IStoragePermalinkRegistryContext
{
    /// <summary>
    /// Gets or sets persisted permalink registry entries.
    /// </summary>
    /// <example>
    /// <code>
    /// var query = context.StoragePermalinks.AsNoTracking();
    /// </code>
    /// </example>
    DbSet<StoragePermalink> StoragePermalinks { get; set; }
}
