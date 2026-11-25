// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>Maps the document primary key and the matching Dimension foreign key.</summary>
/// <example><code>modelBuilder.ApplyConfiguration(new DocumentRowIdConfiguration.Owner());</code></example>
public static class DocumentRowIdConfiguration
{
    /// <summary>Maps the application-assigned document ID to fixed-width UUID text.</summary>
    /// <example><code>modelBuilder.ApplyConfiguration(new DocumentRowIdConfiguration.Owner());</code></example>
    public sealed class Owner : IEntityTypeConfiguration<StorageDocument>
    {
        /// <summary>Stores the owner key as lowercase UUID text and keeps the assigned ID on insert.</summary>
        /// <param name="builder">The document entity builder.</param>
        /// <example><code>new DocumentRowIdConfiguration.Owner().Configure(modelBuilder.Entity&lt;StorageDocument&gt;());</code></example>
        public void Configure(EntityTypeBuilder<StorageDocument> builder)
            => StorageRowIdConfiguration.Configure(builder.Property(row => row.Id)).ValueGeneratedNever();
    }

    /// <summary>Maps the Dimension foreign key to the same representation as the owner key.</summary>
    /// <example><code>modelBuilder.ApplyConfiguration(new DocumentRowIdConfiguration.Dimension());</code></example>
    public sealed class Dimension : IEntityTypeConfiguration<StorageDocumentDimension>
    {
        /// <summary>Stores the Dimension's document ID as fixed-width lowercase UUID text.</summary>
        /// <param name="builder">The Dimension entity builder.</param>
        /// <example><code>new DocumentRowIdConfiguration.Dimension().Configure(modelBuilder.Entity&lt;StorageDocumentDimension&gt;());</code></example>
        public void Configure(EntityTypeBuilder<StorageDocumentDimension> builder)
            => StorageRowIdConfiguration.Configure(builder.Property(row => row.DocumentId));
    }
}
