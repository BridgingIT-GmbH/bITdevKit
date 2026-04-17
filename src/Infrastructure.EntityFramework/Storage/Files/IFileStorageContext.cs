// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

/// <summary>
/// Defines the Entity Framework capability contract required by the database-backed file storage provider.
/// </summary>
/// <remarks>
/// A host <see cref="DbContext" /> opts into Entity Framework file storage by implementing this interface
/// and exposing the required file, file-content, and directory sets.
/// </remarks>
/// <example>
/// <code>
/// public class AppDbContext : DbContext, IFileStorageContext
/// {
///     public DbSet&lt;FileStorageFileEntity&gt; StorageFiles { get; set; }
///
///     public DbSet&lt;FileStorageFileContentEntity&gt; StorageFileContents { get; set; }
///
///     public DbSet&lt;FileStorageDirectoryEntity&gt; StorageDirectories { get; set; }
/// }
/// </code>
/// </example>
public interface IFileStorageContext
{
    /// <summary>
    /// Gets or sets the persisted file metadata rows for the virtual filesystem.
    /// </summary>
    DbSet<FileStorageFileEntity> StorageFiles { get; set; }

    /// <summary>
    /// Gets or sets the persisted file payload rows for the virtual filesystem.
    /// </summary>
    DbSet<FileStorageFileContentEntity> StorageFileContents { get; set; }

    /// <summary>
    /// Gets or sets the persisted directory rows for the virtual filesystem.
    /// </summary>
    DbSet<FileStorageDirectoryEntity> StorageDirectories { get; set; }
}
