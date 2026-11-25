// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

/// <summary>Provides the EF mappings for blob owner, chunk, and dimension row IDs.</summary>
/// <remarks>EF applies these mappings automatically when it discovers the corresponding entities.</remarks>
/// <example><code>modelBuilder.ApplyConfiguration(new BlobRowIdConfiguration.Owner());</code></example>
public static class BlobRowIdConfiguration
{
    private static readonly ValueConverter<Guid, string> Converter = new(
        id => id.ToString("N"),
        value => Guid.ParseExact(value, "N"));

    private static PropertyBuilder<Guid> ConfigureProperty(PropertyBuilder<Guid> property)
        => property.HasConversion(Converter)
            .HasColumnType("char(32)")
            .HasMaxLength(32)
            .IsUnicode(false)
            .IsFixedLength();

    /// <summary>Maps the blob primary key to fixed-width UUID text and preserves application-generated IDs.</summary>
    /// <example><code>modelBuilder.ApplyConfiguration(new BlobRowIdConfiguration.Owner());</code></example>
    public sealed class Owner : IEntityTypeConfiguration<StorageBlob>
    {
        /// <summary>Stores the blob ID as lowercase UUID text in a <c>char(32)</c> column.</summary>
        /// <param name="builder">The builder for the blob owner entity.</param>
        /// <example><code>new BlobRowIdConfiguration.Owner().Configure(modelBuilder.Entity&lt;StorageBlob&gt;());</code></example>
        public void Configure(EntityTypeBuilder<StorageBlob> builder)
            => ConfigureProperty(builder.Property(row => row.Id)).ValueGeneratedNever();
    }

    /// <summary>Maps the chunk's blob foreign key to the same fixed-width UUID text as the owner key.</summary>
    /// <example><code>modelBuilder.ApplyConfiguration(new BlobRowIdConfiguration.Chunk());</code></example>
    public sealed class Chunk : IEntityTypeConfiguration<StorageBlobChunk>
    {
        /// <summary>Stores the chunk's blob ID as lowercase UUID text in a <c>char(32)</c> column.</summary>
        /// <param name="builder">The builder for the blob chunk entity.</param>
        /// <example><code>new BlobRowIdConfiguration.Chunk().Configure(modelBuilder.Entity&lt;StorageBlobChunk&gt;());</code></example>
        public void Configure(EntityTypeBuilder<StorageBlobChunk> builder)
            => ConfigureProperty(builder.Property(row => row.BlobId));
    }

    /// <summary>Maps the dimension's blob foreign key to the same fixed-width UUID text as the owner key.</summary>
    /// <example><code>modelBuilder.ApplyConfiguration(new BlobRowIdConfiguration.Dimension());</code></example>
    public sealed class Dimension : IEntityTypeConfiguration<StorageBlobDimension>
    {
        /// <summary>Stores the dimension's blob ID as lowercase UUID text in a <c>char(32)</c> column.</summary>
        /// <param name="builder">The builder for the blob dimension entity.</param>
        /// <example><code>new BlobRowIdConfiguration.Dimension().Configure(modelBuilder.Entity&lt;StorageBlobDimension&gt;());</code></example>
        public void Configure(EntityTypeBuilder<StorageBlobDimension> builder)
            => ConfigureProperty(builder.Property(row => row.BlobId));
    }
}
