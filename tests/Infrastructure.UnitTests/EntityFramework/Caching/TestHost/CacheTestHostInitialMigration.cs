// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Tests.Caching;

using System.Text;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

/// <summary>Deploys this consumer test host's annotated cache model before provider readiness; the runtime package ships no migrations.</summary>
/// <example><code>await context.Database.MigrateAsync(cancellationToken); // database bootstrap, never a cache request</code></example>
[DbContext(typeof(CacheTestHostDbContext))]
[Migration("202611100001_TestHostCacheInitial")]
public sealed class CacheTestHostInitialMigration : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("__Caching_Stores", columns: table => new
        {
            Id = table.Column<byte[]>(maxLength: 64, nullable: false),
            MetadataFormat = table.Column<int>(nullable: false),
            IdentityFormat = table.Column<int>(nullable: false),
            CursorKey = table.Column<byte[]>(maxLength: 32, nullable: false),
            MaximumBytes = table.Column<long>(nullable: false),
            MaximumEntries = table.Column<long>(nullable: false),
            CurrentBytes = table.Column<long>(nullable: false),
            RetiredBytes = table.Column<long>(nullable: false),
            StagingBytes = table.Column<long>(nullable: false),
            MetadataBytes = table.Column<long>(nullable: false),
            CurrentEntries = table.Column<long>(nullable: false)
        }, constraints: table => table.PrimaryKey("PK___Caching_Stores", x => x.Id));

        migrationBuilder.CreateTable("__Caching_Collections", columns: table => new
        {
            Id = table.Column<byte[]>(maxLength: 64, nullable: false),
            CanonicalIdentity = table.Column<string>(maxLength: 1024, unicode: true, nullable: false),
            MaximumBytes = table.Column<long>(nullable: false),
            MaximumEntries = table.Column<long>(nullable: false),
            Epoch = table.Column<long>(nullable: false),
            Revision = table.Column<long>(nullable: false),
            CurrentBytes = table.Column<long>(nullable: false),
            RetiredBytes = table.Column<long>(nullable: false),
            StagingBytes = table.Column<long>(nullable: false),
            MetadataBytes = table.Column<long>(nullable: false),
            CurrentEntries = table.Column<long>(nullable: false)
        }, constraints: table => table.PrimaryKey("PK___Caching_Collections", x => x.Id));

        migrationBuilder.CreateTable("__Caching_Contracts", columns: table => new
        {
            Id = table.Column<byte[]>(maxLength: 64, nullable: false),
            CollectionId = table.Column<byte[]>(maxLength: 64, nullable: false),
            ContractVersion = table.Column<string>(maxLength: 128, unicode: true, nullable: false),
            ContractAlias = table.Column<string>(maxLength: 128, unicode: true, nullable: false),
            SchemaJson = table.Column<string>(unicode: true, nullable: false),
            PolicyJson = table.Column<string>(unicode: true, nullable: false),
            MetadataFormat = table.Column<int>(nullable: false)
        }, constraints: table => table.PrimaryKey("PK___Caching_Contracts", x => x.Id));

        migrationBuilder.CreateTable("__Caching_Keys", columns: table => new
        {
            Id = table.Column<byte[]>(maxLength: 64, nullable: false),
            CollectionId = table.Column<byte[]>(maxLength: 64, nullable: false),
            CanonicalKey = table.Column<string>(maxLength: 4096, unicode: true, nullable: false),
            Epoch = table.Column<long>(nullable: false)
        }, constraints: table => table.PrimaryKey("PK___Caching_Keys", x => x.Id));

        migrationBuilder.CreateTable("__Caching_Entries", columns: table => new
        {
            Id = table.Column<byte[]>(maxLength: 64, nullable: false),
            CollectionId = table.Column<byte[]>(maxLength: 64, nullable: false),
            ContractId = table.Column<byte[]>(maxLength: 64, nullable: false),
            KeyId = table.Column<byte[]>(maxLength: 64, nullable: false),
            PayloadId = table.Column<byte[]>(maxLength: 32, nullable: true),
            KeyPrefix = table.Column<byte[]>(maxLength: 512, nullable: false),
            MetadataJson = table.Column<string>(unicode: true, nullable: false),
            PublishedTicks = table.Column<long>(nullable: false),
            PublicationSequence = table.Column<long>(nullable: false),
            ExpiryTicks = table.Column<long>(nullable: false),
            RetentionGraceTicks = table.Column<long>(nullable: false),
            PurgeAfterTicks = table.Column<long>(nullable: false),
            AbsoluteExpiryTicks = table.Column<long>(nullable: false),
            LastAccessTicks = table.Column<long>(nullable: false),
            RefreshDueTicks = table.Column<long>(nullable: true),
            RefreshBackoffTicks = table.Column<long>(nullable: true),
            HasForegroundAccess = table.Column<bool>(nullable: false),
            StoredBytes = table.Column<long>(nullable: false),
            MetadataBytes = table.Column<long>(nullable: false),
            Priority = table.Column<int>(nullable: false),
            IsNotFound = table.Column<bool>(nullable: false)
        }, constraints: table => table.PrimaryKey("PK___Caching_Entries", x => x.Id));

        migrationBuilder.CreateTable("__Caching_Payloads", columns: table => new
        {
            Id = table.Column<byte[]>(maxLength: 32, nullable: false),
            EntryId = table.Column<byte[]>(maxLength: 64, nullable: false),
            CollectionId = table.Column<byte[]>(maxLength: 64, nullable: false),
            MetadataBytes = table.Column<long>(nullable: false),
            OperationId = table.Column<byte[]>(maxLength: 32, nullable: false),
            State = table.Column<int>(nullable: false),
            SizeBytes = table.Column<long>(nullable: false),
            ReservedBytes = table.Column<long>(nullable: false),
            CreatedTicks = table.Column<long>(nullable: false),
            DeadlineTicks = table.Column<long>(nullable: false),
            StagingOwner = table.Column<byte[]>(maxLength: 32, nullable: false),
            InlineBytes = table.Column<byte[]>(maxLength: 65536, nullable: true),
            StoredHash = table.Column<byte[]>(maxLength: 64, nullable: true)
        }, constraints: table => table.PrimaryKey("PK___Caching_Payloads", x => x.Id));

        migrationBuilder.CreateTable("__Caching_Chunks", columns: table => new
        {
            PayloadId = table.Column<byte[]>(maxLength: 32, nullable: false),
            Ordinal = table.Column<int>(nullable: false),
            Offset = table.Column<long>(nullable: false),
            Data = table.Column<byte[]>(maxLength: 1048576, nullable: false),
            Hash = table.Column<byte[]>(maxLength: 64, nullable: false)
        }, constraints: table => table.PrimaryKey("PK___Caching_Chunks", x => new { x.PayloadId, x.Ordinal }));

        migrationBuilder.CreateTable("__Caching_Dimensions", columns: table => new
        {
            EntryId = table.Column<byte[]>(maxLength: 64, nullable: false),
            CollectionId = table.Column<byte[]>(maxLength: 64, nullable: false),
            NameHash = table.Column<byte[]>(maxLength: 64, nullable: false),
            Ordinal = table.Column<int>(nullable: false),
            ValueHash = table.Column<byte[]>(maxLength: 64, nullable: true),
            Kind = table.Column<int>(nullable: false),
            Form = table.Column<int>(nullable: false),
            IsNull = table.Column<bool>(nullable: false),
            NumericValue = table.Column<long>(nullable: true),
            IntervalEnd = table.Column<long>(nullable: true)
        }, constraints: table => table.PrimaryKey("PK___Caching_Dimensions", x => new { x.EntryId, x.NameHash, x.Ordinal }));

        migrationBuilder.CreateTable("__Caching_Fences", columns: table => new
        {
            Id = table.Column<byte[]>(maxLength: 32, nullable: false),
            EntryId = table.Column<byte[]>(maxLength: 64, nullable: false),
            KeyId = table.Column<byte[]>(maxLength: 64, nullable: false),
            CollectionId = table.Column<byte[]>(maxLength: 64, nullable: false),
            KeyEpoch = table.Column<long>(nullable: false),
            PublishedSinceCapture = table.Column<bool>(nullable: false),
            CollectionEpoch = table.Column<long>(nullable: false),
            ObservedVersion = table.Column<byte[]>(maxLength: 32, nullable: true),
            ObservedPublicationTicks = table.Column<long>(nullable: false),
            DeadlineTicks = table.Column<long>(nullable: false)
        }, constraints: table => table.PrimaryKey("PK___Caching_Fences", x => x.Id));

        migrationBuilder.CreateTable("__Caching_Ownerships", columns: table => new
        {
            Id = table.Column<byte[]>(maxLength: 32, nullable: false),
            EntryId = table.Column<byte[]>(maxLength: 64, nullable: false),
            KeyId = table.Column<byte[]>(maxLength: 64, nullable: false),
            CollectionId = table.Column<byte[]>(maxLength: 64, nullable: false),
            NodeId = table.Column<string>(maxLength: 128, unicode: true, nullable: false),
            OwnerToken = table.Column<byte[]>(maxLength: 32, nullable: false),
            FenceId = table.Column<byte[]>(maxLength: 32, nullable: false),
            State = table.Column<int>(nullable: false),
            LeaseExpiryTicks = table.Column<long>(nullable: false),
            DeadlineTicks = table.Column<long>(nullable: false),
            PublishedVersion = table.Column<byte[]>(maxLength: 32, nullable: true),
            FailureCode = table.Column<int>(nullable: true),
            IsNotFound = table.Column<bool>(nullable: false),
            Independent = table.Column<bool>(nullable: false),
            InterestsJson = table.Column<string>(maxLength: 262144, unicode: false, nullable: false)
        }, constraints: table => table.PrimaryKey("PK___Caching_Ownerships", x => x.Id));

        migrationBuilder.CreateTable("__Caching_Readers", columns: table => new
        {
            Id = table.Column<byte[]>(maxLength: 32, nullable: false),
            PayloadId = table.Column<byte[]>(maxLength: 32, nullable: false),
            ExpiryTicks = table.Column<long>(nullable: false)
        }, constraints: table => table.PrimaryKey("PK___Caching_Readers", x => x.Id));

        migrationBuilder.CreateTable("__Caching_Operations", columns: table => new
        {
            Id = table.Column<byte[]>(maxLength: 32, nullable: false),
            CollectionId = table.Column<byte[]>(maxLength: 64, nullable: false),
            Revision = table.Column<byte[]>(maxLength: 32, nullable: false),
            MetadataJson = table.Column<string>(unicode: true, nullable: false),
            State = table.Column<int>(nullable: false),
            CreatedTicks = table.Column<long>(nullable: false),
            UpdatedTicks = table.Column<long>(nullable: false),
            ClaimNode = table.Column<string>(maxLength: 128, unicode: true, nullable: true),
            ClaimUntilTicks = table.Column<long>(nullable: true)
        }, constraints: table => table.PrimaryKey("PK___Caching_Operations", x => x.Id));

        migrationBuilder.CreateIndex(CacheIndexName.Create("__Caching_Contracts", "CollectionId", "ContractVersion"), "__Caching_Contracts", new[] { "CollectionId", "ContractVersion" });
        migrationBuilder.CreateIndex(CacheIndexName.Create("__Caching_Keys", "CollectionId", "Id"), "__Caching_Keys", new[] { "CollectionId", "Id" });
        migrationBuilder.CreateIndex(CacheIndexName.Create("__Caching_Entries", "CollectionId", "KeyId"), "__Caching_Entries", new[] { "CollectionId", "KeyId" });
        migrationBuilder.CreateIndex(CacheIndexName.Create("__Caching_Entries", "CollectionId", "ContractId", "KeyPrefix", "Id"), "__Caching_Entries", new[] { "CollectionId", "ContractId", "KeyPrefix", "Id" });
        migrationBuilder.CreateIndex(CacheIndexName.Create("__Caching_Entries", "CollectionId", "PublishedTicks", "Id"), "__Caching_Entries", new[] { "CollectionId", "PublishedTicks", "Id" });
        migrationBuilder.CreateIndex(CacheIndexName.Create("__Caching_Entries", "CollectionId", "ExpiryTicks", "Id"), "__Caching_Entries", new[] { "CollectionId", "ExpiryTicks", "Id" });
        migrationBuilder.CreateIndex(CacheIndexName.Create("__Caching_Entries", "CollectionId", "StoredBytes", "Id"), "__Caching_Entries", new[] { "CollectionId", "StoredBytes", "Id" });
        migrationBuilder.CreateIndex(CacheIndexName.Create("__Caching_Entries", "CollectionId", "HasForegroundAccess", "RefreshDueTicks", "Id"), "__Caching_Entries", new[] { "CollectionId", "HasForegroundAccess", "RefreshDueTicks", "Id" });
        migrationBuilder.CreateIndex(CacheIndexName.Create("__Caching_Entries", "CollectionId", "ContractId", "HasForegroundAccess", "AbsoluteExpiryTicks", "Id"), "__Caching_Entries", new[] { "CollectionId", "ContractId", "HasForegroundAccess", "AbsoluteExpiryTicks", "Id" });
        migrationBuilder.CreateIndex(CacheIndexName.Create("__Caching_Payloads", "EntryId", "State", "Id"), "__Caching_Payloads", new[] { "EntryId", "State", "Id" });
        migrationBuilder.CreateIndex(CacheIndexName.Create("__Caching_Payloads", "OperationId"), "__Caching_Payloads", new[] { "OperationId" }, unique: true);
        migrationBuilder.CreateIndex(CacheIndexName.Create("__Caching_Payloads", "State", "DeadlineTicks", "Id"), "__Caching_Payloads", new[] { "State", "DeadlineTicks", "Id" });
        migrationBuilder.CreateIndex(CacheIndexName.Create("__Caching_Chunks", "PayloadId", "Offset"), "__Caching_Chunks", new[] { "PayloadId", "Offset" });
        migrationBuilder.CreateIndex(CacheIndexName.Create("__Caching_Dimensions", "CollectionId", "NameHash", "Kind", "Form", "ValueHash", "EntryId"), "__Caching_Dimensions", new[] { "CollectionId", "NameHash", "Kind", "Form", "ValueHash", "EntryId" });
        migrationBuilder.CreateIndex(CacheIndexName.Create("__Caching_Dimensions", "CollectionId", "NameHash", "Kind", "NumericValue", "IntervalEnd", "EntryId"), "__Caching_Dimensions", new[] { "CollectionId", "NameHash", "Kind", "NumericValue", "IntervalEnd", "EntryId" });
        migrationBuilder.CreateIndex(CacheIndexName.Create("__Caching_Fences", "KeyId", "Id"), "__Caching_Fences", new[] { "KeyId", "Id" });
        migrationBuilder.CreateIndex(CacheIndexName.Create("__Caching_Fences", "CollectionId", "DeadlineTicks", "Id"), "__Caching_Fences", new[] { "CollectionId", "DeadlineTicks", "Id" });
        migrationBuilder.CreateIndex(CacheIndexName.Create("__Caching_Ownerships", "EntryId", "Independent", "State", "LeaseExpiryTicks"), "__Caching_Ownerships", new[] { "EntryId", "Independent", "State", "LeaseExpiryTicks" });
        migrationBuilder.CreateIndex(CacheIndexName.Create("__Caching_Ownerships", "CollectionId", "DeadlineTicks", "Id"), "__Caching_Ownerships", new[] { "CollectionId", "DeadlineTicks", "Id" });
        migrationBuilder.CreateIndex(CacheIndexName.Create("__Caching_Readers", "PayloadId", "ExpiryTicks"), "__Caching_Readers", new[] { "PayloadId", "ExpiryTicks" });
        migrationBuilder.CreateIndex(CacheIndexName.Create("__Caching_Readers", "ExpiryTicks", "Id"), "__Caching_Readers", new[] { "ExpiryTicks", "Id" });
        migrationBuilder.CreateIndex(CacheIndexName.Create("__Caching_Operations", "CollectionId", "State", "UpdatedTicks", "Id"), "__Caching_Operations", new[] { "CollectionId", "State", "UpdatedTicks", "Id" });
        migrationBuilder.CreateIndex(CacheIndexName.Create("__Caching_Operations", "State", "ClaimUntilTicks", "Id"), "__Caching_Operations", new[] { "State", "ClaimUntilTicks", "Id" });

        migrationBuilder.CreateIndex(CacheIndexName.Create("__Caching_Entries", "PayloadId"), "__Caching_Entries", "PayloadId");
        migrationBuilder.CreateIndex(CacheIndexName.Create("__Caching_Entries", "PurgeAfterTicks", "Id"), "__Caching_Entries", new[] { "PurgeAfterTicks", "Id" });
        migrationBuilder.CreateIndex(CacheIndexName.Create("__Caching_Entries", "CollectionId", "Priority", "LastAccessTicks", "Id"), "__Caching_Entries", new[] { "CollectionId", "Priority", "LastAccessTicks", "Id" });
        migrationBuilder.CreateIndex(CacheIndexName.Create("__Caching_Fences", "EntryId"), "__Caching_Fences", "EntryId");
        migrationBuilder.CreateIndex(CacheIndexName.Create("__Caching_Entries", "CollectionId", "PublicationSequence", "Id"), "__Caching_Entries", new[] { "CollectionId", "PublicationSequence", "Id" });
        migrationBuilder.AddForeignKey("FK___Caching_Entries___Caching_Payloads_PayloadId", "__Caching_Entries", "PayloadId", "__Caching_Payloads", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey("FK___Caching_Chunks___Caching_Payloads_PayloadId", "__Caching_Chunks", "PayloadId", "__Caching_Payloads", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
        migrationBuilder.AddForeignKey("FK___Caching_Dimensions___Caching_Entries_EntryId", "__Caching_Dimensions", "EntryId", "__Caching_Entries", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
        migrationBuilder.AddForeignKey("FK___Caching_Readers___Caching_Payloads_PayloadId", "__Caching_Readers", "PayloadId", "__Caching_Payloads", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.InsertData("__Caching_Stores", new[] { "Id", "MetadataFormat", "IdentityFormat", "CursorKey", "MaximumBytes", "MaximumEntries", "CurrentBytes", "RetiredBytes", "StagingBytes", "MetadataBytes", "CurrentEntries" },
            new object[] { Encoding.ASCII.GetBytes("root"), 1, 1, new byte[32], 0L, 0L, 0L, 0L, 0L, 0L, 0L });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("__Caching_Dimensions");
        migrationBuilder.DropTable("__Caching_Chunks");
        migrationBuilder.DropTable("__Caching_Readers");
        migrationBuilder.DropTable("__Caching_Entries");
        migrationBuilder.DropTable("__Caching_Payloads");
        migrationBuilder.DropTable("__Caching_Stores");
        migrationBuilder.DropTable("__Caching_Collections");
        migrationBuilder.DropTable("__Caching_Contracts");
        migrationBuilder.DropTable("__Caching_Keys");
        migrationBuilder.DropTable("__Caching_Fences");
        migrationBuilder.DropTable("__Caching_Ownerships");
        migrationBuilder.DropTable("__Caching_Operations");
    }

    /// <inheritdoc />
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        CacheTestHostModel.Configure(modelBuilder);
    }
}