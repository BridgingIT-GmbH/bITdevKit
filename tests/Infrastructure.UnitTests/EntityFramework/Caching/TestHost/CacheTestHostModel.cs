// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Tests.Caching;

using System.Text;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

/// <summary>Freezes the annotated format-one model independently of runtime mapping and native column types.</summary>
/// <remarks>Canonical token converters preserve provider-neutral binary comparisons on all supported engines.</remarks>
/// <example><code>CacheTestHostModel.Configure(modelBuilder);</code></example>
public static class CacheTestHostModel
{
    private static readonly ValueConverter<string, byte[]> CanonicalBytes = new(value => Encoding.ASCII.GetBytes(value), value => Encoding.ASCII.GetString(value));

    /// <summary>Builds the frozen migration target without relying on model conventions or runtime annotations.</summary>
    /// <param name="modelBuilder">The migration or snapshot model builder.</param>
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.14");

        modelBuilder.Entity<CacheChunkEntity>(entity =>
        {
            entity.ToTable("__Caching_Chunks");
            entity.Property(e => e.PayloadId).HasMaxLength(32).IsUnicode(false).HasConversion(CanonicalBytes).IsRequired(true).ValueGeneratedNever();
            entity.Property(e => e.Ordinal).IsRequired(true).ValueGeneratedNever();
            entity.Property(e => e.Offset).IsRequired(true);
            entity.Property(e => e.Data).HasMaxLength(1048576).IsRequired(true);
            entity.Property(e => e.Hash).HasMaxLength(64).IsUnicode(false).HasConversion(CanonicalBytes).IsRequired(true);
            entity.HasKey("PayloadId", "Ordinal");
            entity.HasIndex("PayloadId", "Offset").HasDatabaseName("IX___Caching_Chunks_PayloadId_Offset");
        });

        modelBuilder.Entity<CacheCollectionEntity>(entity =>
        {
            entity.ToTable("__Caching_Collections");
            entity.Property(e => e.Id).HasMaxLength(64).IsUnicode(false).HasConversion(CanonicalBytes).IsRequired(true).ValueGeneratedNever();
            entity.Property(e => e.CanonicalIdentity).HasMaxLength(1024).IsUnicode(true).IsRequired(true);
            entity.Property(e => e.MaximumBytes).IsRequired(true);
            entity.Property(e => e.MaximumEntries).IsRequired(true);
            entity.Property(e => e.Epoch).IsRequired(true);
            entity.Property(e => e.Revision).IsRequired(true);
            entity.Property(e => e.CurrentBytes).IsRequired(true);
            entity.Property(e => e.RetiredBytes).IsRequired(true);
            entity.Property(e => e.StagingBytes).IsRequired(true);
            entity.Property(e => e.MetadataBytes).IsRequired(true);
            entity.Property(e => e.CurrentEntries).IsRequired(true);
            entity.HasKey("Id");
        });

        modelBuilder.Entity<CacheContractEntity>(entity =>
        {
            entity.ToTable("__Caching_Contracts");
            entity.Property(e => e.Id).HasMaxLength(64).IsUnicode(false).HasConversion(CanonicalBytes).IsRequired(true).ValueGeneratedNever();
            entity.Property(e => e.CollectionId).HasMaxLength(64).IsUnicode(false).HasConversion(CanonicalBytes).IsRequired(true);
            entity.Property(e => e.ContractVersion).HasMaxLength(128).IsUnicode(true).IsRequired(true);
            entity.Property(e => e.ContractAlias).HasMaxLength(128).IsUnicode(true).IsRequired(true);
            entity.Property(e => e.SchemaJson).IsRequired(true);
            entity.Property(e => e.PolicyJson).IsRequired(true);
            entity.Property(e => e.MetadataFormat).IsRequired(true);
            entity.HasKey("Id");
            entity.HasIndex("CollectionId", "ContractVersion").HasDatabaseName("IX___Caching_Contracts_CollectionId_ContractVersion");
        });

        modelBuilder.Entity<CacheDimensionEntity>(entity =>
        {
            entity.ToTable("__Caching_Dimensions");
            entity.Property(e => e.EntryId).HasMaxLength(64).IsUnicode(false).HasConversion(CanonicalBytes).IsRequired(true).ValueGeneratedNever();
            entity.Property(e => e.CollectionId).HasMaxLength(64).IsUnicode(false).HasConversion(CanonicalBytes).IsRequired(true);
            entity.Property(e => e.NameHash).HasMaxLength(64).IsUnicode(false).HasConversion(CanonicalBytes).IsRequired(true).ValueGeneratedNever();
            entity.Property(e => e.Ordinal).IsRequired(true).ValueGeneratedNever();
            entity.Property(e => e.ValueHash).HasMaxLength(64).IsUnicode(false).HasConversion(CanonicalBytes).IsRequired(false);
            entity.Property(e => e.Kind).IsRequired(true);
            entity.Property(e => e.Form).IsRequired(true);
            entity.Property(e => e.IsNull).IsRequired(true);
            entity.Property(e => e.NumericValue).IsRequired(false);
            entity.Property(e => e.IntervalEnd).IsRequired(false);
            entity.HasKey("EntryId", "NameHash", "Ordinal");
            entity.HasIndex("CollectionId", "NameHash", "Kind", "Form", "ValueHash", "EntryId").HasDatabaseName("IX___Caching_Dimensions_CollectionId_NameHash_Kind_0e56c8669181");
            entity.HasIndex("CollectionId", "NameHash", "Kind", "NumericValue", "IntervalEnd", "EntryId").HasDatabaseName("IX___Caching_Dimensions_CollectionId_NameHash_Kind_8a342904a7e2");
        });

        modelBuilder.Entity<CacheEntryEntity>(entity =>
        {
            entity.ToTable("__Caching_Entries");
            entity.Property(e => e.Id).HasMaxLength(64).IsUnicode(false).HasConversion(CanonicalBytes).IsRequired(true).ValueGeneratedNever();
            entity.Property(e => e.CollectionId).HasMaxLength(64).IsUnicode(false).HasConversion(CanonicalBytes).IsRequired(true);
            entity.Property(e => e.ContractId).HasMaxLength(64).IsUnicode(false).HasConversion(CanonicalBytes).IsRequired(true);
            entity.Property(e => e.KeyId).HasMaxLength(64).IsUnicode(false).HasConversion(CanonicalBytes).IsRequired(true);
            entity.Property(e => e.PayloadId).HasMaxLength(32).IsUnicode(false).HasConversion(CanonicalBytes).IsRequired(false);
            entity.Property(e => e.KeyPrefix).HasMaxLength(512).IsUnicode(false).HasConversion(CanonicalBytes).IsRequired(true);
            entity.Property(e => e.MetadataJson).IsRequired(true);
            entity.Property(e => e.PublishedTicks).IsRequired(true);
            entity.Property(e => e.PublicationSequence).IsRequired(true);
            entity.Property(e => e.ExpiryTicks).IsRequired(true);
            entity.Property(e => e.RetentionGraceTicks).IsRequired(true);
            entity.Property(e => e.PurgeAfterTicks).IsRequired(true);
            entity.Property(e => e.AbsoluteExpiryTicks).IsRequired(true);
            entity.Property(e => e.LastAccessTicks).IsRequired(true);
            entity.Property(e => e.RefreshDueTicks).IsRequired(false);
            entity.Property(e => e.RefreshBackoffTicks).IsRequired(false);
            entity.Property(e => e.HasForegroundAccess).IsRequired(true);
            entity.Property(e => e.StoredBytes).IsRequired(true);
            entity.Property(e => e.MetadataBytes).IsRequired(true);
            entity.Property(e => e.Priority).IsRequired(true);
            entity.Property(e => e.IsNotFound).IsRequired(true);
            entity.HasKey("Id");
            entity.HasIndex("PayloadId").HasDatabaseName("IX___Caching_Entries_PayloadId");
            entity.HasIndex("CollectionId", "PublicationSequence", "Id").HasDatabaseName("IX___Caching_Entries_CollectionId_PublicationSequence_Id");
            entity.HasIndex("PurgeAfterTicks", "Id").HasDatabaseName("IX___Caching_Entries_PurgeAfterTicks_Id");
            entity.HasIndex("CollectionId", "Priority", "LastAccessTicks", "Id").HasDatabaseName("IX___Caching_Entries_CollectionId_Priority_LastAccessTicks_Id");
            entity.HasIndex("CollectionId", "KeyId").HasDatabaseName("IX___Caching_Entries_CollectionId_KeyId");
            entity.HasIndex("CollectionId", "ContractId", "KeyPrefix", "Id").HasDatabaseName("IX___Caching_Entries_CollectionId_ContractId_KeyPrefix_Id");
            entity.HasIndex("CollectionId", "PublishedTicks", "Id").HasDatabaseName("IX___Caching_Entries_CollectionId_PublishedTicks_Id");
            entity.HasIndex("CollectionId", "ExpiryTicks", "Id").HasDatabaseName("IX___Caching_Entries_CollectionId_ExpiryTicks_Id");
            entity.HasIndex("CollectionId", "StoredBytes", "Id").HasDatabaseName("IX___Caching_Entries_CollectionId_StoredBytes_Id");
            entity.HasIndex("CollectionId", "HasForegroundAccess", "RefreshDueTicks", "Id").HasDatabaseName("IX___Caching_Entries_CollectionId_HasForegroundAcc_927582618536");
            entity.HasIndex("CollectionId", "ContractId", "HasForegroundAccess", "AbsoluteExpiryTicks", "Id").HasDatabaseName("IX___Caching_Entries_CollectionId_ContractId_HasFo_7d9c3f4ac379");
        });

        modelBuilder.Entity<CacheFenceEntity>(entity =>
        {
            entity.ToTable("__Caching_Fences");
            entity.Property(e => e.Id).HasMaxLength(32).IsUnicode(false).HasConversion(CanonicalBytes).IsRequired(true).ValueGeneratedNever();
            entity.Property(e => e.EntryId).HasMaxLength(64).IsUnicode(false).HasConversion(CanonicalBytes).IsRequired(true);
            entity.Property(e => e.KeyId).HasMaxLength(64).IsUnicode(false).HasConversion(CanonicalBytes).IsRequired(true);
            entity.Property(e => e.CollectionId).HasMaxLength(64).IsUnicode(false).HasConversion(CanonicalBytes).IsRequired(true);
            entity.Property(e => e.KeyEpoch).IsRequired(true);
            entity.Property(e => e.PublishedSinceCapture).IsRequired(true);
            entity.Property(e => e.CollectionEpoch).IsRequired(true);
            entity.Property(e => e.ObservedVersion).HasMaxLength(32).IsUnicode(false).HasConversion(CanonicalBytes).IsRequired(false);
            entity.Property(e => e.ObservedPublicationTicks).IsRequired(true);
            entity.Property(e => e.DeadlineTicks).IsRequired(true);
            entity.HasKey("Id");
            entity.HasIndex("EntryId").HasDatabaseName("IX___Caching_Fences_EntryId");
            entity.HasIndex("KeyId", "Id").HasDatabaseName("IX___Caching_Fences_KeyId_Id");
            entity.HasIndex("CollectionId", "DeadlineTicks", "Id").HasDatabaseName("IX___Caching_Fences_CollectionId_DeadlineTicks_Id");
        });

        modelBuilder.Entity<CacheKeyEntity>(entity =>
        {
            entity.ToTable("__Caching_Keys");
            entity.Property(e => e.Id).HasMaxLength(64).IsUnicode(false).HasConversion(CanonicalBytes).IsRequired(true).ValueGeneratedNever();
            entity.Property(e => e.CollectionId).HasMaxLength(64).IsUnicode(false).HasConversion(CanonicalBytes).IsRequired(true);
            entity.Property(e => e.CanonicalKey).HasMaxLength(4096).IsUnicode(true).IsRequired(true);
            entity.Property(e => e.Epoch).IsRequired(true);
            entity.HasKey("Id");
            entity.HasIndex("CollectionId", "Id").HasDatabaseName("IX___Caching_Keys_CollectionId_Id");
        });

        modelBuilder.Entity<CacheOperationEntity>(entity =>
        {
            entity.ToTable("__Caching_Operations");
            entity.Property(e => e.Id).HasMaxLength(32).IsUnicode(false).HasConversion(CanonicalBytes).IsRequired(true).ValueGeneratedNever();
            entity.Property(e => e.CollectionId).HasMaxLength(64).IsUnicode(false).HasConversion(CanonicalBytes).IsRequired(true);
            entity.Property(e => e.Revision).HasMaxLength(32).IsUnicode(false).HasConversion(CanonicalBytes).IsRequired(true);
            entity.Property(e => e.MetadataJson).IsRequired(true);
            entity.Property(e => e.State).IsRequired(true);
            entity.Property(e => e.CreatedTicks).IsRequired(true);
            entity.Property(e => e.UpdatedTicks).IsRequired(true);
            entity.Property(e => e.ClaimNode).HasMaxLength(128).IsUnicode(true).IsRequired(false);
            entity.Property(e => e.ClaimUntilTicks).IsRequired(false);
            entity.HasKey("Id");
            entity.HasIndex("CollectionId", "State", "UpdatedTicks", "Id").HasDatabaseName("IX___Caching_Operations_CollectionId_State_UpdatedTicks_Id");
            entity.HasIndex("State", "ClaimUntilTicks", "Id").HasDatabaseName("IX___Caching_Operations_State_ClaimUntilTicks_Id");
        });

        modelBuilder.Entity<CacheOwnershipEntity>(entity =>
        {
            entity.ToTable("__Caching_Ownerships");
            entity.Property(e => e.Id).HasMaxLength(32).IsUnicode(false).HasConversion(CanonicalBytes).IsRequired(true).ValueGeneratedNever();
            entity.Property(e => e.EntryId).HasMaxLength(64).IsUnicode(false).HasConversion(CanonicalBytes).IsRequired(true);
            entity.Property(e => e.KeyId).HasMaxLength(64).IsUnicode(false).HasConversion(CanonicalBytes).IsRequired(true);
            entity.Property(e => e.CollectionId).HasMaxLength(64).IsUnicode(false).HasConversion(CanonicalBytes).IsRequired(true);
            entity.Property(e => e.NodeId).HasMaxLength(128).IsUnicode(true).IsRequired(true);
            entity.Property(e => e.OwnerToken).HasMaxLength(32).IsUnicode(false).HasConversion(CanonicalBytes).IsRequired(true);
            entity.Property(e => e.FenceId).HasMaxLength(32).IsUnicode(false).HasConversion(CanonicalBytes).IsRequired(true);
            entity.Property(e => e.State).IsRequired(true);
            entity.Property(e => e.LeaseExpiryTicks).IsRequired(true);
            entity.Property(e => e.DeadlineTicks).IsRequired(true);
            entity.Property(e => e.PublishedVersion).HasMaxLength(32).IsUnicode(false).HasConversion(CanonicalBytes).IsRequired(false);
            entity.Property(e => e.FailureCode).IsRequired(false);
            entity.Property(e => e.IsNotFound).IsRequired(true);
            entity.Property(e => e.Independent).IsRequired(true);
            entity.Property(e => e.InterestsJson).HasMaxLength(262144).IsUnicode(false).IsRequired(true);
            entity.HasKey("Id");
            entity.HasIndex("EntryId", "Independent", "State", "LeaseExpiryTicks").HasDatabaseName("IX___Caching_Ownerships_EntryId_Independent_State__e0bbc98d7e0c");
            entity.HasIndex("CollectionId", "DeadlineTicks", "Id").HasDatabaseName("IX___Caching_Ownerships_CollectionId_DeadlineTicks_Id");
        });

        modelBuilder.Entity<CachePayloadEntity>(entity =>
        {
            entity.ToTable("__Caching_Payloads");
            entity.Property(e => e.Id).HasMaxLength(32).IsUnicode(false).HasConversion(CanonicalBytes).IsRequired(true).ValueGeneratedNever();
            entity.Property(e => e.EntryId).HasMaxLength(64).IsUnicode(false).HasConversion(CanonicalBytes).IsRequired(true);
            entity.Property(e => e.CollectionId).HasMaxLength(64).HasConversion(CanonicalBytes).IsRequired(true);
            entity.Property(e => e.MetadataBytes).IsRequired(true);
            entity.Property(e => e.OperationId).HasMaxLength(32).IsUnicode(false).HasConversion(CanonicalBytes).IsRequired(true);
            entity.Property(e => e.State).IsRequired(true);
            entity.Property(e => e.SizeBytes).IsRequired(true);
            entity.Property(e => e.ReservedBytes).IsRequired(true);
            entity.Property(e => e.CreatedTicks).IsRequired(true);
            entity.Property(e => e.DeadlineTicks).IsRequired(true);
            entity.Property(e => e.StagingOwner).HasMaxLength(32).IsUnicode(false).HasConversion(CanonicalBytes).IsRequired(true);
            entity.Property(e => e.InlineBytes).HasMaxLength(65536).IsRequired(false);
            entity.Property(e => e.StoredHash).HasMaxLength(64).IsUnicode(false).HasConversion(CanonicalBytes).IsRequired(false);
            entity.HasKey("Id");
            entity.HasIndex("EntryId", "State", "Id").HasDatabaseName("IX___Caching_Payloads_EntryId_State_Id");
            entity.HasIndex("OperationId").HasDatabaseName("IX___Caching_Payloads_OperationId").IsUnique();
            entity.HasIndex("State", "DeadlineTicks", "Id").HasDatabaseName("IX___Caching_Payloads_State_DeadlineTicks_Id");
        });

        modelBuilder.Entity<CacheReaderEntity>(entity =>
        {
            entity.ToTable("__Caching_Readers");
            entity.Property(e => e.Id).HasMaxLength(32).IsUnicode(false).HasConversion(CanonicalBytes).IsRequired(true).ValueGeneratedNever();
            entity.Property(e => e.PayloadId).HasMaxLength(32).IsUnicode(false).HasConversion(CanonicalBytes).IsRequired(true);
            entity.Property(e => e.ExpiryTicks).IsRequired(true);
            entity.HasKey("Id");
            entity.HasIndex("PayloadId", "ExpiryTicks").HasDatabaseName("IX___Caching_Readers_PayloadId_ExpiryTicks");
            entity.HasIndex("ExpiryTicks", "Id").HasDatabaseName("IX___Caching_Readers_ExpiryTicks_Id");
        });

        modelBuilder.Entity<CacheStoreEntity>(entity =>
        {
            entity.ToTable("__Caching_Stores");
            entity.Property(e => e.Id).HasMaxLength(64).IsUnicode(false).HasConversion(CanonicalBytes).IsRequired(true).ValueGeneratedNever();
            entity.Property(e => e.MetadataFormat).IsRequired(true);
            entity.Property(e => e.IdentityFormat).IsRequired(true);
            entity.Property(e => e.CursorKey).HasMaxLength(32).IsRequired(true);
            entity.Property(e => e.MaximumBytes).IsRequired(true);
            entity.Property(e => e.MaximumEntries).IsRequired(true);
            entity.Property(e => e.CurrentBytes).IsRequired(true);
            entity.Property(e => e.RetiredBytes).IsRequired(true);
            entity.Property(e => e.StagingBytes).IsRequired(true);
            entity.Property(e => e.MetadataBytes).IsRequired(true);
            entity.Property(e => e.CurrentEntries).IsRequired(true);
            entity.HasKey("Id");
        });

        modelBuilder.Entity<CacheChunkEntity>().HasOne(e => e.Payload).WithMany().HasForeignKey(e => e.PayloadId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CacheDimensionEntity>().HasOne(e => e.Entry).WithMany().HasForeignKey(e => e.EntryId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CacheEntryEntity>().HasOne(e => e.Payload).WithMany().HasForeignKey(e => e.PayloadId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CacheReaderEntity>().HasOne(e => e.Payload).WithMany().HasForeignKey(e => e.PayloadId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CacheStoreEntity>().HasData(new CacheStoreEntity
        {
            Id = "root", MetadataFormat = 1, IdentityFormat = 1, CursorKey = new byte[32]
        });
    }
}