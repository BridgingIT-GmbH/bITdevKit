// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Outbox;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;

[IntegrationTest("Infrastructure")]
[Collection(nameof(TestEnvironmentCollection))]
public class SqlServerEntityBulkInsertProviderTests(
    ITestOutputHelper output,
    TestEnvironmentFixture fixture
)
{
    private readonly TestEnvironmentFixture fixture = fixture.WithOutput(output);

    [Fact]
    public async Task InsertAsync_FlatEntities_InsertsRowsWithProviderSpecificOptions()
    {
        // Arrange
        await using var verificationContext = this.fixture.EnsureSqlServerDbContext(output, true);
        using var serviceProvider = this.CreateServiceProvider<BulkInsertPersonStub, StubDbContext>(
            new SqlServerEntityBulkInsertOptions
            {
                SqlBulkCopyOptions = SqlBulkCopyOptions.TableLock,
            },
            verificationContext.Database.GetConnectionString()
        );
        using var scope = serviceProvider.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<
            IEntityBulkInserter<BulkInsertPersonStub>
        >();
        var marker = $"Bulk {DateTime.UtcNow.Ticks}";
        var entities = Enumerable
            .Range(1, 5)
            .Select(i => new BulkInsertPersonStub
            {
                FirstName = $"Bulk-{i}",
                LastName = marker,
                Age = 20 + i,
            })
            .ToList();

        // Act
        var result = await sut.InsertAsync(entities);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(entities.Count);
        entities.ShouldAllBe(e => e.Id != Guid.Empty);

        var stored = await verificationContext
            .BulkInsertPersons.Where(e => e.LastName == marker)
            .OrderBy(e => e.FirstName)
            .ToListAsync();

        stored.Count.ShouldBe(entities.Count);
        stored.Select(e => e.Age).ShouldBe([21, 22, 23, 24, 25]);
    }

    [Fact]
    public async Task InsertAsync_EntityWithSameTableOwnedReference_InsertsOwnedValues()
    {
        // Arrange
        await using var verificationContext = this.fixture.EnsureSqlServerDbContext(output, true);
        using var serviceProvider = this.CreateServiceProvider<PersonStub, StubDbContext>(
            connectionString: verificationContext.Database.GetConnectionString()
        );
        using var scope = serviceProvider.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<IEntityBulkInserter<PersonStub>>();
        var email = $"bulk.owned.{DateTime.UtcNow.Ticks}@example.com";
        var entity = new PersonStub("Bulk", "Owned", email, 42, Status.Active);

        // Act
        var result = await sut.InsertAsync([entity]);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(1);

        var stored = await verificationContext
            .Persons.AsNoTracking()
            .SingleAsync(e => e.Email.Value == email);

        stored.Email.Value.ShouldBe(email);
        stored.Status.ShouldBe(Status.Active);
    }

    [Fact]
    public async Task InsertAsync_DecoratedAggregate_PersistsAuditConcurrencyAndOutboxAtomically()
    {
        // Arrange
        await using var verificationContext = this.fixture.EnsureSqlServerDbContext(output, true);
        await EnsureDecoratedPersonTableAsync(verificationContext);
        using var serviceProvider = this.CreateServiceProvider<BulkInsertDecoratedPersonStub, StubDbContext>(
            connectionString: verificationContext.Database.GetConnectionString(),
            configure: builder => builder
                .WithBehavior<EntityBulkInserterOutboxDomainEventBehavior<BulkInsertDecoratedPersonStub, StubDbContext>>()
                .WithBehavior<EntityBulkInserterAuditStateBehavior<BulkInsertDecoratedPersonStub>>()
                .WithBehavior<EntityBulkInserterConcurrencyBehavior<BulkInsertDecoratedPersonStub>>()
                .WithBehavior<EntityBulkInserterDomainEventBehavior<BulkInsertDecoratedPersonStub>>());
        using var scope = serviceProvider.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<IEntityBulkInserter<BulkInsertDecoratedPersonStub>>();
        var entity = new BulkInsertDecoratedPersonStub { Name = $"Decorated {Guid.NewGuid():N}" };
        var existingOutboxCount = await verificationContext.OutboxDomainEvents.CountAsync(
            domainEvent => domainEvent.Type.Contains(nameof(EntityCreatedDomainEvent<BulkInsertDecoratedPersonStub>))
        );

        // Act
        var result = await sut.InsertAsync([entity]);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        entity.Id.ShouldNotBe(Guid.Empty);
        entity.ConcurrencyVersion.ShouldNotBe(Guid.Empty);
        entity.AuditState.CreatedDate.ShouldBeGreaterThan(DateTimeOffset.MinValue);
        entity.DomainEvents.GetAll().ShouldBeEmpty();

        var stored = await verificationContext.BulkInsertDecoratedPersons
            .AsNoTracking()
            .SingleAsync(person => person.Id == entity.Id);
        stored.ConcurrencyVersion.ShouldBe(entity.ConcurrencyVersion);
        stored.AuditState.CreatedDate.ShouldBe(entity.AuditState.CreatedDate);
        (await verificationContext.OutboxDomainEvents.CountAsync(
            domainEvent => domainEvent.Type.Contains(nameof(EntityCreatedDomainEvent<BulkInsertDecoratedPersonStub>))))
            .ShouldBe(existingOutboxCount + 1);
    }

    [Fact]
    public async Task InsertAsync_OutboxProjectionFailure_RollsBackRootWrite()
    {
        // Arrange
        await using var verificationContext = this.fixture.EnsureSqlServerDbContext(output, true);
        await EnsureDecoratedPersonTableAsync(verificationContext);
        using var serviceProvider = this.CreateServiceProvider<BulkInsertDecoratedPersonStub, StubDbContext>(
            connectionString: verificationContext.Database.GetConnectionString(),
            configure: builder => builder
                .WithBehavior<EntityBulkInserterOutboxDomainEventBehavior<BulkInsertDecoratedPersonStub, StubDbContext>>(
                    (inner, serviceProvider) => new EntityBulkInserterOutboxDomainEventBehavior<BulkInsertDecoratedPersonStub, StubDbContext>(
                        serviceProvider.GetRequiredService<StubDbContext>(),
                        inner,
                        options: new OutboxDomainEventOptions { Serializer = new ThrowingSerializer() }))
                .WithBehavior<EntityBulkInserterDomainEventBehavior<BulkInsertDecoratedPersonStub>>());
        using var scope = serviceProvider.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<IEntityBulkInserter<BulkInsertDecoratedPersonStub>>();
        var entity = new BulkInsertDecoratedPersonStub { Name = $"Rollback {Guid.NewGuid():N}" };

        // Act
        await Should.ThrowAsync<InvalidOperationException>(() => sut.InsertAsync([entity]));

        // Assert
        (await verificationContext.BulkInsertDecoratedPersons.CountAsync(person => person.Id == entity.Id))
            .ShouldBe(0);
    }

    [Fact]
    public async Task AddEntityFrameworkBulkInserter_AddSqlServerDbContext_RegistersTerminalAndOneNativeProvider()
    {
        // Arrange
        await using var setupContext = this.fixture.EnsureSqlServerDbContext(output, true);
        using var serviceProvider = this.CreateServiceProvider<BulkInsertPersonStub, StubDbContext>(
            connectionString: setupContext.Database.GetConnectionString()
        );
        using var scope = serviceProvider.CreateScope();

        // Act
        var inserter = scope.ServiceProvider.GetRequiredService<
            IEntityBulkInserter<BulkInsertPersonStub>
        >();
        var providers = scope.ServiceProvider.GetServices<IEntityBulkInsertProvider>().ToList();

        // Assert
        inserter.ShouldBeOfType<
            EntityFrameworkEntityBulkInserter<BulkInsertPersonStub, StubDbContext>
        >();
        providers.Count.ShouldBe(1);
        providers[0].ShouldBeOfType<SqlServerEntityBulkInsertProvider>();
    }

    [Fact]
    public async Task InsertAsync_EntityWithOwnedCollectionRows_ReturnsFailure()
    {
        // Arrange
        await using var setupContext = this.fixture.EnsureSqlServerDbContext(output, true);
        using var serviceProvider = this.CreateServiceProvider<PersonStub, StubDbContext>(
            connectionString: setupContext.Database.GetConnectionString()
        );
        using var scope = serviceProvider.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<IEntityBulkInserter<PersonStub>>();
        var entity = new PersonStub("Bulk", "Graph", "bulk.graph@example.com", 42).AddLocation(
            LocationStub.Create("Office", "Main Street", "1", "12345", "Berlin", "DE")
        );

        // Act
        var result = await sut.InsertAsync([entity]);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(e =>
            e.Message.Contains("populated owned collection", StringComparison.Ordinal)
        );
    }

    [Fact]
    public async Task InsertAsync_CallerOwnedEfTransaction_ReusesWithoutCommitting()
    {
        // Arrange
        await using var verificationContext = this.fixture.EnsureSqlServerDbContext(output, true);
        using var serviceProvider = this.CreateServiceProvider<BulkInsertPersonStub, StubDbContext>(
            connectionString: verificationContext.Database.GetConnectionString()
        );
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<StubDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<
            IEntityBulkInserter<BulkInsertPersonStub>
        >();
        var entity = new BulkInsertPersonStub
        {
            FirstName = "Ambient",
            LastName = $"Transaction {DateTime.UtcNow.Ticks}",
            Age = 42,
        };

        await using var transaction = await context.Database.BeginTransactionAsync();

        // Act
        var result = await sut.InsertAsync([entity]);
        context.Database.CurrentTransaction.ShouldBeSameAs(transaction);
        await transaction.CommitAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(1);
        (await verificationContext.BulkInsertPersons.CountAsync(e => e.Id == entity.Id)).ShouldBe(
            1
        );
    }

    [Fact]
    public async Task InsertAsync_CallerOwnedEfTransactionRollback_RemovesInsertedRows()
    {
        // Arrange
        await using var verificationContext = this.fixture.EnsureSqlServerDbContext(output, true);
        using var serviceProvider = this.CreateServiceProvider<BulkInsertPersonStub, StubDbContext>(
            connectionString: verificationContext.Database.GetConnectionString()
        );
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<StubDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<
            IEntityBulkInserter<BulkInsertPersonStub>
        >();
        var entity = new BulkInsertPersonStub
        {
            FirstName = "Caller",
            LastName = $"Rollback {DateTime.UtcNow.Ticks}",
            Age = 42,
        };
        await using var transaction = await context.Database.BeginTransactionAsync();

        // Act
        var result = await sut.InsertAsync([entity]);
        await transaction.RollbackAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await verificationContext.BulkInsertPersons.CountAsync(e => e.Id == entity.Id)).ShouldBe(
            0
        );
    }

    [Fact]
    public async Task InsertAsync_OwnedTransactionProviderFailure_RollsBackProviderWrite()
    {
        // Arrange
        await using var verificationContext = this.fixture.EnsureSqlServerDbContext(output, true);
        await verificationContext.Database.ExecuteSqlRawAsync(
            """
            IF OBJECT_ID(N'[dbo].[BulkInsertTransactionProbe]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[BulkInsertTransactionProbe]
                (
                    [Marker] nvarchar(128) NOT NULL PRIMARY KEY
                );
            END
            """
        );
        var marker = $"rollback-{Guid.NewGuid():N}";
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSqlServerDbContext<StubDbContext>(
            verificationContext.Database.GetConnectionString()
        );
        services.RemoveAll<IEntityBulkInsertProvider>();
        services.AddSingleton<IEntityBulkInsertProvider>(
            new WriteThenThrowBulkInsertProvider(marker)
        );
        services.AddEntityFrameworkBulkInserter<BulkInsertPersonStub, StubDbContext>();
        using var serviceProvider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true }
        );
        using var scope = serviceProvider.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<
            IEntityBulkInserter<BulkInsertPersonStub>
        >();

        // Act
        var result = await sut.InsertAsync([
            new BulkInsertPersonStub
            {
                FirstName = "Provider",
                LastName = "Failure",
                Age = 42,
            },
        ]);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(error => error is EntityBulkInsertProviderError);
        var probeCount = await verificationContext
            .Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS [Value] FROM [dbo].[BulkInsertTransactionProbe] WHERE [Marker] = {0}",
                marker
            )
            .SingleAsync();
        probeCount.ShouldBe(0);
    }

    [Fact]
    public async Task InsertAsync_SqlServerProviderWithoutActiveTransaction_ThrowsBeforeWrite()
    {
        // Arrange
        await using var context = this.fixture.EnsureSqlServerDbContext(output, true);
        var entity = new BulkInsertPersonStub
        {
            FirstName = "Missing",
            LastName = "Transaction",
            Age = 42,
        };
        var batch = new EntityBulkInsertMappingBuilder<BulkInsertPersonStub>().Build(
            context,
            [entity],
            new EntityBulkInsertOptions()
        );
        var sut = new SqlServerEntityBulkInsertProvider(NullLoggerFactory.Instance);
        await context.Database.OpenConnectionAsync();

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.InsertAsync(context, batch)
        );

        // Assert
        exception.Message.ShouldContain("active Microsoft.Data.SqlClient.SqlTransaction");
    }

    [Fact]
    public async Task InsertAsync_KeepGeneratedIdentityValues_PreservesIdentityValue()
    {
        // Arrange
        await using var setupContext = this.fixture.EnsureSqlServerDbContext(output, true);
        await setupContext.Database.ExecuteSqlRawAsync(
            """
            IF OBJECT_ID(N'[dbo].[BulkInsertIdentityPersons]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[BulkInsertIdentityPersons]
                (
                    [Id] int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                    [Name] nvarchar(128) NOT NULL
                );
            END
            """
        );
        await setupContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM [dbo].[BulkInsertIdentityPersons] WHERE [Id] = {0}",
            10_001
        );
        using var serviceProvider = this.CreateServiceProvider<
            IdentityBulkInsertPerson,
            IdentityBulkInsertDbContext
        >(
            new SqlServerEntityBulkInsertOptions { KeepGeneratedIdentityValues = true },
            setupContext.Database.GetConnectionString()
        );
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IdentityBulkInsertDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<
            IEntityBulkInserter<IdentityBulkInsertPerson>
        >();
        var entity = new IdentityBulkInsertPerson { Id = 10_001, Name = "Preserved identity" };

        // Act
        var result = await sut.InsertAsync([entity]);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(1);
        (
            await context
                .Set<IdentityBulkInsertPerson>()
                .AsNoTracking()
                .SingleAsync(e => e.Id == entity.Id)
        ).Name.ShouldBe(entity.Name);
    }

    [Theory]
    [InlineData(SqlBulkCopyOptions.KeepIdentity)]
    [InlineData(SqlBulkCopyOptions.UseInternalTransaction)]
    public async Task InsertAsync_ProviderManagedBulkCopyOption_ReturnsFailure(
        SqlBulkCopyOptions bulkCopyOption
    )
    {
        // Arrange
        await using var setupContext = this.fixture.EnsureSqlServerDbContext(output, true);
        using var serviceProvider = this.CreateServiceProvider<BulkInsertPersonStub, StubDbContext>(
            new SqlServerEntityBulkInsertOptions { SqlBulkCopyOptions = bulkCopyOption },
            setupContext.Database.GetConnectionString()
        );
        using var scope = serviceProvider.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<
            IEntityBulkInserter<BulkInsertPersonStub>
        >();

        // Act
        var result = await sut.InsertAsync([
            new BulkInsertPersonStub
            {
                FirstName = "Forbidden",
                LastName = "Option",
                Age = 42,
            },
        ]);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(error =>
            error.Message.Contains(
                nameof(SqlServerEntityBulkInsertOptions.SqlBulkCopyOptions),
                StringComparison.Ordinal
            )
            && error.Message.Contains(
                nameof(EntityBulkInsertOptions.KeepGeneratedIdentityValues),
                StringComparison.Ordinal
            )
        );
    }

    private ServiceProvider CreateServiceProvider<TEntity, TContext>(
        EntityBulkInsertOptions options = null,
        string connectionString = null,
        Action<EntityBulkInserterBuilderContext<TEntity>> configure = null
    )
        where TEntity : class, IEntity
        where TContext : DbContext
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSqlServerDbContext<TContext>(
            connectionString ?? this.fixture.SqlConnectionString
        );
        var builder = services.AddEntityFrameworkBulkInserter<TEntity, TContext>(options);
        configure?.Invoke(builder);

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static Task EnsureDecoratedPersonTableAsync(StubDbContext context)
    {
        return context.Database.ExecuteSqlRawAsync(
            """
            IF OBJECT_ID(N'[dbo].[BulkInsertDecoratedPersons]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[BulkInsertDecoratedPersons]
                (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [Name] nvarchar(128) NOT NULL,
                    [ConcurrencyVersion] uniqueidentifier NOT NULL,
                    [CreatedBy] nvarchar(256) NULL,
                    [CreatedDate] datetimeoffset NOT NULL,
                    [UpdatedBy] nvarchar(256) NULL,
                    [UpdatedDate] datetimeoffset NULL,
                    [CreatedDescription] nvarchar(1024) NULL,
                    [UpdatedDescription] nvarchar(1024) NULL,
                    [Deactivated] bit NULL,
                    [DeactivatedBy] nvarchar(256) NULL,
                    [DeactivatedDate] datetimeoffset NULL,
                    [DeactivatedDescription] nvarchar(1024) NULL,
                    [Deleted] bit NULL,
                    [DeletedBy] nvarchar(256) NULL,
                    [DeletedDate] datetimeoffset NULL,
                    [DeletedReason] nvarchar(1024) NULL,
                    [DeletedDescription] nvarchar(1024) NULL
                );
            END
            """
        );
    }

    private sealed class IdentityBulkInsertPerson : Entity<int>
    {
        public string Name { get; set; }
    }

    private sealed class WriteThenThrowBulkInsertProvider(string marker) : IEntityBulkInsertProvider
    {
        public string ProviderName => SqlServerEntityBulkInsertProvider.EntityFrameworkProviderName;

        public async Task<long> InsertAsync<TEntity>(
            DbContext context,
            EntityBulkInsertBatch<TEntity> batch,
            CancellationToken cancellationToken = default
        )
            where TEntity : class
        {
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO [dbo].[BulkInsertTransactionProbe] ([Marker]) VALUES ({0})",
                [marker],
                cancellationToken
            );
            throw new InvalidOperationException("Provider failure after transactional write.");
        }
    }

    private sealed class ThrowingSerializer : ISerializer
    {
        public void Serialize(object value, Stream output) => throw new InvalidOperationException("Outbox projection failure.");

        public object Deserialize(Stream input, Type type) => throw new NotSupportedException();

        public T Deserialize<T>(Stream input) => throw new NotSupportedException();
    }

    private sealed class IdentityBulkInsertDbContext(
        DbContextOptions<IdentityBulkInsertDbContext> options
    ) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IdentityBulkInsertPerson>(builder =>
            {
                builder.ToTable("BulkInsertIdentityPersons");
                builder.HasKey(entity => entity.Id);
                builder.Property(entity => entity.Id).UseIdentityColumn();
                builder.Property(entity => entity.Name).IsRequired().HasMaxLength(128);
            });
        }
    }
}
