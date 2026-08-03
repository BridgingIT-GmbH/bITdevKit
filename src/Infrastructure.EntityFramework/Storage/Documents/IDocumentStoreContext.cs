// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

/// <summary>
/// Defines the Entity Framework context surface required by the Document Storage provider.
/// </summary>
/// <remarks>
/// Application DbContext types implement this interface and expose the annotated <see cref="StorageDocument" /> entity set.
/// The provider creates and owns a dependency-injection scope and context for every operation; callers do not pass context
/// instances into the provider.
/// </remarks>
/// <example>
/// <code>
/// public sealed class AppDbContext(DbContextOptions&lt;AppDbContext&gt; options)
///     : DbContext(options), IDocumentStoreContext
/// {
///     public DbSet&lt;StorageDocument&gt; StorageDocuments { get; set; }
/// }
/// </code>
/// </example>
public interface IDocumentStoreContext
{
    /// <summary>Gets or sets the annotated entity set used to persist serialized documents and metadata.</summary>
    /// <example><code>var query = context.StorageDocuments.AsNoTracking();</code></example>
    DbSet<StorageDocument> StorageDocuments { get; set; }
}
