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
        var entity = new FlatEntity
        {
            Name = "Ada",
            Status = MappingStatus.Active,
            NumericStatus = MappingStatus.Active,
        };
        var sut = new EntityBulkInsertMappingBuilder<FlatEntity>();

        // Act
        var batch = sut.Build(context, [entity], new EntityBulkInsertOptions());

        // Assert
        batch.TableName.ShouldBe("FlatEntities");
        batch.Columns.Select(column => column.ColumnName).ShouldContain(nameof(FlatEntity.Name));
        batch.Columns.Select(column => column.ColumnName).ShouldContain(nameof(FlatEntity.Status));
        batch
            .Columns.Select(column => column.ColumnName)
            .ShouldNotContain(nameof(FlatEntity.DatabaseIdentity));
        batch
            .Columns.Select(column => column.ColumnName)
            .ShouldNotContain(nameof(FlatEntity.ComputedOn));
        batch
            .Columns.Select(column => column.ColumnName)
            .ShouldNotContain(nameof(FlatEntity.DefaultName));
        batch
            .Columns.Select(column => column.ColumnName)
            .ShouldNotContain(nameof(FlatEntity.RowVersion));
        batch
            .Columns.Select(column => column.ColumnName)
            .ShouldNotContain(nameof(FlatEntity.GeneratedNumber));

        var statusColumn = batch.Columns.Single(column =>
            column.Property.Name == nameof(FlatEntity.Status)
        );
        statusColumn.ProviderClrType.ShouldBe(typeof(string));
        statusColumn.GetProviderValue(entity).ShouldBe("Active");
        var numericStatusColumn = batch.Columns.Single(column =>
            column.Property.Name == nameof(FlatEntity.NumericStatus)
        );
        numericStatusColumn.ProviderClrType.ShouldBe(typeof(int));
        numericStatusColumn.GetProviderValue(entity).ShouldBe(1);
    }

    [Fact]
    public void Build_KeepGeneratedIdentityValues_IncludesStoreGeneratedIdentityColumn()
    {
        // Arrange
        using var context = CreateContext();
        var entity = new FlatEntity { DatabaseIdentity = 42 };
        var sut = new EntityBulkInsertMappingBuilder<FlatEntity>();

        // Act
        var batch = sut.Build(
            context,
            [entity],
            new EntityBulkInsertOptions { KeepGeneratedIdentityValues = true }
        );

        // Assert
        batch
            .Columns.Select(column => column.Property.Name)
            .ShouldContain(nameof(FlatEntity.DatabaseIdentity));
        batch
            .Columns.Single(column => column.Property.Name == nameof(FlatEntity.DatabaseIdentity))
            .IsIdentity.ShouldBeTrue();
        batch
            .Columns.Select(column => column.Property.Name)
            .ShouldNotContain(nameof(FlatEntity.ComputedOn));
        batch
            .Columns.Select(column => column.Property.Name)
            .ShouldNotContain(nameof(FlatEntity.DefaultName));
        batch
            .Columns.Select(column => column.Property.Name)
            .ShouldNotContain(nameof(FlatEntity.GeneratedNumber));
    }

    [Fact]
    public void Build_EntityWithSameTableOwnedReference_FlattensOwnedColumns()
    {
        // Arrange
        using var context = CreateContext();
        var entity = new OwnedReferenceEntity
        {
            Details = new OwnedReferenceValue { Name = "Details" },
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
    public void Analyze_SameTableOwnedReferenceWithOwnerBackReference_AllowsRootOnlyInsert()
    {
        // Arrange
        using var context = CreateContext();
        var entity = new OwnedReferenceEntity();
        entity.Details = new OwnedReferenceValue { Name = "Details", Owner = entity };
        var sut = new EntityBulkInsertMappingBuilder<OwnedReferenceEntity>();

        // Act
        var analysis = sut.Analyze(context, [entity], new EntityBulkInsertOptions());

        // Assert
        analysis.Entities.ShouldHaveSingleItem();
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
    public void Build_EntityImplementingConcurrency_DoesNotAssignConcurrencyVersion()
    {
        // Arrange
        using var context = CreateContext();
        var entity = new FlatEntity();
        var sut = new EntityBulkInsertMappingBuilder<FlatEntity>();

        // Act
        sut.Build(context, [entity], new EntityBulkInsertOptions());

        // Assert
        entity.ConcurrencyVersion.ShouldBe(Guid.Empty);
    }

    [Fact]
    public void Build_EntityWithNonOwnedNavigation_ThrowsNotSupportedException()
    {
        // Arrange
        using var context = CreateContext();
        var sut = new EntityBulkInsertMappingBuilder<RelatedEntityRoot>();

        // Act
        var entity = new RelatedEntityRoot { Related = new RelatedEntity() };

        // Act
        var exception = Assert.Throws<NotSupportedException>(() =>
            sut.Build(context, [entity], new EntityBulkInsertOptions())
        );

        // Assert
        exception.Message.ShouldContain(nameof(RelatedEntityRoot.Related));
    }

    [Fact]
    public void Build_EntityWithNullNonOwnedNavigation_AllowsRootOnlyInsert()
    {
        // Arrange
        using var context = CreateContext();
        var sut = new EntityBulkInsertMappingBuilder<RelatedEntityRoot>();

        // Act
        var batch = sut.Build(
            context,
            [new RelatedEntityRoot { Related = null }],
            new EntityBulkInsertOptions()
        );

        // Assert
        batch.TableName.ShouldBe("RelatedEntityRoots");
    }

    [Fact]
    public void Analyze_EntityWithEmptyNonOwnedCollection_AllowsRootOnlyInsert()
    {
        // Arrange
        using var context = CreateContext();
        var sut = new EntityBulkInsertMappingBuilder<RelatedEntityRoot>();

        // Act
        var analysis = sut.Analyze(
            context,
            [new RelatedEntityRoot { RelatedItems = [] }],
            new EntityBulkInsertOptions()
        );

        // Assert
        analysis.Entities.ShouldHaveSingleItem();
    }

    [Fact]
    public void Analyze_EntityWithPopulatedNonOwnedCollection_ThrowsNotSupportedException()
    {
        // Arrange
        using var context = CreateContext();
        var sut = new EntityBulkInsertMappingBuilder<RelatedEntityRoot>();

        // Act
        var exception = Assert.Throws<NotSupportedException>(() =>
            sut.Analyze(
                context,
                [new RelatedEntityRoot { RelatedItems = [new RelatedEntity()] }],
                new EntityBulkInsertOptions()
            )
        );

        // Assert
        exception.Message.ShouldContain(nameof(RelatedEntityRoot.RelatedItems));
    }

    [Fact]
    public void Analyze_DefaultGeneratedValues_DoesNotMutateEntities()
    {
        // Arrange
        using var context = CreateContext();
        var entity = new FlatEntity();
        var sut = new EntityBulkInsertMappingBuilder<FlatEntity>();

        // Act
        var analysis = sut.Analyze(context, [entity], new EntityBulkInsertOptions());

        // Assert
        analysis.Entities.ShouldHaveSingleItem();
        entity.Id.ShouldBe(Guid.Empty);
        entity.ConcurrencyVersion.ShouldBe(Guid.Empty);
    }

    [Fact]
    public void Build_PrecomputedAnalysis_AssignsGeneratedValuesAfterAnalysis()
    {
        // Arrange
        using var context = CreateContext();
        var entity = new FlatEntity();
        var sut = new EntityBulkInsertMappingBuilder<FlatEntity>();
        var analysis = sut.Analyze(context, [entity], new EntityBulkInsertOptions());

        // Act
        var batch = sut.Build(analysis);

        // Assert
        batch.Entities.ShouldHaveSingleItem();
        entity.Id.ShouldNotBe(Guid.Empty);
        entity.ConcurrencyVersion.ShouldBe(Guid.Empty);
    }

    [Fact]
    public void Build_DetachedEntity_DoesNotTrackOrHydrateStoreGeneratedValues()
    {
        // Arrange
        using var context = CreateContext();
        var entity = new FlatEntity
        {
            DatabaseIdentity = 42,
            DefaultName = "original",
            ComputedOn = new DateTime(2026, 7, 22),
            RowVersion = [1, 2, 3],
        };
        var sut = new EntityBulkInsertMappingBuilder<FlatEntity>();

        // Act
        sut.Build(context, [entity], new EntityBulkInsertOptions());

        // Assert
        context.Entry(entity).State.ShouldBe(EntityState.Detached);
        entity.DatabaseIdentity.ShouldBe(42);
        entity.DefaultName.ShouldBe("original");
        entity.ComputedOn.ShouldBe(new DateTime(2026, 7, 22));
        entity.RowVersion.ShouldBe([1, 2, 3]);
    }

    [Fact]
    public void Build_MissingRequiredClrValue_ThrowsBeforeGeneratingKey()
    {
        // Arrange
        using var context = CreateContext();
        var entity = new RequiredClrEntity { RequiredName = null };
        var sut = new EntityBulkInsertMappingBuilder<RequiredClrEntity>();
        var analysis = sut.Analyze(context, [entity], new EntityBulkInsertOptions());

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => sut.Build(analysis));

        // Assert
        exception.Message.ShouldContain(nameof(RequiredClrEntity.RequiredName));
        entity.Id.ShouldBe(Guid.Empty);
    }

    [Fact]
    public void Analyze_DuplicateEntityReference_ThrowsWithoutMutation()
    {
        // Arrange
        using var context = CreateContext();
        var entity = new FlatEntity();
        var sut = new EntityBulkInsertMappingBuilder<FlatEntity>();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            sut.Analyze(context, [entity, entity], new EntityBulkInsertOptions())
        );

        // Assert
        exception.Message.ShouldContain("same entity instance");
        entity.Id.ShouldBe(Guid.Empty);
    }

    [Fact]
    public void Analyze_TrackedEntity_ThrowsWithoutChangingTrackingState()
    {
        // Arrange
        using var context = CreateContext();
        var entity = new FlatEntity { Id = Guid.NewGuid() };
        context.Attach(entity);
        var sut = new EntityBulkInsertMappingBuilder<FlatEntity>();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            sut.Analyze(context, [entity], new EntityBulkInsertOptions())
        );

        // Assert
        exception.Message.ShouldContain("already tracked");
        context.Entry(entity).State.ShouldBe(EntityState.Unchanged);
    }

    [Fact]
    public void Analyze_EmptyBatch_PreservesEmptyAnalysis()
    {
        // Arrange
        using var context = CreateContext();
        var sut = new EntityBulkInsertMappingBuilder<FlatEntity>();

        // Act
        var analysis = sut.Analyze(context, [], new EntityBulkInsertOptions());

        // Assert
        analysis.Entities.ShouldBeEmpty();
    }

    [Fact]
    public void Analyze_RequiredShadowWithoutProvider_ThrowsInvalidOperationException()
    {
        // Arrange
        using var context = CreateContext();
        var sut = new EntityBulkInsertMappingBuilder<RequiredShadowEntity>();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            sut.Analyze(context, [new RequiredShadowEntity()], new EntityBulkInsertOptions())
        );

        // Assert
        exception.Message.ShouldContain("TenantId");
        exception.Message.ShouldContain(
            nameof(IEntityBulkInsertShadowValueProvider<RequiredShadowEntity>)
        );
    }

    [Fact]
    public void Build_RequiredShadowWithProvider_UsesProviderValue()
    {
        // Arrange
        using var context = CreateContext();
        var entity = new RequiredShadowEntity();
        var sut = new EntityBulkInsertMappingBuilder<RequiredShadowEntity>([
            new RequiredShadowValueProvider("tenant-42"),
        ]);

        // Act
        var batch = sut.Build(context, [entity], new EntityBulkInsertOptions());

        // Assert
        var column = batch.Columns.Single(column => column.Property.Name == "TenantId");
        column.Source.ShouldBe(EntityBulkInsertColumnSource.ShadowProvider);
        column.GetProviderValue(entity).ShouldBe("tenant-42");
    }

    [Fact]
    public void Build_TphDerivedEntity_IncludesMetadataDiscriminator()
    {
        // Arrange
        using var context = CreateContext();
        var entity = new TphDerivedEntity();
        var sut = new EntityBulkInsertMappingBuilder<TphDerivedEntity>();

        // Act
        var batch = sut.Build(context, [entity], new EntityBulkInsertOptions());

        // Assert
        var discriminator = batch.Columns.Single(column => column.Property.Name == "Kind");
        discriminator.Source.ShouldBe(EntityBulkInsertColumnSource.MetadataConstant);
        discriminator.GetProviderValue(entity).ShouldBe("derived");
    }

    [Theory]
    [InlineData(typeof(TptDerivedEntity), "TPT")]
    [InlineData(typeof(TpcDerivedEntity), "TPC")]
    public void Analyze_MultiTableInheritance_ThrowsNotSupportedException(
        Type entityType,
        string expectedStrategy
    )
    {
        // Arrange
        using var context = CreateContext();

        // Act
        var exception =
            entityType == typeof(TptDerivedEntity)
                ? Assert.Throws<NotSupportedException>(() =>
                    new EntityBulkInsertMappingBuilder<TptDerivedEntity>().Analyze(
                        context,
                        [new TptDerivedEntity()],
                        new EntityBulkInsertOptions()
                    )
                )
                : Assert.Throws<NotSupportedException>(() =>
                    new EntityBulkInsertMappingBuilder<TpcDerivedEntity>().Analyze(
                        context,
                        [new TpcDerivedEntity()],
                        new EntityBulkInsertOptions()
                    )
                );

        // Assert
        exception.Message.ShouldContain(expectedStrategy);
    }

    [Fact]
    public void Analyze_SeparateTableOwnedReference_ThrowsEvenWhenNull()
    {
        // Arrange
        using var context = CreateContext();
        var sut = new EntityBulkInsertMappingBuilder<SeparateOwnedEntity>();

        // Act
        var exception = Assert.Throws<NotSupportedException>(() =>
            sut.Analyze(context, [new SeparateOwnedEntity()], new EntityBulkInsertOptions())
        );

        // Assert
        exception.Message.ShouldContain("separate-table");
    }

    [Fact]
    public void Analyze_JsonOwnedReference_ThrowsEvenWhenNull()
    {
        // Arrange
        using var context = CreateContext();
        var sut = new EntityBulkInsertMappingBuilder<JsonOwnedEntity>();

        // Act
        var exception = Assert.Throws<NotSupportedException>(() =>
            sut.Analyze(context, [new JsonOwnedEntity()], new EntityBulkInsertOptions())
        );

        // Assert
        exception.Message.ShouldContain("JSON-owned");
    }

    [Fact]
    public void Build_EntityWithPopulatedOwnedCollection_ThrowsNotSupportedException()
    {
        // Arrange
        using var context = CreateContext();
        var entity = new OwnedCollectionEntity
        {
            Items = [new OwnedCollectionValue { Name = "Item" }],
        };
        var sut = new EntityBulkInsertMappingBuilder<OwnedCollectionEntity>();

        // Act
        var exception = Assert.Throws<NotSupportedException>(() =>
            sut.Build(context, [entity], new EntityBulkInsertOptions())
        );

        // Assert
        exception.Message.ShouldContain(nameof(OwnedCollectionEntity.Items));
    }

    [Fact]
    public void Analyze_EntityWithEmptyOwnedCollection_AllowsRootOnlyInsert()
    {
        // Arrange
        using var context = CreateContext();
        var sut = new EntityBulkInsertMappingBuilder<OwnedCollectionEntity>();

        // Act
        var analysis = sut.Analyze(
            context,
            [new OwnedCollectionEntity { Items = [] }],
            new EntityBulkInsertOptions()
        );

        // Assert
        analysis.Entities.ShouldHaveSingleItem();
    }

    [Fact]
    public void Analyze_EntitySplitting_ThrowsNotSupportedException()
    {
        // Arrange
        using var context = CreateContext();
        var sut = new EntityBulkInsertMappingBuilder<SplitEntity>();

        // Act
        var exception = Assert.Throws<NotSupportedException>(() =>
            sut.Analyze(context, [new SplitEntity()], new EntityBulkInsertOptions())
        );

        // Assert
        exception.Message.ShouldContain("multi-table");
    }

    [Fact]
    public void Build_UnmappedEntity_ThrowsInvalidOperationException()
    {
        // Arrange
        using var context = CreateContext();
        var sut = new EntityBulkInsertMappingBuilder<UnmappedEntity>();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            sut.Build(context, [new UnmappedEntity()], new EntityBulkInsertOptions())
        );

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
            sut.Build(context, [new NoWritableColumnsEntity()], new EntityBulkInsertOptions())
        );

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
            sut.Build(context, [new DuplicateColumnsEntity()], new EntityBulkInsertOptions())
        );

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
            sut.Build(context, [new FlatEntity()], new EntityBulkInsertOptions { BatchSize = 0 })
        );

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

    private sealed class MappingDbContext(DbContextOptions<MappingDbContext> options)
        : DbContext(options)
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
            ConfigureRequiredShadowEntity(modelBuilder);
            ConfigureTphEntity(modelBuilder);
            ConfigureTptEntity(modelBuilder);
            ConfigureTpcEntity(modelBuilder);
            ConfigureSeparateOwnedEntity(modelBuilder);
            ConfigureJsonOwnedEntity(modelBuilder);
            ConfigureRequiredClrEntity(modelBuilder);
            ConfigureSplitEntity(modelBuilder);
        }

        private static void ConfigureFlatEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FlatEntity>(builder =>
            {
                builder.ToTable("FlatEntities");
                builder.HasKey(entity => entity.Id);
                builder.Property(entity => entity.Id).ValueGeneratedOnAdd();
                builder.Property(entity => entity.Status).HasConversion<string>();
                builder
                    .Property(entity => entity.DatabaseIdentity)
                    .ValueGeneratedOnAdd()
                    .HasAnnotation("SqlServer:ValueGenerationStrategy", "IdentityColumn");
                builder.Property(entity => entity.DefaultName).HasDefaultValue("database-default");
                builder.Property(entity => entity.ComputedOn).ValueGeneratedOnAddOrUpdate();
                builder.Property(entity => entity.RowVersion).IsRowVersion();
                builder.Property(entity => entity.GeneratedNumber).ValueGeneratedOnAdd();
            });
        }

        private static void ConfigureOwnedReferenceEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OwnedReferenceEntity>(builder =>
            {
                builder.ToTable("OwnedReferenceEntities");
                builder.HasKey(entity => entity.Id);
                builder.Property(entity => entity.Id).ValueGeneratedOnAdd();
                builder.OwnsOne(
                    entity => entity.Details,
                    owned =>
                    {
                        owned.WithOwner(value => value.Owner);
                        owned.Property(value => value.Name).HasColumnName("Details_Name");
                    }
                );
            });
        }

        private static void ConfigureOwnedCollectionEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OwnedCollectionEntity>(builder =>
            {
                builder.ToTable("OwnedCollectionEntities");
                builder.HasKey(entity => entity.Id);
                builder.Property(entity => entity.Id).ValueGeneratedOnAdd();
                builder.OwnsMany(
                    entity => entity.Items,
                    owned =>
                    {
                        owned.ToTable("OwnedCollectionItems");
                        owned.WithOwner().HasForeignKey("OwnerId");
                        owned.HasKey("Id");
                    }
                );
            });
        }

        private static void ConfigureRelatedEntityRoot(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RelatedEntityRoot>(builder =>
            {
                builder.ToTable("RelatedEntityRoots");
                builder.HasKey(entity => entity.Id);
                builder.Property(entity => entity.Id).ValueGeneratedOnAdd();
                builder
                    .HasOne(entity => entity.Related)
                    .WithMany()
                    .HasForeignKey(entity => entity.RelatedId);
                builder.HasMany(entity => entity.RelatedItems).WithOne();
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
                builder
                    .Property(entity => entity.Id)
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
                builder.OwnsOne(
                    entity => entity.Details,
                    owned => owned.Property(value => value.Name).HasColumnName("Duplicate")
                );
            });
        }

        private static void ConfigureRequiredShadowEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RequiredShadowEntity>(builder =>
            {
                builder.ToTable("RequiredShadowEntities");
                builder.HasKey(entity => entity.Id);
                builder.Property(entity => entity.Id).ValueGeneratedOnAdd();
                builder.Property<string>("TenantId").IsRequired();
            });
        }

        private static void ConfigureTphEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TphBaseEntity>(builder =>
            {
                builder.ToTable("TphEntities");
                builder.HasKey(entity => entity.Id);
                builder.HasDiscriminator<string>("Kind").HasValue<TphDerivedEntity>("derived");
            });
            modelBuilder.Entity<TphDerivedEntity>();
        }

        private static void ConfigureTptEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TptBaseEntity>(builder =>
            {
                builder.UseTptMappingStrategy();
                builder.ToTable("TptBaseEntities");
                builder.HasKey(entity => entity.Id);
            });
            modelBuilder.Entity<TptDerivedEntity>().ToTable("TptDerivedEntities");
        }

        private static void ConfigureTpcEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TpcBaseEntity>(builder =>
            {
                builder.UseTpcMappingStrategy();
                builder.HasKey(entity => entity.Id);
            });
            modelBuilder.Entity<TpcDerivedEntity>().ToTable("TpcDerivedEntities");
        }

        private static void ConfigureSeparateOwnedEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SeparateOwnedEntity>(builder =>
            {
                builder.ToTable("SeparateOwnedEntities");
                builder.HasKey(entity => entity.Id);
                builder.OwnsOne(
                    entity => entity.Details,
                    owned => owned.ToTable("SeparateOwnedDetails")
                );
            });
        }

        private static void ConfigureJsonOwnedEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<JsonOwnedEntity>(builder =>
            {
                builder.ToTable("JsonOwnedEntities");
                builder.HasKey(entity => entity.Id);
                builder.OwnsOne(entity => entity.Details, owned => owned.ToJson());
            });
        }

        private static void ConfigureRequiredClrEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RequiredClrEntity>(builder =>
            {
                builder.ToTable("RequiredClrEntities");
                builder.HasKey(entity => entity.Id);
                builder.Property(entity => entity.Id).ValueGeneratedOnAdd();
                builder.Property(entity => entity.RequiredName).IsRequired();
            });
        }

        private static void ConfigureSplitEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SplitEntity>(builder =>
            {
                builder.ToTable("SplitEntities");
                builder.HasKey(entity => entity.Id);
                builder.SplitToTable(
                    "SplitEntityDetails",
                    table => table.Property(entity => entity.SecondaryName)
                );
            });
        }
    }

    private sealed class FlatEntity : Entity<Guid>, IConcurrency
    {
        public string Name { get; set; }

        public MappingStatus Status { get; set; }

        public MappingStatus NumericStatus { get; set; }

        public int DatabaseIdentity { get; set; }

        public string DefaultName { get; set; }

        public DateTime ComputedOn { get; set; }

        public byte[] RowVersion { get; set; }

        public int GeneratedNumber { get; set; }

        public Guid ConcurrencyVersion { get; set; }
    }

    private sealed class OwnedReferenceEntity : Entity<Guid>
    {
        public OwnedReferenceValue Details { get; set; }
    }

    private sealed class OwnedReferenceValue
    {
        public string Name { get; set; }

        public OwnedReferenceEntity Owner { get; set; }
    }

    private sealed class OwnedCollectionEntity : Entity<Guid>
    {
        public List<OwnedCollectionValue> Items { get; set; } = [];
    }

    private sealed class OwnedCollectionValue
    {
        public string Name { get; set; }
    }

    private sealed class RelatedEntityRoot : Entity<Guid>
    {
        public Guid RelatedId { get; set; }

        public RelatedEntity Related { get; set; }

        public List<RelatedEntity> RelatedItems { get; set; } = [];
    }

    private sealed class RelatedEntity : Entity<Guid>;

    private sealed class TypedIdEntity : Entity<MappingEntityId>;

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

    private sealed class NoWritableColumnsEntity : Entity<int>
    {
        public DateTime ComputedOn { get; set; }
    }

    private sealed class DuplicateColumnsEntity : Entity<Guid>
    {
        public string Name { get; set; }

        public DuplicateColumnsValue Details { get; set; }
    }

    private sealed class DuplicateColumnsValue
    {
        public string Name { get; set; }
    }

    private sealed class RequiredShadowEntity : Entity<Guid>;

    private sealed class RequiredShadowValueProvider(string value)
        : IEntityBulkInsertShadowValueProvider<RequiredShadowEntity>
    {
        private readonly string value = value;

        public bool TryGetValue(
            EntityBulkInsertShadowPropertyContext<RequiredShadowEntity> context,
            out object value
        )
        {
            value = context.Property.Name == "TenantId" ? this.value : null;
            return value is not null;
        }
    }

    private abstract class TphBaseEntity : Entity<Guid>;

    private sealed class TphDerivedEntity : TphBaseEntity;

    private abstract class TptBaseEntity : Entity<Guid>;

    private sealed class TptDerivedEntity : TptBaseEntity;

    private abstract class TpcBaseEntity : Entity<Guid>;

    private sealed class TpcDerivedEntity : TpcBaseEntity;

    private sealed class SeparateOwnedEntity : Entity<Guid>
    {
        public SeparateOwnedValue Details { get; set; }
    }

    private sealed class SeparateOwnedValue
    {
        public string Name { get; set; }
    }

    private sealed class JsonOwnedEntity : Entity<Guid>
    {
        public JsonOwnedValue Details { get; set; }
    }

    private sealed class JsonOwnedValue
    {
        public string Name { get; set; }
    }

    private sealed class RequiredClrEntity : Entity<Guid>
    {
        public string RequiredName { get; set; }
    }

    private sealed class SplitEntity : Entity<Guid>
    {
        public string SecondaryName { get; set; }
    }

    private sealed class UnmappedEntity : Entity<Guid>;

    private enum MappingStatus
    {
        Inactive,
        Active,
    }
}
