// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

[IntegrationTest("Infrastructure")]
[Collection(nameof(TestEnvironmentCollection))]
public class SqlServerEntityBulkInsertProviderTests(ITestOutputHelper output, TestEnvironmentFixture fixture)
{
    private readonly TestEnvironmentFixture fixture = fixture.WithOutput(output);

    [Fact]
    public async Task InsertAsync_FlatEntities_InsertsRowsWithProviderSpecificOptions()
    {
        // Arrange
        await using var verificationContext = this.fixture.EnsureSqlServerDbContext(output, true);
        using var serviceProvider = this.CreateServiceProvider<BulkInsertPersonStub, StubDbContext>(
            new SqlServerEntityBulkInsertOptions { SqlBulkCopyOptions = SqlBulkCopyOptions.TableLock },
            verificationContext.Database.GetConnectionString());
        using var scope = serviceProvider.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<IEntityBulkInserter<BulkInsertPersonStub>>();
        var marker = $"Bulk {DateTime.UtcNow.Ticks}";
        var entities = Enumerable.Range(1, 5)
            .Select(i => new BulkInsertPersonStub
            {
                FirstName = $"Bulk-{i}",
                LastName = marker,
                Age = 20 + i
            })
            .ToList();

        // Act
        var result = await sut.InsertAsync(entities);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(entities.Count);
        entities.ShouldAllBe(e => e.Id != Guid.Empty);

        var stored = await verificationContext.BulkInsertPersons
            .Where(e => e.LastName == marker)
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
            connectionString: verificationContext.Database.GetConnectionString());
        using var scope = serviceProvider.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<IEntityBulkInserter<PersonStub>>();
        var email = $"bulk.owned.{DateTime.UtcNow.Ticks}@example.com";
        var entity = new PersonStub("Bulk", "Owned", email, 42, Status.Active);

        // Act
        var result = await sut.InsertAsync([entity]);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(1);

        var stored = await verificationContext.Persons
            .AsNoTracking()
            .SingleAsync(e => e.Email.Value == email);

        stored.Email.Value.ShouldBe(email);
        stored.Status.ShouldBe(Status.Active);
    }

    [Fact]
    public async Task WithBulkInsert_AddSqlServerDbContext_RegistersSharedInserterAndOneNativeProvider()
    {
        // Arrange
        await using var setupContext = this.fixture.EnsureSqlServerDbContext(output, true);
        using var serviceProvider = this.CreateServiceProvider<BulkInsertPersonStub, StubDbContext>(
            connectionString: setupContext.Database.GetConnectionString());
        using var scope = serviceProvider.CreateScope();

        // Act
        var inserter = scope.ServiceProvider.GetRequiredService<IEntityBulkInserter<BulkInsertPersonStub>>();
        var providers = scope.ServiceProvider.GetServices<IEntityBulkInsertProvider>().ToList();

        // Assert
        inserter.ShouldBeOfType<EntityFrameworkEntityBulkInserter<BulkInsertPersonStub, StubDbContext>>();
        providers.Count.ShouldBe(1);
        providers[0].ShouldBeOfType<SqlServerEntityBulkInsertProvider>();
    }

    [Fact]
    public async Task InsertAsync_EntityWithOwnedCollectionRows_ReturnsFailure()
    {
        // Arrange
        await using var setupContext = this.fixture.EnsureSqlServerDbContext(output, true);
        using var serviceProvider = this.CreateServiceProvider<PersonStub, StubDbContext>(
            connectionString: setupContext.Database.GetConnectionString());
        using var scope = serviceProvider.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<IEntityBulkInserter<PersonStub>>();
        var entity = new PersonStub("Bulk", "Graph", "bulk.graph@example.com", 42)
            .AddLocation(LocationStub.Create(
                "Office",
                "Main Street",
                "1",
                "12345",
                "Berlin",
                "DE"));

        // Act
        var result = await sut.InsertAsync([entity]);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Message.Contains("does not insert owned collection rows", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InsertAsync_AmbientTransaction_InsertsRows()
    {
        // Arrange
        await using var verificationContext = this.fixture.EnsureSqlServerDbContext(output, true);
        using var serviceProvider = this.CreateServiceProvider<BulkInsertPersonStub, StubDbContext>(
            connectionString: verificationContext.Database.GetConnectionString());
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<StubDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IEntityBulkInserter<BulkInsertPersonStub>>();
        var entity = new BulkInsertPersonStub
        {
            FirstName = "Ambient",
            LastName = $"Transaction {DateTime.UtcNow.Ticks}",
            Age = 42
        };

        await using var transaction = await context.Database.BeginTransactionAsync();

        // Act
        var result = await sut.InsertAsync([entity]);
        await transaction.CommitAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(1);
        (await verificationContext.BulkInsertPersons.CountAsync(e => e.Id == entity.Id)).ShouldBe(1);
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
            """);
        await setupContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM [dbo].[BulkInsertIdentityPersons] WHERE [Id] = {0}",
            10_001);
        using var serviceProvider = this.CreateServiceProvider<IdentityBulkInsertPerson, IdentityBulkInsertDbContext>(
            new SqlServerEntityBulkInsertOptions { KeepGeneratedIdentityValues = true },
            setupContext.Database.GetConnectionString());
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IdentityBulkInsertDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IEntityBulkInserter<IdentityBulkInsertPerson>>();
        var entity = new IdentityBulkInsertPerson { Id = 10_001, Name = "Preserved identity" };

        // Act
        var result = await sut.InsertAsync([entity]);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(1);
        (await context.Set<IdentityBulkInsertPerson>().AsNoTracking().SingleAsync(e => e.Id == entity.Id)).Name.ShouldBe(entity.Name);
    }

    [Theory]
    [InlineData(SqlBulkCopyOptions.KeepIdentity)]
    [InlineData(SqlBulkCopyOptions.UseInternalTransaction)]
    public async Task InsertAsync_ProviderManagedBulkCopyOption_ReturnsFailure(SqlBulkCopyOptions bulkCopyOption)
    {
        // Arrange
        await using var setupContext = this.fixture.EnsureSqlServerDbContext(output, true);
        using var serviceProvider = this.CreateServiceProvider<BulkInsertPersonStub, StubDbContext>(
            new SqlServerEntityBulkInsertOptions { SqlBulkCopyOptions = bulkCopyOption },
            setupContext.Database.GetConnectionString());
        using var scope = serviceProvider.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<IEntityBulkInserter<BulkInsertPersonStub>>();

        // Act
        var result = await sut.InsertAsync([new BulkInsertPersonStub
        {
            FirstName = "Forbidden",
            LastName = "Option",
            Age = 42
        }]);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(error =>
            error.Message.Contains(nameof(SqlServerEntityBulkInsertOptions.SqlBulkCopyOptions), StringComparison.Ordinal) &&
            error.Message.Contains(nameof(EntityBulkInsertOptions.KeepGeneratedIdentityValues), StringComparison.Ordinal));
    }

    private ServiceProvider CreateServiceProvider<TEntity, TContext>(
        EntityBulkInsertOptions options = null,
        string connectionString = null)
        where TEntity : class, IEntity
        where TContext : DbContext
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSqlServerDbContext<TContext>(connectionString ?? this.fixture.SqlConnectionString);
        services.AddEntityFrameworkRepository<TEntity, TContext>()
            .WithBulkInsert(options);

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private sealed class IdentityBulkInsertPerson : Entity<int>
    {
        public string Name { get; set; }
    }

    private sealed class IdentityBulkInsertDbContext(DbContextOptions<IdentityBulkInsertDbContext> options) : DbContext(options)
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
