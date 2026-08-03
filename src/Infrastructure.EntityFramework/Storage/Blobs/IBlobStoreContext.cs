// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

/// <summary>
/// Defines the Entity Framework capability contract required by the database-backed blob store provider.
/// </summary>
/// <remarks>
/// A host <see cref="DbContext" /> opts into Entity Framework blob storage by implementing this interface
/// and exposing the required blob metadata and blob chunk sets.
/// </remarks>
/// <example>
/// <code>
/// public class AppDbContext : DbContext, IBlobStoreContext
/// {
///     public DbSet&lt;StorageBlob&gt; StorageBlobs { get; set; }
///
///     public DbSet&lt;StorageBlobChunk&gt; StorageBlobChunks { get; set; }
/// }
/// </code>
/// </example>
public interface IBlobStoreContext
{
    /// <summary>
    /// Gets or sets the persisted blob metadata rows.
    /// </summary>
    /// <example>
    /// <code>
    /// var blobs = context.StorageBlobs;
    /// </code>
    /// </example>
    DbSet<StorageBlob> StorageBlobs { get; set; }

    /// <summary>
    /// Gets or sets the persisted blob content chunk rows.
    /// </summary>
    /// <example>
    /// <code>
    /// var chunks = context.StorageBlobChunks;
    /// </code>
    /// </example>
    DbSet<StorageBlobChunk> StorageBlobChunks { get; set; }
}
