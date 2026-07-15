// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Repositories.Bulk;

using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;

public class EntityBulkInsertMappingBuilderTests
{
    [Fact]
    public void Build_FlatEntity_ReturnsWritableConvertedColumns()
    {
        // Arrange
        using var context = CreateContext();
        var entity = new FlatEntity { Name = "Ada", Status = MappingStatus.Active };
        var sut = new EntityBulkInsertMappingBuilder<FlatEntity>();

        // Act
        var batch = sut.Build(context, [entity], new EntityBulkInsertOptions());

        // Assert
        batch.TableName.ShouldBe("FlatEntities");
        batch.Columns.Select(column => column.ColumnName).ShouldContain(nameof(FlatEntity.Name));
        batch.Columns.Select(column => column.ColumnName).ShouldContain(nameof(FlatEntity.Status));
        batch.Columns.Select(column => column.ColumnName).ShouldNotContain(nameof(FlatEntity.DatabaseIdentity));
        batch.Columns.Select(column => column.ColumnName).ShouldNotContain(nameof(FlatEntity.ComputedOn));

        var statusColumn = batch.Columns.Single(column => column.Property.Name == nameof(FlatEntity.Status));
        statusColumn.ProviderClrType.ShouldBe(typeof(string));
        statusColumn.GetProviderValue(entity).ShouldBe("Active");
    }

    [Fact]
    public void Build_KeepGeneratedIdentityValues_IncludesStoreGeneratedIdentityColumn()
    {
        // Arrange
        using var context = CreateContext();
        var entity = new FlatEntity { DatabaseIdentity = 42 };
        var sut = new EntityBulkInsertMappingBuilder<FlatEntity>();

        // Act
        var batch = sut.Build(context, [entity], new EntityBulkInsertOptions
        {
            KeepGeneratedIdentityValues = true
        });

        // Assert
        batch.Columns.Select(column => column.Property.Name).ShouldContain(nameof(FlatEntity.DatabaseIdentity));
        batch.Columns.Select(column => column.Property.Name).ShouldNotContain(nameof(FlatEntity.ComputedOn));
    }

    [Fact]
    public void Build_EntityWithSameTableOwnedReference_FlattensOwnedColumns()
    {
        // Arrange
        using var context = CreateContext();
        var entity = new OwnedReferenceEntity
        {
            Details = new OwnedReferenceValue { Name = "Details" }
        };
        var sut = new EntityBulkInsertMappingBuilder<OwnedReferenceEntity>();

        // Act
        var batch = sut.Build(context, [entity], new EntityBulkInsertOptions());

        // Assert
        var ownedColumn = batch.Columns.Single(column => column.ColumnName == "Details_Name");
        ownedColumn.GetProviderValue(entity).ShouldBe("Details");
    }

    [Fact]
    public void Build_EntityWithNullSameTableOwnedReference_UsesDatabaseNullValue()
    {
        // Arrange
        using var context = CreateContext();
        var entity = new OwnedReferenceEntity { Details = null };
        var sut = new EntityBulkInsertMappingBuilder<OwnedReferenceEntity>();

        // Act
        var batch = sut.Build(context, [entity], new EntityBulkInsertOptions());

        // Assert
        var ownedColumn = batch.Columns.Single(column => column.ColumnName == "Details_Name");
        ownedColumn.GetProviderValue(entity).ShouldBeNull();
    }

    [Fact]
    public void Build_EntityWithDefaultGuidKey_AssignsSequentialGuid()
    {
        // Arrange
        using var context = CreateContext();
        var entity = new FlatEntity();
        var sut = new EntityBulkInsertMappingBuilder<FlatEntity>();

        // Act
        sut.Build(context, [entity], new EntityBulkInsertOptions());

        // Assert
        entity.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Build_EntityWithDefaultTypedGuidKey_AssignsSequentialGuid()
    {
        // Arrange
        using var context = CreateContext();
        var entity = new TypedIdEntity { Id = MappingEntityId.Create(Guid.Empty) };
        var sut = new EntityBulkInsertMappingBuilder<TypedIdEntity>();

        // Act
        sut.Build(context, [entity], new EntityBulkInsertOptions());

        // Assert
        entity.Id.Value.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Build_EntityImplementingConcurrency_AssignsConcurrencyVersion()
    {
        // Arrange
        using var context = CreateContext();
        var entity = new FlatEntity();
        var sut = new EntityBulkInsertMappingBuilder<FlatEntity>();

        // Act
        sut.Build(context, [entity], new EntityBulkInsertOptions());

        // Assert
        entity.ConcurrencyVersion.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Build_EntityWithNonOwnedNavigation_ThrowsNotSupportedException()
    {
        // Arrange
        using var context = CreateContext();
        var sut = new EntityBulkInsertMappingBuilder<RelatedEntityRoot>();

        // Act
        var exception = Assert.Throws<NotSupportedException>(() =>
            sut.Build(context, [new RelatedEntityRoot()], new EntityBulkInsertOptions()));

        // Assert
        exception.Message.ShouldContain(nameof(RelatedEntityRoot.Related));
    }

    [Fact]
    public void Build_EntityWithPopulatedOwnedCollection_ThrowsNotSupportedException()
    {
        // Arrange
        using var context = CreateContext();
        var entity = new OwnedCollectionEntity
        {
            Items = [new OwnedCollectionValue { Name = "Item" }]
        };
        var sut = new EntityBulkInsertMappingBuilder<OwnedCollectionEntity>();

        // Act
        var exception = Assert.Throws<NotSupportedException>(() =>
            sut.Build(context, [entity], new EntityBulkInsertOptions()));

        // Assert
        exception.Message.ShouldContain(nameof(OwnedCollectionEntity.Items));
    }

    [Fact]
    public void Build_UnmappedEntity_ThrowsInvalidOperationException()
    {
        // Arrange
        using var context = CreateContext();
        var sut = new EntityBulkInsertMappingBuilder<UnmappedEntity>();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            sut.Build(context, [new UnmappedEntity()], new EntityBulkInsertOptions()));

        // Assert
        exception.Message.ShouldContain("not part of the DbContext model");
    }

    [Fact]
    public void Build_EntityWithoutWritableColumns_ThrowsInvalidOperationException()
    {
        // Arrange
        using var context = CreateContext();
        var sut = new EntityBulkInsertMappingBuilder<NoWritableColumnsEntity>();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            sut.Build(context, [new NoWritableColumnsEntity()], new EntityBulkInsertOptions()));

        // Assert
        exception.Message.ShouldContain("has no writable columns");
    }

    [Fact]
    public void Build_EntityWithDuplicateWritableColumns_ThrowsInvalidOperationException()
    {
        // Arrange
        using var context = CreateContext();
        var sut = new EntityBulkInsertMappingBuilder<DuplicateColumnsEntity>();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            sut.Build(context, [new DuplicateColumnsEntity()], new EntityBulkInsertOptions()));

        // Assert
        exception.Message.ShouldContain("Duplicate");
    }

    [Fact]
    public void Build_InvalidOptions_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var context = CreateContext();
        var sut = new EntityBulkInsertMappingBuilder<FlatEntity>();

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            sut.Build(context, [new FlatEntity()], new EntityBulkInsertOptions { BatchSize = 0 }));

        // Assert
        exception.ParamName.ShouldBe(nameof(EntityBulkInsertOptions.BatchSize));
    }

    private static MappingDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MappingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new MappingDbContext(options);
    }

    private sealed class MappingDbContext(DbContextOptions<MappingDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ConfigureFlatEntity(modelBuilder);
            ConfigureOwnedReferenceEntity(modelBuilder);
            ConfigureOwnedCollectionEntity(modelBuilder);
            ConfigureRelatedEntityRoot(modelBuilder);
            ConfigureTypedIdEntity(modelBuilder);
            ConfigureNoWritableColumnsEntity(modelBuilder);
            ConfigureDuplicateColumnsEntity(modelBuilder);
        }

        private static void ConfigureFlatEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FlatEntity>(builder =>
            {
                builder.ToTable("FlatEntities");
                builder.HasKey(entity => entity.Id);
                builder.Property(entity => entity.Id).ValueGeneratedOnAdd();
                builder.Property(entity => entity.Status).HasConversion<string>();
                builder.Property(entity => entity.DatabaseIdentity).ValueGeneratedOnAdd();
                builder.Property(entity => entity.ComputedOn).ValueGeneratedOnAddOrUpdate();
            });
        }

        private static void ConfigureOwnedReferenceEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OwnedReferenceEntity>(builder =>
            {
                builder.ToTable("OwnedReferenceEntities");
                builder.HasKey(entity => entity.Id);
                builder.Property(entity => entity.Id).ValueGeneratedOnAdd();
                builder.OwnsOne(entity => entity.Details, owned =>
                    owned.Property(value => value.Name).HasColumnName("Details_Name"));
            });
        }

        private static void ConfigureOwnedCollectionEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OwnedCollectionEntity>(builder =>
            {
                builder.ToTable("OwnedCollectionEntities");
                builder.HasKey(entity => entity.Id);
                builder.Property(entity => entity.Id).ValueGeneratedOnAdd();
                builder.OwnsMany(entity => entity.Items, owned =>
                {
                    owned.ToTable("OwnedCollectionItems");
                    owned.WithOwner().HasForeignKey("OwnerId");
                    owned.HasKey("Id");
                });
            });
        }

        private static void ConfigureRelatedEntityRoot(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RelatedEntityRoot>(builder =>
            {
                builder.ToTable("RelatedEntityRoots");
                builder.HasKey(entity => entity.Id);
                builder.Property(entity => entity.Id).ValueGeneratedOnAdd();
                builder.HasOne(entity => entity.Related)
                    .WithMany()
                    .HasForeignKey(entity => entity.RelatedId);
            });

            modelBuilder.Entity<RelatedEntity>(builder =>
            {
                builder.ToTable("RelatedEntities");
                builder.HasKey(entity => entity.Id);
            });
        }

        private static void ConfigureTypedIdEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TypedIdEntity>(builder =>
            {
                builder.ToTable("TypedIdEntities");
                builder.HasKey(entity => entity.Id);
                builder.Property(entity => entity.Id)
                    .ValueGeneratedOnAdd()
                    .HasConversion(id => id.Value, value => MappingEntityId.Create(value));
            });
        }

        private static void ConfigureNoWritableColumnsEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NoWritableColumnsEntity>(builder =>
            {
                builder.ToTable("NoWritableColumnsEntities");
                builder.HasKey(entity => entity.Id);
                builder.Property(entity => entity.Id).ValueGeneratedOnAdd();
                builder.Property(entity => entity.ComputedOn).ValueGeneratedOnAddOrUpdate();
            });
        }

        private static void ConfigureDuplicateColumnsEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DuplicateColumnsEntity>(builder =>
            {
                builder.ToTable("DuplicateColumnsEntities");
                builder.HasKey(entity => entity.Id);
                builder.Property(entity => entity.Id).ValueGeneratedOnAdd();
                builder.Property(entity => entity.Name).HasColumnName("Duplicate");
                builder.OwnsOne(entity => entity.Details, owned =>
                    owned.Property(value => value.Name).HasColumnName("Duplicate"));
            });
        }
    }

    private sealed class FlatEntity : IConcurrency
    {
        public Guid Id { get; set; }

        public string Name { get; set; }

        public MappingStatus Status { get; set; }

        public int DatabaseIdentity { get; set; }

        public DateTime ComputedOn { get; set; }

        public Guid ConcurrencyVersion { get; set; }
    }

    private sealed class OwnedReferenceEntity
    {
        public Guid Id { get; set; }

        public OwnedReferenceValue Details { get; set; }
    }

    private sealed class OwnedReferenceValue
    {
        public string Name { get; set; }
    }

    private sealed class OwnedCollectionEntity
    {
        public Guid Id { get; set; }

        public List<OwnedCollectionValue> Items { get; set; } = [];
    }

    private sealed class OwnedCollectionValue
    {
        public string Name { get; set; }
    }

    private sealed class RelatedEntityRoot
    {
        public Guid Id { get; set; }

        public Guid RelatedId { get; set; }

        public RelatedEntity Related { get; set; }
    }

    private sealed class RelatedEntity
    {
        public Guid Id { get; set; }
    }

    private sealed class TypedIdEntity
    {
        public MappingEntityId Id { get; set; }
    }

    private sealed class MappingEntityId : EntityId<Guid>
    {
        public override Guid Value { get; protected set; }

        public static MappingEntityId Create(Guid value)
        {
            return new MappingEntityId { Value = value };
        }

        protected override IEnumerable<object> GetAtomicValues()
        {
            yield return this.Value;
        }
    }

    private sealed class NoWritableColumnsEntity
    {
        public int Id { get; set; }

        public DateTime ComputedOn { get; set; }
    }

    private sealed class DuplicateColumnsEntity
    {
        public Guid Id { get; set; }

        public string Name { get; set; }

        public DuplicateColumnsValue Details { get; set; }
    }

    private sealed class DuplicateColumnsValue
    {
        public string Name { get; set; }
    }

    private sealed class UnmappedEntity;

    private enum MappingStatus
    {
        Inactive,
        Active
    }
}
